using System.Security.Claims;
using BuildingBlocks.Auth;
using Team.Repositories.AuditLog;
using Team.Repositories.Invitations;
using Team.Repositories.JoinRequests;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Services.Invitations;

public interface IInvitationService
{
    /// <summary>S04, S18, S19 – trưởng nhóm mời sinh viên chưa có nhóm cùng môn; hết hạn sau 72 giờ (rule 8).</summary>
    Task<InvitationResponse> SendAsync(Guid teamId, SendInvitationRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S20 tab 2 – lời mời nhóm đã gửi (trưởng nhóm, Admin).</summary>
    Task<IReadOnlyList<InvitationResponse>> ListForTeamAsync(Guid teamId, string? status, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S12 tab 1 – lời mời tôi nhận được.</summary>
    Task<IReadOnlyList<InvitationResponse>> ListMineAsync(string? status, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Vào nhóm ngay, hoặc thành thay đổi chờ duyệt với nhóm EXE201 kế thừa (rule 7).</summary>
    Task<MemberChangeResult> AcceptAsync(Guid invitationId, ClaimsPrincipal user, CancellationToken ct);

    Task DeclineAsync(Guid invitationId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Trưởng nhóm thu hồi.</summary>
    Task CancelAsync(Guid invitationId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>InvitationExpiryJob: lời mời / yêu cầu tham gia quá hạn → EXPIRED; phát InvitationExpired cho lời mời.</summary>
    Task<ExpireResult> ExpireOverdueAsync(CancellationToken ct);
}

/// <summary>Module Invitations của Team Service (BE-28).</summary>
public sealed class InvitationService(
    IInvitationRepository invitations, IJoinRequestRepository joinRequests, ITeamRepository teams, MembershipService membership,
    RoundGuard rounds, TeamReader reader, AuditLogWriter audit, ITeamService teamService, TimeProvider clock) : IInvitationService
{
    public async Task<InvitationResponse> SendAsync(Guid teamId, SendInvitationRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (graph, leaderId) = await RequireLeaderAsync(teamId, user, ct);
        var team = graph.Team;
        await rounds.RequireMembershipRoundAsync(team, ct);
        if (request.StudentId == leaderId)
        {
            throw TeamErrors.Invalid("studentId", "Không thể mời chính mình.");
        }
        var invitee = await rounds.LookupAsync(team.SemesterId, request.StudentId, ct);
        if (invitee.CourseCode != team.CourseCode)
        {
            throw TeamErrors.Invalid("studentId", $"Sinh viên không học {team.CourseCode}.");
        }
        if (await teams.IsStudentInTeamAsync(team.SemesterId, invitee.Id, ct))
        {
            throw TeamErrors.AlreadyInTeam();
        }
        var maxSize = (await rounds.TeamRulesAsync(ct)).MaxSize;
        if (graph.Members.Count >= maxSize)
        {
            throw TeamErrors.TeamFull(maxSize);
        }

        var now = clock.GetUtcNow();
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(), TeamId = team.Id, InviteeStudentId = invitee.Id, InvitedByStudentId = leaderId,
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            CreatedAt = now, ExpiresAt = now + TeamRules.RequestLifetime,
        };
        invitations.Add(invitation);
        audit.Add(team.Id, "Invitation", invitation.Id, "INVITATION_SENT", user.ToAuditActor(), newValues: new { invitee.StudentCode });
        invitations.AddEvent(InvitationEventTypes.InvitationSent, invitation.Id,
            new InvitationSent(invitation.Id, team.Id, team.Name, invitee.Id, leaderId, invitation.ExpiresAt));
        await invitations.SaveChangesAsync(ct);
        return (await ToResponsesAsync([invitation], ct)).Single();
    }

    public async Task<IReadOnlyList<InvitationResponse>> ListForTeamAsync(Guid teamId, string? status, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.IsInRole(Roles.Admin))
        {
            await RequireLeaderAsync(teamId, user, ct);
        }
        return await ToResponsesAsync(await invitations.ListByTeamAsync(teamId, status, clock.GetUtcNow(), ct), ct);
    }

    public async Task<IReadOnlyList<InvitationResponse>> ListMineAsync(string? status, ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        return await ToResponsesAsync(await invitations.ListForInviteeAsync(studentId, status, clock.GetUtcNow(), ct), ct);
    }

    public async Task<MemberChangeResult> AcceptAsync(Guid invitationId, ClaimsPrincipal user, CancellationToken ct)
    {
        var invitation = await GetForInviteeAsync(invitationId, user, ct);
        var team = await teams.GetAsync(invitation.TeamId, ct) ?? throw TeamErrors.TeamNotFound(invitation.TeamId);
        await rounds.RequireMembershipRoundAsync(team, ct);
        var invitee = await rounds.LookupAsync(team.SemesterId, invitation.InviteeStudentId, ct);

        var applied = await membership.JoinAsync(team, invitee, "INVITATION", user.ToAuditActor(), invitation.Id, ct);
        await ResolveAsync(invitation, RequestStatuses.Accepted, InvitationEventTypes.InvitationAccepted, ct);
        await invitations.SaveChangesAsync(ct);
        return new(applied, await teamService.GetAsync(team.Id, user, ct));
    }

    public async Task DeclineAsync(Guid invitationId, ClaimsPrincipal user, CancellationToken ct)
    {
        var invitation = await GetForInviteeAsync(invitationId, user, ct);
        await ResolveAsync(invitation, RequestStatuses.Declined, InvitationEventTypes.InvitationDeclined, ct);
        await invitations.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid invitationId, ClaimsPrincipal user, CancellationToken ct)
    {
        var invitation = await invitations.GetAsync(invitationId, ct) ?? throw NotFound(invitationId);
        await RequireLeaderAsync(invitation.TeamId, user, ct);
        RequirePending(invitation);
        (invitation.Status, invitation.ResolvedAt) = (RequestStatuses.Cancelled, clock.GetUtcNow());
        audit.Add(invitation.TeamId, "Invitation", invitation.Id, "INVITATION_CANCELLED", user.ToAuditActor());
        await invitations.SaveChangesAsync(ct);
    }

    public async Task<ExpireResult> ExpireOverdueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var overdue = await invitations.ListOverdueAsync(now, ct);
        var requests = await joinRequests.ListOverdueAsync(now, ct);
        var leaders = await LeadersAsync(overdue.Select(i => i.TeamId), ct);
        foreach (var invitation in overdue)
        {
            (invitation.Status, invitation.ResolvedAt) = (RequestStatuses.Expired, now);
            invitations.AddEvent(InvitationEventTypes.InvitationExpired, invitation.Id,
                new InvitationResolved(invitation.Id, invitation.TeamId, invitation.InviteeStudentId, leaders.GetValueOrDefault(invitation.TeamId)));
        }
        foreach (var request in requests)
        {
            (request.Status, request.ResolvedAt) = (RequestStatuses.Expired, now);
        }
        await invitations.SaveChangesAsync(ct);
        return new ExpireResult(overdue.Count, requests.Count);
    }

    private async Task<Invitation> GetForInviteeAsync(Guid invitationId, ClaimsPrincipal user, CancellationToken ct)
    {
        var invitation = await invitations.GetAsync(invitationId, ct);
        if (invitation is null || invitation.InviteeStudentId != user.GetStudentId())
        {
            throw NotFound(invitationId);
        }
        RequirePending(invitation);
        return invitation;
    }

    private void RequirePending(Invitation invitation)
    {
        if (invitation.Status != RequestStatuses.Pending || invitation.ExpiresAt <= clock.GetUtcNow())
        {
            throw TeamErrors.RequestNotPending();
        }
    }

    private async Task ResolveAsync(Invitation invitation, string status, string eventType, CancellationToken ct)
    {
        (invitation.Status, invitation.ResolvedAt) = (status, clock.GetUtcNow());
        var leaderId = (await LeadersAsync([invitation.TeamId], ct)).GetValueOrDefault(invitation.TeamId);
        invitations.AddEvent(eventType, invitation.Id, new InvitationResolved(invitation.Id, invitation.TeamId, invitation.InviteeStudentId, leaderId));
    }

    private async Task<(TeamGraph Graph, Guid LeaderId)> RequireLeaderAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct);
        if (graph is null || graph.Team.Status == TeamStatuses.Disbanded)
        {
            throw TeamErrors.TeamNotFound(teamId);
        }
        var studentId = user.GetStudentId();
        return graph.Members.Any(m => m.StudentId == studentId && m.Role == MemberRoles.Leader)
            ? (graph, studentId!.Value)
            : throw TeamErrors.NotLeader();
    }

    private async Task<Dictionary<Guid, Guid>> LeadersAsync(IEnumerable<Guid> teamIds, CancellationToken ct) =>
        (await teams.ListGraphsAsync(teamIds.Distinct().ToList(), ct))
            .Where(g => g.Members.Any(m => m.Role == MemberRoles.Leader))
            .ToDictionary(g => g.Team.Id, g => g.Members.First(m => m.Role == MemberRoles.Leader).StudentId);

    private async Task<List<InvitationResponse>> ToResponsesAsync(IReadOnlyList<Invitation> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return [];
        }
        var (cards, students) = await reader.CardsAndStudentsAsync(rows.Select(i => (i.TeamId, i.InviteeStudentId)).ToList(), ct);
        var now = clock.GetUtcNow();
        return rows.Select(i => new InvitationResponse(i.Id, cards.GetValueOrDefault(i.TeamId), students.GetValueOrDefault(i.InviteeStudentId)!,
            i.Message, RequestStatuses.Display(i.Status, i.ExpiresAt, now), i.CreatedAt, i.ExpiresAt)).ToList();
    }

    private static BuildingBlocks.Errors.NotFoundException NotFound(Guid id) => new("lời mời", id);
}
