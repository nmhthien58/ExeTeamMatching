using System.Security.Claims;
using BuildingBlocks.Errors;
using Team.Repositories.AuditLog;
using Team.Repositories.AutoAssign;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Finalization;
using Team.Services.Matching;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Services.AutoAssign;

public interface IAutoAssignService
{
    /// <summary>AutoAssignUnmatchedJob – áp dụng phương án của Matching. Gọi lại không thêm trùng: sinh viên đã có nhóm được bỏ qua.</summary>
    Task<AutoAssignResultResponse> ApplyAsync(AutoAssignApplyRequest request, CancellationToken ct);

    /// <summary>W20 – Admin chạy lại ghép tự động: hỏi Matching.PlanAutoAssignment rồi áp dụng ngay.</summary>
    Task<AutoAssignResultResponse> RunAsync(AutoAssignRunRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>W21 – kết quả ghép gần nhất; 404 nếu môn trong học kỳ chưa ghép lần nào.</summary>
    Task<AutoAssignResultResponse> GetResultAsync(AutoAssignResultQuery query, CancellationToken ct);
}

/// <summary>
/// Module AutoAssign của Team Service (BE-49, rule 14): bù sinh viên chưa có nhóm vào nhóm Cần xem lại, tạo nhóm mới cho số còn lại.
/// Nhóm hợp lệ sau khi ghép được chốt luôn; nhóm chưa hợp lệ ở lại Cần xem lại cho Admin sửa tay (W21, W22).
/// </summary>
public sealed class AutoAssignService(
    IAutoAssignRepository repository, ITeamRepository teams, IMemberRepository members, IMatchingClient matching, IAcademicClient academic,
    TeamFinalizer finalizer, UnassignedStudents unassigned, TeamReader reader, AuditLogWriter audit, TimeProvider clock) : IAutoAssignService
{
    private const string JoinSource = AutoAssignRepository.JoinSource;
    private const string Reason = "Ghép tự động";

    private sealed record Applied(IReadOnlyList<Guid> FilledTeamIds, IReadOnlyList<Guid> NewTeamIds, IReadOnlyList<Guid> SkippedStudentIds);

    public async Task<AutoAssignResultResponse> ApplyAsync(AutoAssignApplyRequest request, CancellationToken ct)
    {
        var applied = await ApplyCoreAsync(request.SemesterId, request.CourseCode, request.Fills ?? [], request.NewTeams ?? [], AuditActor.System, ct);
        return await ToResponseAsync(request.SemesterId, clock.GetUtcNow(), applied.FilledTeamIds, applied.NewTeamIds, applied.SkippedStudentIds, ct);
    }

    public async Task<AutoAssignResultResponse> RunAsync(AutoAssignRunRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var plan = await matching.PlanAutoAssignmentAsync(request.SemesterId, request.CourseCode, ct);
        var applied = await ApplyCoreAsync(request.SemesterId, request.CourseCode,
            plan.Fills.Select(f => new AutoAssignFill(f.TeamId, f.StudentIds)).ToList(),
            plan.NewTeams.Select(t => new AutoAssignNewTeam(t.Name, t.LeaderStudentId, t.StudentIds)).ToList(),
            user.ToAuditActor(), ct);
        return await ToResponseAsync(request.SemesterId, clock.GetUtcNow(), applied.FilledTeamIds, applied.NewTeamIds,
            plan.UnassignedStudentIds.Concat(applied.SkippedStudentIds).Distinct().ToList(), ct);
    }

    public async Task<AutoAssignResultResponse> GetResultAsync(AutoAssignResultQuery query, CancellationToken ct)
    {
        var snapshot = await repository.GetSnapshotAsync(query.SemesterId, query.CourseCode, ct)
            ?? throw new NotFoundException("kết quả ghép tự động");
        var left = new List<Guid>();
        foreach (var course in query.CourseCode is { } courseCode ? [courseCode] : CourseCodes.All)
        {
            left.AddRange(await unassigned.ListAsync(query.SemesterId, course, ct));
        }
        return await ToResponseAsync(query.SemesterId, snapshot.RunAt, snapshot.FilledTeamIds, snapshot.NewTeamIds, left, ct);
    }

    private async Task<Applied> ApplyCoreAsync(
        Guid semesterId, string courseCode, IReadOnlyList<AutoAssignFill> fills, IReadOnlyList<AutoAssignNewTeam> newTeams, AuditActor actor,
        CancellationToken ct)
    {
        var requested = fills.SelectMany(f => f.StudentIds).Concat(newTeams.SelectMany(t => t.StudentIds)).Distinct().ToList();
        var inTeam = await repository.StudentsInTeamAsync(semesterId, requested, ct);
        var free = requested.Where(id => !inTeam.Contains(id)).ToList();
        var snapshots = (free.Count == 0 ? [] : await academic.LookupStudentsAsync(semesterId, free, ct))
            .Where(s => s.IsEnrolled && s.CourseCode == courseCode)
            .ToDictionary(s => s.Id);
        var placed = new HashSet<Guid>();
        List<StudentSnapshot> Take(IEnumerable<Guid> ids, int max) =>
            ids.Where(id => snapshots.ContainsKey(id) && !placed.Contains(id)).Take(max).Select(id => { placed.Add(id); return snapshots[id]; }).ToList();

        var maxSize = (await academic.GetTeamRulesAsync(ct)).MaxSize;
        var filled = new List<Guid>();
        foreach (var fill in fills)
        {
            var team = await teams.GetAsync(fill.TeamId, ct);
            if (team is null || team.SemesterId != semesterId || team.CourseCode != courseCode
                || team.Status is TeamStatuses.Disbanded or TeamStatuses.Finalized)
            {
                continue;
            }
            var current = await members.ListAsync(team.Id, ct);
            var students = Take(fill.StudentIds, maxSize - current.Count);
            if (students.Count == 0)
            {
                continue;
            }
            var joined = students.Select((s, i) => Join(team, s, MemberRoles.Member, current.Count + i + 1, actor)).ToList();
            await SettleAsync(team, [.. current, .. joined], actor, ct);
            filled.Add(team.Id);
        }

        var created = new List<Guid>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in newTeams)
        {
            var students = Take(plan.StudentIds, maxSize);
            if (students.Count == 0)
            {
                continue;
            }
            var leader = students.FirstOrDefault(s => s.Id == plan.LeaderStudentId) ?? students[0];
            var team = new TeamEntity
            {
                Id = Guid.NewGuid(), SemesterId = semesterId, CourseCode = courseCode, IsAutoAssigned = true,
                Name = await UniqueNameAsync(semesterId, courseCode, plan.Name.Trim(), names, ct),
            };
            teams.Add(team);
            audit.Add(team.Id, "Team", team.Id, "TEAM_CREATED", actor, newValues: new { team.Name, isAutoAssigned = true }, reason: Reason);
            teams.AddEvent(TeamEventTypes.TeamCreated, team.Id, new TeamCreated(team.Id, semesterId, courseCode, team.Name, leader.Id, leader.FullName,
                ProjectId: null, IsAutoAssigned: true, IsInherited: false, SourceTeamId: null));
            // Trưởng nhóm vào nhóm qua TeamCreated (như khi sinh viên tự tạo nhóm), các thành viên khác qua MemberJoined.
            var joined = new List<TeamMember> { Join(team, leader, MemberRoles.Leader, 1, actor, publish: false) };
            joined.AddRange(students.Where(s => s.Id != leader.Id).Select((s, i) => Join(team, s, MemberRoles.Member, i + 2, actor)));
            await SettleAsync(team, joined, actor, ct);
            created.Add(team.Id);
        }

        await teams.SaveChangesAsync(ct);
        return new Applied(filled, created, free.Where(id => !placed.Contains(id)).ToList());
    }

