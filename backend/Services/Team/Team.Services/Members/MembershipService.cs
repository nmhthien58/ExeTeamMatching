using Team.Repositories.AuditLog;
using Team.Repositories.ChangeRequests;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.LeaderVotes;
using Team.Services.Teams;

namespace Team.Services.Members;

public static class MemberEventTypes
{
    public const string MemberJoined = "MemberJoined";
    public const string MemberLeft = "MemberLeft";
    public const string LeaderChanged = "LeaderChanged";
}

// Payload exe.team (docs/contracts/events.md), key = teamId.
public sealed record MemberJoined(Guid TeamId, Guid SemesterId, Guid StudentId, string FullName, int SpecializationId, string JoinSource, int MemberCount);

/// <summary><c>Reason</c>: LEAVE | REMOVED | CHANGE_REQUEST | ADMIN.</summary>
public sealed record MemberLeft(Guid TeamId, Guid SemesterId, Guid StudentId, string Reason, int MemberCount);

public sealed record LeaderChanged(Guid TeamId, Guid OldLeaderStudentId, Guid NewLeaderStudentId);

/// <summary>
/// Vào / rời nhóm dùng chung cho lời mời, yêu cầu tham gia, rời nhóm, xóa thành viên.
/// Nhóm EXE201 kế thừa (rule 7): không đổi thành viên ngay mà ghi thay đổi chờ duyệt (<see cref="ChangeRequestItem"/> chưa gắn yêu cầu);
/// gom vào yêu cầu đổi nhóm và duyệt thuộc module ChangeRequests.
/// </summary>
public sealed class MembershipService(IMemberRepository members, ITeamRepository teams, AuditLogWriter audit, LeaderVoteCloser leaderVotes, IAcademicClient academic, TimeProvider clock)
{
    /// <summary>
    /// Thêm sinh viên vào nhóm; true nếu vào ngay, false nếu thành thay đổi chờ duyệt.
    /// Khi vào ngay, các lời mời / yêu cầu khác còn chờ của sinh viên trong học kỳ bị hủy (rule 4), trừ <paramref name="keepRequestId"/>.
    /// </summary>
    public async Task<bool> JoinAsync(TeamEntity team, StudentSnapshot student, string source, AuditActor actor, Guid keepRequestId, CancellationToken ct)
    {
        var current = await members.ListAsync(team.Id, ct);
        var rules = await academic.GetTeamRulesAsync(ct);
        if (current.Count >= rules.MaxSize)
        {
            throw TeamErrors.TeamFull(rules.MaxSize);
        }
        if (await teams.IsStudentInTeamAsync(team.SemesterId, student.Id, ct))
        {
            throw TeamErrors.AlreadyInTeam();
        }

        if (team.IsInherited)
        {
            members.Add(new ChangeRequestItem
            {
                TeamId = team.Id, Action = "ADD", Source = source, StudentId = student.Id, StudentCode = student.StudentCode,
                FullName = student.FullName, SpecializationId = student.SpecializationId,
            });
            audit.Add(team.Id, "TeamMember", student.Id, "MEMBER_ADD_PENDING", actor, newValues: new { student.StudentCode, source });
            return false;
        }

        members.Add(new TeamMember
        {
            TeamId = team.Id, SemesterId = team.SemesterId, StudentId = student.Id, StudentCode = student.StudentCode, FullName = student.FullName,
            SpecializationId = student.SpecializationId ?? 0, Role = MemberRoles.Member, JoinSource = source,
        });
        var count = current.Count + 1;
        team.Status = TeamRules.StatusAfterMemberChange(team.Status, count, rules);
        await CancelOtherPendingAsync(team.SemesterId, student.Id, keepRequestId, ct);
        audit.Add(team.Id, "TeamMember", student.Id, "MEMBER_JOINED", actor, newValues: new { student.StudentCode, source, memberCount = count });
        members.AddEvent(MemberEventTypes.MemberJoined, team.Id,
            new MemberJoined(team.Id, team.SemesterId, student.Id, student.FullName, student.SpecializationId ?? 0, source, count));
        return true;
    }

    /// <summary>
    /// Thành viên rời nhóm (<paramref name="reason"/> LEAVE) hoặc bị trưởng nhóm xóa (REMOVED); true nếu áp dụng ngay.
    /// </summary>
    public async Task<bool> RemoveAsync(TeamEntity team, TeamMember member, string reason, AuditActor actor, CancellationToken ct)
    {
        if (team.IsInherited)
        {
            members.Add(new ChangeRequestItem
            {
                TeamId = team.Id, Action = "REMOVE", Source = reason == "LEAVE" ? "LEAVE" : "REMOVE", StudentId = member.StudentId,
                StudentCode = member.StudentCode, FullName = member.FullName, SpecializationId = member.SpecializationId,
            });
            audit.Add(team.Id, "TeamMember", member.StudentId, "MEMBER_REMOVE_PENDING", actor, newValues: new { member.StudentCode, reason });
            return false;
        }

        var current = await members.ListAsync(team.Id, ct);
        var count = current.Count - 1;
        await leaderVotes.CancelOpenAsync(team, current, member.StudentId, ct);
        members.Remove(member);
        team.Status = TeamRules.StatusAfterMemberChange(team.Status, count, await academic.GetTeamRulesAsync(ct));
        audit.Add(team.Id, "TeamMember", member.StudentId, reason == "LEAVE" ? "MEMBER_LEFT" : "MEMBER_REMOVED", actor,
            oldValues: new { member.StudentCode, member.Role }, newValues: new { memberCount = count });
        members.AddEvent(MemberEventTypes.MemberLeft, team.Id, new MemberLeft(team.Id, team.SemesterId, member.StudentId, reason, count));
        return true;
    }

    private async Task CancelOtherPendingAsync(Guid semesterId, Guid studentId, Guid keepRequestId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var (invitations, requests) = await members.ListPendingOfStudentAsync(semesterId, studentId, ct);
        foreach (var invitation in invitations.Where(i => i.Id != keepRequestId))
        {
            (invitation.Status, invitation.ResolvedAt) = ("CANCELLED", now);
        }
        foreach (var request in requests.Where(r => r.Id != keepRequestId))
        {
            (request.Status, request.ResolvedAt) = ("CANCELLED", now);
        }
    }
}
