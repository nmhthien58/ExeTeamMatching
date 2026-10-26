using System.Security.Claims;
using BuildingBlocks.Auth;
using Team.Repositories.AuditLog;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Teams;

namespace Team.Services.Members;

/// <summary>Schema <c>MemberChangeResult</c>: <c>appliedImmediately = false</c> khi nhóm EXE201 kế thừa ghi thay đổi chờ duyệt.</summary>
public sealed record MemberChangeResult(bool AppliedImmediately, TeamDetailResponse Team);

public sealed record TransferLeaderRequest(Guid StudentId);

public interface IMemberService
{
    /// <summary>S17 – trưởng nhóm xóa thành viên.</summary>
    Task<MemberChangeResult> RemoveAsync(Guid teamId, Guid studentId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S17 – thành viên rời nhóm; trưởng nhóm phải chuyển quyền trước.</summary>
    Task<MemberChangeResult> LeaveAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S17 – chuyển quyền trưởng nhóm (áp dụng ngay, cả nhóm kế thừa).</summary>
    Task<MemberChangeResult> TransferLeaderAsync(Guid teamId, TransferLeaderRequest request, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Module Members của Team Service (BE-30, rule 1, 6, 7, 10, 15).</summary>
public sealed class MemberService(
    ITeamRepository teams, IMemberRepository members, MembershipService membership, RoundGuard rounds, AuditLogWriter audit,
    ITeamService teamService) : IMemberService
{
    public async Task<MemberChangeResult> RemoveAsync(Guid teamId, Guid studentId, ClaimsPrincipal user, CancellationToken ct)
    {
        var (team, current, me) = await LoadAsync(teamId, user, ct);
        RequireLeader(me);
        if (studentId == me.StudentId)
        {
            throw TeamErrors.Invalid("studentId", "Trưởng nhóm không tự xóa mình; chuyển quyền rồi rời nhóm.");
        }
        var target = current.FirstOrDefault(m => m.StudentId == studentId) ?? throw TeamErrors.MemberNotFound(studentId);
        await rounds.RequireMembershipRoundAsync(team, ct);

        var applied = await membership.RemoveAsync(team, target, "REMOVED", user.ToAuditActor(), ct);
        await members.SaveChangesAsync(ct);
        return new(applied, await teamService.GetAsync(teamId, user, ct));
    }

    public async Task<MemberChangeResult> LeaveAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var (team, _, me) = await LoadAsync(teamId, user, ct);
        if (me.Role == MemberRoles.Leader)
        {
            throw TeamErrors.LeaderMustTransfer();
        }
        await rounds.RequireMembershipRoundAsync(team, ct);

        var applied = await membership.RemoveAsync(team, me, "LEAVE", user.ToAuditActor(), ct);
        await members.SaveChangesAsync(ct);
        return new(applied, await teamService.GetAsync(teamId, user, ct));
    }

    public async Task<MemberChangeResult> TransferLeaderAsync(Guid teamId, TransferLeaderRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (team, current, me) = await LoadAsync(teamId, user, ct);
        RequireLeader(me);
        var target = current.FirstOrDefault(m => m.StudentId == request.StudentId) ?? throw TeamErrors.MemberNotFound(request.StudentId);
        if (target.StudentId == me.StudentId)
        {
            throw TeamErrors.Invalid("studentId", "Bạn đang là trưởng nhóm.");
        }
        await rounds.RequireMembershipRoundAsync(team, ct);

        (me.Role, target.Role) = (MemberRoles.Member, MemberRoles.Leader);
        audit.Add(team.Id, "TeamMember", target.StudentId, "LEADER_CHANGED", user.ToAuditActor(),
            new { leader = me.StudentCode }, new { leader = target.StudentCode });
        members.AddEvent(MemberEventTypes.LeaderChanged, team.Id, new LeaderChanged(team.Id, me.StudentId, target.StudentId));
        await members.SaveChangesAsync(ct);
        return new(true, await teamService.GetAsync(teamId, user, ct));
    }

    private async Task<(TeamEntity Team, List<TeamMember> Members, TeamMember Me)> LoadAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var team = await teams.GetAsync(teamId, ct);
        if (team is null || team.Status == TeamStatuses.Disbanded)
        {
            throw TeamErrors.TeamNotFound(teamId);
        }
        var current = await members.ListAsync(teamId, ct);
        var me = current.FirstOrDefault(m => m.StudentId == studentId) ?? throw TeamErrors.NotMember();
        return (team, current, me);
    }

    private static void RequireLeader(TeamMember me)
    {
        if (me.Role != MemberRoles.Leader)
        {
            throw TeamErrors.NotLeader();
        }
    }
}