    private TeamMember Join(TeamEntity team, StudentSnapshot student, string role, int memberCount, AuditActor actor, bool publish = true)
    {
        var member = new TeamMember
        {
            TeamId = team.Id, SemesterId = team.SemesterId, StudentId = student.Id, StudentCode = student.StudentCode, FullName = student.FullName,
            MajorId = student.MajorId, SpecializationId = student.SpecializationId, Role = role, JoinSource = JoinSource,
        };
        members.Add(member);
        audit.Add(team.Id, "TeamMember", student.Id, "MEMBER_JOINED", actor,
            newValues: new { student.StudentCode, source = JoinSource, memberCount }, reason: Reason);
        if (publish)
        {
            members.AddEvent(MemberEventTypes.MemberJoined, team.Id,
                new MemberJoined(team.Id, team.SemesterId, student.Id, student.FullName, student.SpecializationId, JoinSource, memberCount, MajorId: student.MajorId));
        }
        return member;
    }

    /// <summary>Nhóm hợp lệ sau khi ghép → Đã chốt; chưa hợp lệ → Cần xem lại (rule 14).</summary>
    private async Task SettleAsync(TeamEntity team, IReadOnlyCollection<TeamMember> teamMembers, AuditActor actor, CancellationToken ct)
    {
        var check = await finalizer.CheckAsync(teamMembers, ct);
        if (check.IsValid)
        {
            finalizer.Finalize(team, teamMembers, TeamFinalizer.BySystem, actor, Reason);
        }
        else if (team.Status != TeamStatuses.NeedsReview)
        {
            finalizer.MarkNeedsReview(team, check.Violations, actor, Reason);
        }
    }

    /// <summary>Tên gợi ý của Matching (ví dụ "Auto-FA26-01") có thể đã có nhóm dùng; thêm hậu tố cho tới khi không trùng.</summary>
    private async Task<string> UniqueNameAsync(Guid semesterId, string courseCode, string name, HashSet<string> used, CancellationToken ct)
    {
        var candidate = name;
        for (var i = 2; used.Contains(candidate) || await teams.IsNameTakenAsync(semesterId, courseCode, candidate, null, ct); i++)
        {
            candidate = $"{name} ({i})";
        }
        used.Add(candidate);
        return candidate;
    }

    private async Task<AutoAssignResultResponse> ToResponseAsync(
        Guid semesterId, DateTimeOffset runAt, IReadOnlyList<Guid> filledTeamIds, IReadOnlyList<Guid> newTeamIds, IReadOnlyList<Guid> unassignedIds,
        CancellationToken ct)
    {
        var catalogs = await reader.LoadCatalogsAsync(ct);
        var cards = (await teams.ListGraphsAsync([.. filledTeamIds, .. newTeamIds], ct)).ToDictionary(g => g.Team.Id, g => TeamMapper.ToCard(g, catalogs));
        var students = await reader.StudentsAsync(semesterId, unassignedIds, catalogs, ct);
        return new AutoAssignResultResponse(runAt,
            filledTeamIds.Where(cards.ContainsKey).Select(id => cards[id]).ToList(),
            newTeamIds.Where(cards.ContainsKey).Select(id => cards[id]).ToList(),
            unassignedIds.Select(id => students[id]).ToList());
    }
}
