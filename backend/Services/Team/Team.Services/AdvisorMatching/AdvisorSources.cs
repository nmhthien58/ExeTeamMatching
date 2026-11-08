using Team.Repositories.AdvisorMatching;
using Team.Repositories.AuditLog;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Matching;
using Team.Services.Teams;
using Team.Services.TopicInterests;

namespace Team.Services.AdvisorMatching;

/// <summary>
/// Cố vấn đang hoạt động. <see cref="MaxTeams"/> null = không giới hạn; <see cref="PreferredTeams"/> 0 = không đặt (chỉ mentor).
/// <see cref="Organization"/>, <see cref="IsInternal"/> chỉ có ở mentor. <see cref="LecturerId"/> là tài khoản giảng viên của cố vấn:
/// giảng viên – chính <see cref="Id"/>; mentor nội bộ – giảng viên liên kết; mentor ngoài – null.
/// </summary>
public sealed record AdvisorCandidate(
    Guid Id, string FullName, IReadOnlyList<CatalogRef> Domains, int? MaxTeams, int PreferredTeams,
    string? Organization = null, bool IsInternal = false, Guid? LecturerId = null)
{
    /// <summary>Cố vấn trùng người bị chặn ở nhóm (<paramref name="blocked"/> từ <see cref="IAdvisorSource.ListBlockedLecturersAsync"/>) – không được ghép (rule 20).</summary>
    public bool IsBlockedAt(Guid teamId, IReadOnlyDictionary<Guid, Guid> blocked) =>
        LecturerId is { } lecturerId && blocked.TryGetValue(teamId, out var blockedId) && blockedId == lecturerId;
}

/// <summary>Cố vấn hiện tại của một nhóm (bảng W19 / W28).</summary>
public sealed record CurrentAdvisor(Guid AdvisorId, string AdvisorName, string Reason, int? PreferenceRank);

/// <summary>
/// Phần riêng của từng loại cố vấn trong luồng ghép chung (<see cref="AdvisorMatchingService"/>): danh sách cố vấn, tải hiện tại,
/// đầu vào của nhóm và cách ghi kết quả.
/// </summary>
public interface IAdvisorSource
{
    /// <summary>LECTURER hoặc MENTOR.</summary>
    string Kind { get; }

    /// <summary>
    /// Phạm vi môn của khóa <c>pg_advisory_xact_lock</c>: giảng viên theo môn; mentor dùng chung một khóa cho cả học kỳ vì
    /// số nhóm tối đa của mentor tính chung hai môn.
    /// </summary>
    string LockScope(string courseCode);

    Task<IReadOnlyList<AdvisorCandidate>> ListActiveAsync(string courseCode, CancellationToken ct);

    /// <summary>Số nhóm đang có của từng cố vấn trong học kỳ của lần ghép (giảng viên theo môn, mentor chung hai môn).</summary>
    Task<Dictionary<Guid, int>> CountLoadsAsync(Guid semesterId, string courseCode, CancellationToken ct);

    /// <summary>Cố vấn hiện tại của các nhóm.</summary>
    Task<Dictionary<Guid, CurrentAdvisor>> ListCurrentAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct);

    /// <summary>
    /// Giảng viên mà cố vấn loại này không được trùng ở từng nhóm (teamId → lecturerId): mentor – giảng viên hướng dẫn đang gán của nhóm
    /// (rule 20: một người không vừa hướng dẫn vừa mentor cùng một nhóm, so theo <see cref="AdvisorCandidate.LecturerId"/>);
    /// giảng viên – <c>lecturerId</c> của mentor đang gán, nhóm có mentor ngoài thì không có dòng.
    /// </summary>
    Task<Dictionary<Guid, Guid>> ListBlockedLecturersAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct);

    /// <summary>Đầu vào PlanAdvisorMatching của các nhóm chưa có cố vấn (domain, nguyện vọng, quan tâm OPEN, giảng viên EXE101).</summary>
    Task<IReadOnlyList<AdvisorMatchingTeam>> BuildTeamsAsync(IReadOnlyList<TeamEntity> teams, CancellationToken ct);

    /// <summary>Thêm assignment AUTO, nhật ký và sự kiện ghép (chưa lưu). Người gọi đóng quan tâm và lưu trong cùng transaction.</summary>
    void StageAutoAssignment(TeamEntity team, AdvisorCandidate advisor, AdvisorMatchingPlanItem item, Guid runId, IReadOnlyList<Guid> memberStudentIds,
        AuditActor actor);
}

