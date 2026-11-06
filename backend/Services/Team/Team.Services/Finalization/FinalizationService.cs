using System.Security.Claims;
using BuildingBlocks.Errors;
using Team.Repositories.AuditLog;
using Team.Repositories.Finalization;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Members;
using Team.Services.Teams;
using Team.Services.TopicInterests;

namespace Team.Services.Finalization;

public interface IFinalizationService
{
    /// <summary>Job sau khi đợt lập nhóm đóng: nhóm hợp lệ → Đã chốt, còn lại → Cần xem lại (rule 13). Gọi lại không đổi nhóm đã xử lý.</summary>
    Task<FinalizeRoundResult> FinalizeRoundAsync(FinalizeRoundRequest request, CancellationToken ct);

    /// <summary>W16, W22 – Admin chốt một nhóm; 409 composition-invalid nếu chưa đủ rule 1, 2.</summary>
    Task<TeamDetailResponse> FinalizeAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>W16 – Admin mở lại nhóm đã chốt: còn đợt lập nhóm thì về Nháp / Hoạt động, hết đợt thì Cần xem lại.</summary>
    Task<TeamDetailResponse> ReopenAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>W16, W21 – Admin thêm sinh viên chưa có nhóm vào nhóm (mọi lúc, kể cả nhóm EXE201 kế thừa).</summary>
    Task<TeamDetailResponse> AddMemberAsync(Guid teamId, AdminAddMemberRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>
    /// W16 – Admin bớt thành viên khỏi nhóm (mọi lúc, kể cả nhóm EXE201 kế thừa – không qua yêu cầu đổi nhóm): sinh viên thành chưa có nhóm.
    /// Bớt người cuối cùng thì nhóm bị giải tán và quan tâm đề tài đang mở đóng với lý do TEAM_DISBANDED (rule 19).
    /// </summary>
    Task<TeamDetailResponse> RemoveMemberAsync(Guid teamId, Guid studentId, AdminRemoveMemberRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>W21 – Admin chuyển sinh viên sang nhóm khác cùng học kỳ và môn.</summary>
    Task MoveMemberAsync(Guid teamId, Guid studentId, MoveMemberRequest request, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Module Finalization của Team Service (BE-48, rule 1, 2, 10, 13, 15).</summary>
public sealed class FinalizationService(
    IFinalizationRepository finalization, ITeamRepository teams, IMemberRepository members, TeamFinalizer finalizer, RoundGuard rounds,
    AuditLogWriter audit, ITeamService teamService, ITopicInterestService topicInterests) : IFinalizationService
{
    private const string AdminSource = "ADMIN";
    private const string RoundClosedReason = "Đóng đợt lập nhóm";
    private static readonly string[] OpenStatuses = [TeamStatuses.Draft, TeamStatuses.Active];

    public async Task<FinalizeRoundResult> FinalizeRoundAsync(FinalizeRoundRequest request, CancellationToken ct)
    {
        var open = await finalization.ListTeamsAsync(request.SemesterId, request.CourseCode, OpenStatuses, ct);
        var membersByTeam = await finalization.ListMembersAsync(open.Select(t => t.Id).ToList(), ct);
        var finalized = 0;
        foreach (var team in open)
        {
            var check = await finalizer.CheckAsync(membersByTeam[team.Id], ct);
            if (check.IsValid)
            {
                finalizer.Finalize(team, membersByTeam[team.Id], TeamFinalizer.BySystem, AuditActor.System, RoundClosedReason);
                finalized++;
            }
            else
            {
                finalizer.MarkNeedsReview(team, check.Violations, AuditActor.System, RoundClosedReason);
            }
        }
        await teams.SaveChangesAsync(ct);
        return new FinalizeRoundResult(finalized, open.Count - finalized);
    }

    public async Task<TeamDetailResponse> FinalizeAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var team = await RequireTeamAsync(teamId, ct);
        if (team.Status != TeamStatuses.Finalized)
        {
            var current = await members.ListAsync(teamId, ct);
            var check = await finalizer.CheckAsync(current, ct);
            if (!check.IsValid)
            {
                throw new ConflictException("composition-invalid", "Nhóm chưa đủ điều kiện chốt", string.Join(" ", check.Messages));
            }
            finalizer.Finalize(team, current, TeamFinalizer.ByAdmin, user.ToAuditActor());
            await teams.SaveChangesAsync(ct);
        }
        return await teamService.GetAsync(teamId, user, ct);
    }

    public async Task<TeamDetailResponse> ReopenAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var team = await RequireTeamAsync(teamId, ct);
        if (team.Status == TeamStatuses.Finalized)
        {
            var actor = user.ToAuditActor();
            if (await rounds.PhaseAsync(team.CourseCode, team.SemesterId, ct) == Phases.TeamFormation)
            {
                var count = (await members.ListAsync(teamId, ct)).Count;
                (team.Status, team.FinalizedAt) = (TeamRules.StatusAfterMemberChange(TeamStatuses.Active, count), null);
                audit.Add(team.Id, "Team", team.Id, "TEAM_REOPENED", actor, new { status = TeamStatuses.Finalized }, new { status = team.Status });
            }
            else
            {
                finalizer.MarkNeedsReview(team, [], actor, "Admin mở lại nhóm");
            }
            await teams.SaveChangesAsync(ct);
        }
        return await teamService.GetAsync(teamId, user, ct);
    }

    public async Task<TeamDetailResponse> AddMemberAsync(Guid teamId, AdminAddMemberRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var team = await RequireTeamAsync(teamId, ct);
        var current = await members.ListAsync(teamId, ct);
        if (current.Count >= TeamRules.MaxSize)
        {
            throw TeamErrors.TeamFull();
        }
        var student = await rounds.LookupAsync(team.SemesterId, request.StudentId, ct);
        if (student.CourseCode != team.CourseCode)
        {
            throw TeamErrors.Invalid("studentId", $"Sinh viên học {student.CourseCode}, nhóm thuộc {team.CourseCode}.");
        }
        if (await teams.IsStudentInTeamAsync(team.SemesterId, student.Id, ct))
        {
            throw TeamErrors.AlreadyInTeam();
        }

        var actor = user.ToAuditActor();
        var added = AddMember(team, new TeamMember
        {
            StudentId = student.Id, StudentCode = student.StudentCode, FullName = student.FullName, SpecializationId = student.SpecializationId,
            Role = current.Count == 0 ? MemberRoles.Leader : MemberRoles.Member,
        }, current.Count + 1, actor, request.Reason);
        await finalizer.AfterMembersChangedAsync(team, [.. current, added], actor, ct);
        await teams.SaveChangesAsync(ct);
        return await teamService.GetAsync(teamId, user, ct);
    }

    public async Task<TeamDetailResponse> RemoveMemberAsync(Guid teamId, Guid studentId, AdminRemoveMemberRequest request, ClaimsPrincipal user,
        CancellationToken ct)
    {
        var team = await RequireTeamAsync(teamId, ct);
        var current = await members.ListAsync(team.Id, ct);
        var leaving = current.FirstOrDefault(m => m.StudentId == studentId) ?? throw TeamErrors.MemberNotFound(studentId);

        await finalization.InTransactionAsync(async () =>
        {
            await DetachAsync(team, current, leaving, user.ToAuditActor(), "MEMBER_REMOVED", new { memberCount = current.Count - 1 }, request.Reason,
                "Admin bớt thành viên cuối cùng", ct);
            await teams.SaveChangesAsync(ct);
        }, ct);
        return await teamService.GetAsync(teamId, user, ct);
    }

    public async Task MoveMemberAsync(Guid teamId, Guid studentId, MoveMemberRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        if (request.TargetTeamId == teamId)
        {
            throw TeamErrors.Invalid("targetTeamId", "Nhóm đích trùng nhóm hiện tại.");
        }
        var source = await RequireTeamAsync(teamId, ct);
        var target = await RequireTeamAsync(request.TargetTeamId, ct);
        if (target.SemesterId != source.SemesterId || target.CourseCode != source.CourseCode)
        {
            throw TeamErrors.Invalid("targetTeamId", "Nhóm đích phải cùng học kỳ và cùng môn.");
        }
        var sourceMembers = await members.ListAsync(source.Id, ct);
        var moving = sourceMembers.FirstOrDefault(m => m.StudentId == studentId) ?? throw TeamErrors.MemberNotFound(studentId);
        var targetMembers = await members.ListAsync(target.Id, ct);
        if (targetMembers.Count >= TeamRules.MaxSize)
        {
            throw TeamErrors.TeamFull();
        }

        var actor = user.ToAuditActor();

        // Dòng thành viên không đổi nhóm được (trigger): xóa ở nhóm cũ rồi mới thêm ở nhóm mới, trong một transaction.
        await finalization.InTransactionAsync(async () =>
        {
            await DetachAsync(source, sourceMembers, moving, actor, "MEMBER_MOVED_OUT",
                new { targetTeam = target.Name, memberCount = sourceMembers.Count - 1 }, null, "Thành viên cuối cùng được chuyển sang nhóm khác", ct);
            var added = AddMember(target, new TeamMember
            {
                StudentId = moving.StudentId, StudentCode = moving.StudentCode, FullName = moving.FullName,
                SpecializationId = moving.SpecializationId, Role = MemberRoles.Member,
            }, targetMembers.Count + 1, actor, $"Chuyển từ nhóm {source.Name}");
            await finalizer.AfterMembersChangedAsync(target, [.. targetMembers, added], actor, ct);
            await teams.SaveChangesAsync(ct);
        }, ct);
    }

    /// <summary>
    /// Xóa dòng thành viên do Admin (bớt hoặc chuyển đi): nhật ký <paramref name="action"/>, MemberLeft (reason ADMIN), trạng thái nhóm theo
    /// thành viên còn lại hoặc giải tán nếu hết người, rồi chuyển trưởng nhóm cho người vào sớm nhất nếu người rời là trưởng nhóm.
    /// Gọi trong transaction; việc đổi trưởng nhóm do người gọi lưu.
    /// </summary>
    private async Task DetachAsync(TeamEntity team, IReadOnlyList<TeamMember> current, TeamMember leaving, AuditActor actor, string action,
        object newValues, string? reason, string disbandReason, CancellationToken ct)
    {
        var remaining = current.Where(m => m.StudentId != leaving.StudentId).ToList();
        var newLeader = leaving.Role == MemberRoles.Leader ? remaining.MinBy(m => m.JoinedAt) : null;

        members.Remove(leaving);
        audit.Add(team.Id, "TeamMember", leaving.StudentId, action, actor, new { leaving.StudentCode, leaving.Role }, newValues, reason);
        members.AddEvent(MemberEventTypes.MemberLeft, team.Id, new MemberLeft(team.Id, team.SemesterId, leaving.StudentId, AdminSource, remaining.Count));
        if (remaining.Count == 0)
        {
            audit.Add(team.Id, "Team", team.Id, "TEAM_DISBANDED", actor, new { status = team.Status }, new { status = TeamStatuses.Disbanded }, disbandReason);
            (team.Status, team.FinalizedAt) = (TeamStatuses.Disbanded, null);
            await topicInterests.CloseInterestsOnTeamDisbandedAsync(team.Id, ct);
        }
        else
        {
            await finalizer.AfterMembersChangedAsync(team, remaining, actor, ct);
        }
        await teams.SaveChangesAsync(ct);

        if (newLeader is not null)
        {
            newLeader.Role = MemberRoles.Leader;
            audit.Add(team.Id, "TeamMember", newLeader.StudentId, "LEADER_CHANGED", actor,
                new { leader = leaving.StudentCode }, new { leader = newLeader.StudentCode });
            members.AddEvent(MemberEventTypes.LeaderChanged, team.Id, new LeaderChanged(team.Id, leaving.StudentId, newLeader.StudentId));
        }
    }

    /// <summary>Thêm dòng thành viên do Admin (join_source ADMIN), ghi nhật ký và MemberJoined; trạng thái nhóm do người gọi xử lý.</summary>
    private TeamMember AddMember(TeamEntity team, TeamMember member, int memberCount, AuditActor actor, string? reason)
    {
        (member.TeamId, member.SemesterId, member.JoinSource) = (team.Id, team.SemesterId, AdminSource);
        members.Add(member);
        audit.Add(team.Id, "TeamMember", member.StudentId, "MEMBER_ADDED", actor,
            newValues: new { member.StudentCode, source = AdminSource, memberCount }, reason: reason);
        members.AddEvent(MemberEventTypes.MemberJoined, team.Id,
            new MemberJoined(team.Id, team.SemesterId, member.StudentId, member.FullName, member.SpecializationId, AdminSource, memberCount));
        return member;
    }

    private async Task<TeamEntity> RequireTeamAsync(Guid teamId, CancellationToken ct)
    {
        var team = await teams.GetAsync(teamId, ct);
        return team is null || team.Status == TeamStatuses.Disbanded ? throw TeamErrors.TeamNotFound(teamId) : team;
    }
}
