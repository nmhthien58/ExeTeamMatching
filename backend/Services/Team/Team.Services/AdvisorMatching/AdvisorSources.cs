using Team.Repositories.AdvisorMatching;
using Team.Repositories.AuditLog;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Matching;
using Team.Services.Teams;
using Team.Services.TopicInterests;

namespace Team.Services.AdvisorMatching;

/// <summary>Cố vấn đang hoạt động. <see cref="MaxTeams"/> null = không giới hạn; <see cref="PreferredTeams"/> 0 = không đặt (chỉ mentor).</summary>
public sealed record AdvisorCandidate(Guid Id, string FullName, IReadOnlyList<CatalogRef> Domains, int? MaxTeams, int PreferredTeams);

/// <summary>
/// Phần riêng của từng loại cố vấn trong luồng ghép chung (<see cref="AdvisorMatchingService"/>): danh sách cố vấn, tải hiện tại,
/// đầu vào của nhóm và cách ghi kết quả. Ghép mentor chỉ cần một lớp mới cài interface này khi Academic có API mentor (BE-66).
/// </summary>
public interface IAdvisorSource
{
    /// <summary>LECTURER hoặc MENTOR.</summary>
    string Kind { get; }

    /// <summary>Báo lỗi ngay (trước khi mở transaction) khi loại cố vấn này chưa có nguồn dữ liệu.</summary>
    void EnsureAvailable();

    Task<IReadOnlyList<AdvisorCandidate>> ListActiveAsync(string courseCode, CancellationToken ct);

    /// <summary>Số nhóm đang có của từng cố vấn trong môn và học kỳ của lần ghép.</summary>
    Task<Dictionary<Guid, int>> CountLoadsAsync(Guid semesterId, string courseCode, CancellationToken ct);

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

    public void EnsureAvailable()
    {
    }

    public async Task<IReadOnlyList<AdvisorCandidate>> ListActiveAsync(string courseCode, CancellationToken ct)
    {
        IReadOnlyList<LecturerSnapshot> lecturers;
        try
        {
            lecturers = await academic.ListActiveLecturersAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw AdvisorMatchingErrors.AcademicUnavailable();
        }
        return lecturers.Where(l => l.IsActive).Select(l => ToCandidate(l, courseCode)).OrderBy(l => l.FullName).ThenBy(l => l.Id).ToList();
    }

    public Task<Dictionary<Guid, int>> CountLoadsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        repo.CountLecturerLoadsAsync(semesterId, courseCode, ct);

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

    /// <summary>Số nhóm tối đa theo môn (rule 9): <c>MaxTeamsExe101</c> / <c>MaxTeamsExe201</c>, null = không giới hạn.</summary>
    public static int? MaxTeamsFor(LecturerSnapshot lecturer, string courseCode) =>
        courseCode == "EXE201" ? lecturer.MaxTeamsExe201 : lecturer.MaxTeamsExe101;

    private static AdvisorCandidate ToCandidate(LecturerSnapshot l, string courseCode) =>
        new(l.Id, l.FullName, l.Domains.Select(d => new CatalogRef(d.Id, d.Code, d.Name)).ToList(), MaxTeamsFor(l, courseCode), 0);
}

/// <summary>
/// Mentor (rule 20) – chưa ghép được: Academic chưa có API danh sách mentor kèm số tối đa và số mong muốn (BE-66 của Nam).
/// Mọi thao tác trả 501 <c>mentor-matching-unavailable</c>, không giả dữ liệu.
/// </summary>
public sealed class MentorAdvisorSource : IAdvisorSource
{
    public string Kind => AdvisorKinds.Mentor;

    public void EnsureAvailable() => throw AdvisorMatchingErrors.MentorSourceUnavailable();

    public Task<IReadOnlyList<AdvisorCandidate>> ListActiveAsync(string courseCode, CancellationToken ct) =>
        throw AdvisorMatchingErrors.MentorSourceUnavailable();

    public Task<Dictionary<Guid, int>> CountLoadsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        throw AdvisorMatchingErrors.MentorSourceUnavailable();

    public Task<IReadOnlyList<AdvisorMatchingTeam>> BuildTeamsAsync(IReadOnlyList<TeamEntity> teams, CancellationToken ct) =>
        throw AdvisorMatchingErrors.MentorSourceUnavailable();

    public void StageAutoAssignment(TeamEntity team, AdvisorCandidate advisor, AdvisorMatchingPlanItem item, Guid runId,
        IReadOnlyList<Guid> memberStudentIds, AuditActor actor) =>
        throw AdvisorMatchingErrors.MentorSourceUnavailable();
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

/// <summary>Payload <c>AdvisorMatchingCompleted</c> (key = matchingRunId).</summary>
public sealed record AdvisorMatchingCompletedEvent(
    Guid MatchingRunId, string AdvisorKind, string CourseCode, string Trigger, int MatchedCount, int UnmatchedCount);