/// <summary>Giảng viên hướng dẫn (rule 9): danh sách từ Academic <c>GET /api/lecturers</c>, dữ liệu ghép từ team_db.</summary>
public sealed class LecturerAdvisorSource(IAcademicClient academic, IAdvisorMatchingRepository repo, LecturerAssignmentWriter writer) : IAdvisorSource
{
    public string Kind => AdvisorKinds.Lecturer;

    public string LockScope(string courseCode) => courseCode;

    public async Task<IReadOnlyList<AdvisorCandidate>> ListActiveAsync(string courseCode, CancellationToken ct)
    {
        var lecturers = await AcademicCall.RunAsync(academic.ListActiveLecturersAsync, ct);
        return lecturers
            .Where(l => l.IsActive && l.IsProfileCompleted)
            .Select(l => ToCandidate(l, courseCode))
            .OrderBy(l => l.FullName)
            .ThenBy(l => l.Id)
            .ToList();
    }

    public Task<Dictionary<Guid, int>> CountLoadsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        repo.CountLecturerLoadsAsync(semesterId, courseCode, ct);

    public async Task<Dictionary<Guid, CurrentAdvisor>> ListCurrentAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct) =>
        (await repo.ListActiveLecturerAssignmentsAsync(teamIds, ct))
        .ToDictionary(x => x.Key, x => new CurrentAdvisor(x.Value.LecturerId, x.Value.LecturerName, x.Value.Reason, x.Value.PreferenceRank));

    /// <summary>
    /// Chỉ gọi Academic khi có nhóm đang có mentor. Dùng danh sách (lấy cả mentor đang khóa, lỗi → 503 <c>academic-unavailable</c>) thay cho
    /// <c>GetMentorAsync</c> vốn trả null khi Academic lỗi – nếu không, kiểm tra rule 20 sẽ bị bỏ qua âm thầm.
    /// </summary>
    public async Task<Dictionary<Guid, Guid>> ListBlockedLecturersAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct)
    {
        var assigned = teamIds.Count == 0 ? [] : await repo.ListActiveMentorAssignmentsAsync(teamIds, ct);
        if (assigned.Count == 0)
        {
            return [];
        }
        var lecturerByMentor = (await AcademicCall.RunAsync(academic.ListMentorsAsync, ct))
            .Where(m => m.LecturerId is not null).ToDictionary(m => m.Id, m => m.LecturerId!.Value);
        return assigned.Where(x => lecturerByMentor.ContainsKey(x.Value.MentorId)).ToDictionary(x => x.Key, x => lecturerByMentor[x.Value.MentorId]);
    }

    public async Task<IReadOnlyList<AdvisorMatchingTeam>> BuildTeamsAsync(IReadOnlyList<TeamEntity> teams, CancellationToken ct)
    {
        var ids = teams.Select(t => t.Id).ToList();
        var domains = await repo.ListDomainIdsAsync(ids, ct);
        var preferences = await repo.ListLecturerPreferencesAsync(ids, ct);
        var interests = await repo.ListInterestsAsync(Kind, ids, openOnly: true, ct);
        var previous = await repo.ListPreviousInstructorsAsync(teams, ct);
        return teams.Select(t => new AdvisorMatchingTeam(
                t.Id,
                domains[t.Id].ToList(),
                preferences[t.Id].OrderBy(p => p.Rank).Select(p => p.LecturerId).ToList(),
                interests[t.Id].Select(i => i.AdvisorId).Distinct().ToList(),
                previous.TryGetValue(t.Id, out var p) ? p.LecturerId : null))
            .ToList();
    }

    public void StageAutoAssignment(TeamEntity team, AdvisorCandidate advisor, AdvisorMatchingPlanItem item, Guid runId,
        IReadOnlyList<Guid> memberStudentIds, AuditActor actor) =>
        writer.Stage(team, advisor.Id, advisor.FullName, item.Reason!, item.PreferenceRank, LecturerAssignmentWriter.SourceAuto, runId, null, null,
            previous: null, memberStudentIds, actor);

    /// <summary>Số nhóm tối đa theo môn (rule 9, BE-93): ưu tiên DesiredTeams do giảng viên khai báo, sau đó mới dùng MaxTeams.</summary>
    public static int? MaxTeamsFor(LecturerSnapshot lecturer, string courseCode) =>
        courseCode == "EXE201"
            ? (lecturer.DesiredTeamsExe201 ?? lecturer.MaxTeamsExe201)
            : (lecturer.DesiredTeamsExe101 ?? lecturer.MaxTeamsExe101);

    public static AdvisorCandidate ToCandidate(LecturerSnapshot l, string courseCode) =>
        new(l.Id, l.FullName, l.Domains.Select(d => new CatalogRef(d.Id, d.Code, d.Name)).ToList(), MaxTeamsFor(l, courseCode), 0, LecturerId: l.Id);
}

