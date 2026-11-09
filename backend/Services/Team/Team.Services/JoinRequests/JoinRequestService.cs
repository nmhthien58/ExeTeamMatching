using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Team.Repositories.AuditLog;
using Team.Repositories.JoinRequests;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Invitations;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Services.JoinRequests;

public interface IJoinRequestService
{
    /// <summary>S09 – xin vào nhóm kèm 1–3 ý tưởng hoặc "Chưa có ý tưởng" (rule 17); chỉ trong đợt lập nhóm (rule 18); hết hạn sau 72 giờ.</summary>
    Task<JoinRequestResponse> SubmitAsync(Guid teamId, SubmitJoinRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S20 tab 1 – yêu cầu gửi tới nhóm (trưởng nhóm, Admin); ý tưởng chỉ nhóm thấy.</summary>
    Task<IReadOnlyList<JoinRequestResponse>> ListForTeamAsync(Guid teamId, string? status, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S12 tab 2 – yêu cầu tôi đã gửi.</summary>
    Task<IReadOnlyList<JoinRequestResponse>> ListMineAsync(string? status, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Trưởng nhóm chấp nhận: vào nhóm ngay, hoặc thay đổi chờ duyệt với nhóm EXE201 kế thừa (rule 7).</summary>
    Task<MemberChangeResult> AcceptAsync(Guid joinRequestId, ClaimsPrincipal user, CancellationToken ct);

    Task DeclineAsync(Guid joinRequestId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Sinh viên hủy yêu cầu của mình.</summary>
    Task CancelAsync(Guid joinRequestId, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Module JoinRequests của Team Service (BE-29).</summary>
public sealed class JoinRequestService(
    IJoinRequestRepository requests, ITeamRepository teams, MembershipService membership, RoundGuard rounds, TeamReader reader,
    AuditLogWriter audit, ITeamService teamService, TimeProvider clock) : IJoinRequestService
{
    public async Task<JoinRequestResponse> SubmitAsync(Guid teamId, SubmitJoinRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var ideas = request.Ideas ?? [];
        if (request.HasNoIdea == ideas.Count > 0)
        {
            throw TeamErrors.IdeasRequired();
        }

        var graph = await teams.GetGraphAsync(teamId, ct);
        if (graph is null || graph.Team.Status == TeamStatuses.Disbanded)
        {
            throw TeamErrors.TeamNotFound(teamId);
        }
        var team = graph.Team;
        if (!team.IsInherited && team.Status is not (TeamStatuses.Draft or TeamStatuses.Active))
        {
            throw TeamErrors.RoundClosed();
        }
        await rounds.RequireTeamFormationAsync(team.CourseCode, team.SemesterId, ct);
        var me = await rounds.LookupAsync(team.SemesterId, studentId, ct);
        if (me.CourseCode != team.CourseCode)
        {
            throw TeamErrors.Invalid("teamId", $"Nhóm thuộc môn {team.CourseCode}, bạn học {me.CourseCode}.");
        }
        if (await teams.IsStudentInTeamAsync(team.SemesterId, studentId, ct))
        {
            throw TeamErrors.AlreadyInTeam();
        }
        var maxSize = (await rounds.TeamRulesAsync(ct)).MaxSize;
        if (graph.Members.Count >= maxSize)
        {
            throw TeamErrors.TeamFull(maxSize);
        }

        var now = clock.GetUtcNow();
        var joinRequest = new JoinRequest
        {
            Id = Guid.NewGuid(), TeamId = team.Id, StudentId = studentId, HasNoIdea = request.HasNoIdea,
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            CreatedAt = now, ExpiresAt = now + TeamRules.RequestLifetime,
        };
        requests.Add(joinRequest);
        for (var i = 0; i < ideas.Count; i++)
        {
            requests.Add(new JoinRequestIdea
            {
                JoinRequestId = joinRequest.Id, Position = (short)(i + 1), Title = ideas[i].Title.Trim(),
                Description = string.IsNullOrWhiteSpace(ideas[i].Description) ? null : ideas[i].Description!.Trim(),
            });
        }
        var leaderId = graph.Members.First(m => m.Role == MemberRoles.Leader).StudentId;
        audit.Add(team.Id, "JoinRequest", joinRequest.Id, "JOIN_REQUEST_SUBMITTED", user.ToAuditActor(),
            newValues: new { me.StudentCode, ideaCount = ideas.Count, request.HasNoIdea });
        requests.AddEvent(JoinRequestEventTypes.JoinRequestSubmitted, joinRequest.Id,
            new JoinRequestSubmitted(joinRequest.Id, team.Id, studentId, leaderId, joinRequest.ExpiresAt, ideas.Count, request.HasNoIdea));
        await requests.SaveChangesAsync(ct);
        return (await ToResponsesAsync([joinRequest], ct)).Single();
    }

    public async Task<IReadOnlyList<JoinRequestResponse>> ListForTeamAsync(Guid teamId, string? status, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.IsInRole(Roles.Admin))
        {
            await RequireLeaderAsync(teamId, user, ct);
        }
        return await ToResponsesAsync(await requests.ListByTeamAsync(teamId, status, clock.GetUtcNow(), ct), ct);
    }

    public async Task<IReadOnlyList<JoinRequestResponse>> ListMineAsync(string? status, ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        return await ToResponsesAsync(await requests.ListByStudentAsync(studentId, status, clock.GetUtcNow(), ct), ct);
    }

    public async Task<MemberChangeResult> AcceptAsync(Guid joinRequestId, ClaimsPrincipal user, CancellationToken ct)
    {
        var joinRequest = await requests.GetAsync(joinRequestId, ct) ?? throw NotFound(joinRequestId);
        var leaderId = await RequireLeaderAsync(joinRequest.TeamId, user, ct);
        RequirePending(joinRequest);
        var team = await teams.GetAsync(joinRequest.TeamId, ct) ?? throw TeamErrors.TeamNotFound(joinRequest.TeamId);
        await rounds.RequireMembershipRoundAsync(team, ct);
        var student = await rounds.LookupAsync(team.SemesterId, joinRequest.StudentId, ct);

        var applied = await membership.JoinAsync(team, student, "JOIN_REQUEST", user.ToAuditActor(), joinRequest.Id, ct);
        Resolve(joinRequest, RequestStatuses.Accepted, leaderId, JoinRequestEventTypes.JoinRequestAccepted);
        await requests.SaveChangesAsync(ct);
        return new(applied, await teamService.GetAsync(team.Id, user, ct));
    }

    public async Task DeclineAsync(Guid joinRequestId, ClaimsPrincipal user, CancellationToken ct)
    {
        var joinRequest = await requests.GetAsync(joinRequestId, ct) ?? throw NotFound(joinRequestId);
        var leaderId = await RequireLeaderAsync(joinRequest.TeamId, user, ct);
        RequirePending(joinRequest);
        Resolve(joinRequest, RequestStatuses.Declined, leaderId, JoinRequestEventTypes.JoinRequestDeclined);
        await requests.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid joinRequestId, ClaimsPrincipal user, CancellationToken ct)
    {
        var joinRequest = await requests.GetAsync(joinRequestId, ct);
        if (joinRequest is null || joinRequest.StudentId != user.GetStudentId())
        {
            throw NotFound(joinRequestId);
        }
        RequirePending(joinRequest);
        (joinRequest.Status, joinRequest.ResolvedAt) = (RequestStatuses.Cancelled, clock.GetUtcNow());
        audit.Add(joinRequest.TeamId, "JoinRequest", joinRequest.Id, "JOIN_REQUEST_CANCELLED", user.ToAuditActor());
        await requests.SaveChangesAsync(ct);
    }

    private void Resolve(JoinRequest joinRequest, string status, Guid leaderId, string eventType)
    {
        (joinRequest.Status, joinRequest.ResolvedAt, joinRequest.ResolvedByStudentId) = (status, clock.GetUtcNow(), leaderId);
        requests.AddEvent(eventType, joinRequest.Id, new JoinRequestResolved(joinRequest.Id, joinRequest.TeamId, joinRequest.StudentId));
    }

    private void RequirePending(JoinRequest joinRequest)
    {
        if (joinRequest.Status != RequestStatuses.Pending || joinRequest.ExpiresAt <= clock.GetUtcNow())
        {
            throw TeamErrors.RequestNotPending();
        }
    }

    private async Task<Guid> RequireLeaderAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        var studentId = user.GetStudentId();
        return graph.Members.Any(m => m.StudentId == studentId && m.Role == MemberRoles.Leader) ? studentId!.Value : throw TeamErrors.NotLeader();
    }

    private async Task<List<JoinRequestResponse>> ToResponsesAsync(IReadOnlyList<JoinRequest> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return [];
        }
        var (cards, students) = await reader.CardsAndStudentsAsync(rows.Select(r => (r.TeamId, r.StudentId)).ToList(), ct);
        var ideas = await requests.ListIdeasAsync(rows.Select(r => r.Id).ToList(), ct);
        var now = clock.GetUtcNow();
        return rows.Select(r => new JoinRequestResponse(r.Id, cards.GetValueOrDefault(r.TeamId), students.GetValueOrDefault(r.StudentId)!,
            reader.SkillsOf(r.StudentId), reader.DescriptionOf(r.StudentId),
            r.Message, ideas[r.Id].Select(i => new IdeaDto(i.Title, i.Description)).ToList(), r.HasNoIdea,
            RequestStatuses.Display(r.Status, r.ExpiresAt, now), r.CreatedAt, r.ExpiresAt)).ToList();
    }

    private static NotFoundException NotFound(Guid id) => new("yêu cầu tham gia", id);
}