/// <summary>
/// Mentor (rule 20): danh sách từ Academic <c>GET /api/mentors</c> (API trả cả mentor đang khóa nên lọc <c>isActive</c> ở đây),
/// số nhóm tối đa và số mong muốn của từng mentor từ Academic, số nhóm đang mentor đếm từ <c>mentor_assignments</c> (chung hai môn
/// trong học kỳ). Nhóm không có nguyện vọng mentor, chỉ có quan tâm OPEN của mentor và domain (sinh viên không chọn mentor).
/// </summary>
public sealed class MentorAdvisorSource(IAcademicClient academic, IAdvisorMatchingRepository repo, MentorAssignmentWriter writer) : IAdvisorSource
{
    /// <summary>Khóa chung của ghép mentor trong học kỳ, không theo môn.</summary>
    public const string AllCourses = "ALL";

    public string Kind => AdvisorKinds.Mentor;

    public string LockScope(string courseCode) => AllCourses;

    public async Task<IReadOnlyList<AdvisorCandidate>> ListActiveAsync(string courseCode, CancellationToken ct)
    {
        var mentors = await AcademicCall.RunAsync(academic.ListMentorsAsync, ct);
        return mentors.Where(m => m.IsActive).Select(ToCandidate).OrderBy(m => m.FullName).ThenBy(m => m.Id).ToList();
    }

    public Task<Dictionary<Guid, int>> CountLoadsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        repo.CountMentorLoadsAsync(semesterId, ct);

    public async Task<Dictionary<Guid, CurrentAdvisor>> ListCurrentAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct) =>
        (await repo.ListActiveMentorAssignmentsAsync(teamIds, ct))
        .ToDictionary(x => x.Key, x => new CurrentAdvisor(x.Value.MentorId, x.Value.MentorName, x.Value.Reason, null));

    public async Task<Dictionary<Guid, Guid>> ListBlockedLecturersAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct) =>
        teamIds.Count == 0
            ? []
            : (await repo.ListActiveLecturerAssignmentsAsync(teamIds, ct)).ToDictionary(x => x.Key, x => x.Value.LecturerId);

    public async Task<IReadOnlyList<AdvisorMatchingTeam>> BuildTeamsAsync(IReadOnlyList<TeamEntity> teams, CancellationToken ct)
    {
        var ids = teams.Select(t => t.Id).ToList();
        var domains = await repo.ListDomainIdsAsync(ids, ct);
        var interests = await repo.ListInterestsAsync(Kind, ids, openOnly: true, ct);
        return teams.Select(t => new AdvisorMatchingTeam(
                t.Id, domains[t.Id].ToList(), [], interests[t.Id].Select(i => i.AdvisorId).Distinct().ToList(), null))
            .ToList();
    }

    public void StageAutoAssignment(TeamEntity team, AdvisorCandidate advisor, AdvisorMatchingPlanItem item, Guid runId,
        IReadOnlyList<Guid> memberStudentIds, AuditActor actor) =>
        writer.Stage(team, advisor, item.Reason!, MentorAssignmentWriter.SourceAuto, runId, null, null, previous: null, memberStudentIds, actor);

    public static AdvisorCandidate ToCandidate(MentorSnapshot m) =>
        new(m.Id, m.FullName, m.Domains.Select(d => new CatalogRef(d.Id, d.Code, d.Name)).ToList(), m.MaxTeams, m.PreferredTeams ?? 0,
            m.Organization, m.IsInternal, m.LecturerId);
}

/// <summary>Gọi Academic lấy danh sách cố vấn: lỗi mạng / quá hạn → 503 <c>academic-unavailable</c>.</summary>
internal static class AcademicCall
{
    public static async Task<IReadOnlyList<T>> RunAsync<T>(Func<CancellationToken, Task<IReadOnlyList<T>>> call, CancellationToken ct)
    {
        try
        {
            return await call(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw AdvisorMatchingErrors.AcademicUnavailable();
        }
    }
}

/// <summary>
/// Ghi mentor của nhóm: dòng <c>mentor_assignments</c>, nhật ký nhóm và sự kiện theo events.md – dùng cho ghép tự động, đổi tay và gỡ.
/// Một mentor mỗi nhóm (<c>uq_mentor_assignments_active</c>): người gọi kết thúc dòng cũ và lưu trước khi thêm dòng mới.
/// Thay mentor chỉ phát <c>MentorAssigned</c> có <c>replacedMentorId</c> (Collaboration đổi người trong workspace);
/// gỡ hẳn mới phát <c>MentorUnassigned</c>.
/// </summary>
public sealed class MentorAssignmentWriter(IAdvisorMatchingRepository repo, AuditLogWriter audit, TimeProvider clock)
{
    public const string SourceAuto = "AUTO";
    public const string SourceManual = "MANUAL";
    public const string ReasonManual = "MANUAL";
    public const string AuditAction = "MENTOR_ASSIGNED";
    public const string AuditRemoveAction = "MENTOR_UNASSIGNED";

    public MentorAssignment Stage(TeamEntity team, AdvisorCandidate mentor, string reason, string source, Guid? runId,
        Guid? assignedByAccountId, string? note, MentorAssignment? previous, IReadOnlyList<Guid> memberStudentIds, AuditActor actor)
    {
        var assignment = new MentorAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            MentorId = mentor.Id,
            MentorName = mentor.FullName,
            Status = "ASSIGNED",
            Reason = reason,
            Source = source,
            MatchingRunId = runId,
            AssignedByAccountId = assignedByAccountId,
            AssignedAt = clock.GetUtcNow(),
            Note = note,
        };
        repo.Add(assignment);
        audit.Add(team.Id, "MentorAssignment", assignment.Id, AuditAction, actor,
            previous is null ? null : new { mentorId = previous.MentorId, mentorName = previous.MentorName, reason = previous.Reason },
            new { mentorId = mentor.Id, mentorName = mentor.FullName, reason, source, matchingRunId = runId },
            note);
        repo.AddEvent(AdvisorMatchingEventTypes.MentorAssigned, "Team", team.Id, new MentorAssignedEvent(
            assignment.Id, team.Id, mentor.Id, mentor.FullName, mentor.LecturerId, reason, source, runId, previous?.MentorId, memberStudentIds));
        return assignment;
    }

    /// <summary>Kết thúc dòng đang gán (ENDED). <paramref name="unassigned"/> = gỡ hẳn: ghi nhật ký và phát <c>MentorUnassigned</c>.</summary>
    public void End(MentorAssignment assignment, string endReason, bool unassigned, AuditActor actor)
    {
        (assignment.Status, assignment.EndedAt, assignment.EndReason) = ("ENDED", clock.GetUtcNow(), endReason);
        if (!unassigned)
        {
            return;
        }
        audit.Add(assignment.TeamId, "MentorAssignment", assignment.Id, AuditRemoveAction, actor,
            new { mentorId = assignment.MentorId, mentorName = assignment.MentorName, reason = assignment.Reason }, null, endReason);
        repo.AddEvent(AdvisorMatchingEventTypes.MentorUnassigned, "Team", assignment.TeamId,
            new MentorUnassignedEvent(assignment.Id, assignment.TeamId, assignment.MentorId, endReason));
    }
}

/// <summary>Ghi giảng viên hướng dẫn: dòng <c>lecturer_assignments</c>, nhật ký nhóm và <c>LecturerAssigned</c> (events.md) – dùng cho ghép tự động và đổi tay.</summary>
public sealed class LecturerAssignmentWriter(IAdvisorMatchingRepository repo, AuditLogWriter audit, TimeProvider clock)
{
    public const string SourceAuto = "AUTO";
    public const string SourceManual = "MANUAL";
    public const string ReasonManual = "MANUAL";
    public const string AuditAction = "LECTURER_ASSIGNED";

    public LecturerAssignment Stage(TeamEntity team, Guid lecturerId, string lecturerName, string reason, int? preferenceRank, string source,
        Guid? runId, Guid? assignedByAccountId, string? note, LecturerAssignment? previous, IReadOnlyList<Guid> memberStudentIds, AuditActor actor)
    {
        // ck_lecturer_assignments_rank: chỉ MUTUAL / PREFERENCE có nguyện vọng thứ mấy.
        var rank = reason is "MUTUAL" or "PREFERENCE" ? preferenceRank : null;
        var assignment = new LecturerAssignment
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            LecturerId = lecturerId,
            LecturerName = lecturerName,
            Status = "ASSIGNED",
            Reason = reason,
            PreferenceRank = rank is { } r ? (short)r : null,
            Source = source,
            MatchingRunId = runId,
            AssignedByAccountId = assignedByAccountId,
            AssignedAt = clock.GetUtcNow(),
            Note = note,
        };
        repo.Add(assignment);
        audit.Add(team.Id, "LecturerAssignment", assignment.Id, AuditAction, actor,
            previous is null ? null : new { lecturerId = previous.LecturerId, lecturerName = previous.LecturerName, reason = previous.Reason },
            new { lecturerId, lecturerName, reason, preferenceRank = rank, source, matchingRunId = runId },
            note);
        repo.AddEvent(AdvisorMatchingEventTypes.LecturerAssigned, "Team", team.Id, new LecturerAssignedEvent(
            assignment.Id, team.Id, lecturerId, lecturerName, reason, rank, source, runId, memberStudentIds, previous?.LecturerId));
        return assignment;
    }
}

/// <summary>Payload <c>LecturerAssigned</c> (docs/contracts/events.md, topic exe.assignment, key = teamId).</summary>
public sealed record LecturerAssignedEvent(
    Guid AssignmentId, Guid TeamId, Guid LecturerId, string LecturerName, string Reason, int? PreferenceRank, string Source, Guid? MatchingRunId,
    IReadOnlyList<Guid> MemberStudentIds, Guid? PreviousLecturerId);

/// <summary>Payload <c>MentorAssigned</c> (events.md, topic exe.assignment, key = teamId).</summary>
public sealed record MentorAssignedEvent(
    Guid AssignmentId, Guid TeamId, Guid MentorId, string MentorName, Guid? MentorLecturerId, string Reason, string Source, Guid? MatchingRunId,
    Guid? ReplacedMentorId, IReadOnlyList<Guid> MemberStudentIds);

/// <summary>Payload <c>MentorUnassigned</c> (key = teamId).</summary>
public sealed record MentorUnassignedEvent(Guid AssignmentId, Guid TeamId, Guid MentorId, string Reason);

/// <summary>Payload <c>AdvisorMatchingCompleted</c> (key = matchingRunId).</summary>
public sealed record AdvisorMatchingCompletedEvent(
    Guid MatchingRunId, string AdvisorKind, string CourseCode, string Trigger, int MatchedCount, int UnmatchedCount);
