using Academic.Repositories.Rounds;
using Academic.Repositories.Semesters;
using Academic.Services.Caching;
using Academic.Services.Rounds;
using BuildingBlocks.Errors;

namespace Academic.Services.Semesters;

/// <summary>Schema <c>CoursePhase</c>.</summary>
public sealed record CoursePhaseResponse(
    string CourseCode, string Phase, DateTimeOffset? TeamFormationDeadlineAt, bool ChangeRequestRoundOpen, DateOnly MentoringStartsOn);

/// <summary>Giá trị <c>phase</c> (PROJECT_CONTEXT §1.1).</summary>
public static class CoursePhases
{
    public const string TeamFormation = "TEAM_FORMATION";
    public const string SemesterRunning = "SEMESTER_RUNNING";
}

/// <summary>Dữ liệu cache để tính giai đoạn: học kỳ hiện hành và mọi đợt của nó. Giai đoạn tính lúc đọc vì phụ thuộc giờ hiện tại.</summary>
public sealed record PhaseSource(SemesterResponse Semester, IReadOnlyList<RoundResponse> Rounds);

public interface ICoursePhaseService
{
    /// <summary>Giai đoạn của EXE101 và EXE201 trong học kỳ hiện hành; 404 khi chưa có học kỳ hiện hành.</summary>
    Task<IReadOnlyList<CoursePhaseResponse>> GetCurrentAsync(CancellationToken ct);
}

/// <summary>
/// Giai đoạn môn (BE-67, rule 18, PROJECT_CONTEXT §1.1): client đổi tab Khám phá ↔ Dự án theo giai đoạn.
/// Học kỳ và danh sách đợt cache Redis (xóa khi sửa học kỳ hoặc đợt); giai đoạn tính lại mỗi lần đọc.
/// </summary>
public sealed class CoursePhaseService(ISemesterRepository semesters, IRoundRepository rounds, AcademicCache cache, TimeProvider clock) : ICoursePhaseService
{
    public async Task<IReadOnlyList<CoursePhaseResponse>> GetCurrentAsync(CancellationToken ct)
    {
        var source = await cache.GetOrLoadAsync(AcademicCacheKeys.CurrentPhaseSource, LoadAsync, ct)
            ?? throw new NotFoundException("học kỳ hiện hành");
        return [CoursePhaseRules.Compute(CourseCodes.Exe101, source, clock.GetUtcNow()), CoursePhaseRules.Compute(CourseCodes.Exe201, source, clock.GetUtcNow())];
    }

    private async Task<PhaseSource?> LoadAsync(CancellationToken ct)
    {
        if (await semesters.GetCurrentAsync(ct) is not { } semester)
        {
            return null;
        }
        var list = await rounds.ListAsync(semester.Id, null, null, ct);
        return new PhaseSource(SemesterResponse.From(semester), list.Select(RoundResponse.From).ToList());
    }
}

public static class CoursePhaseRules
{
    /// <summary>
    /// Rule 18: "học kỳ đã bắt đầu" = đợt lập nhóm của môn đã đóng. Đợt đã quá hạn chót nhưng Job chưa kịp khóa cũng tính là đã đóng
    /// (như <c>RoundService.GetOpenAsync</c>). Còn đợt lập nhóm mở, hoặc chưa có đợt nào đóng (đầu học kỳ) → <c>TEAM_FORMATION</c>.
    /// <c>teamFormationDeadlineAt</c>: hạn chót của đợt đang mở, nếu không có thì của đợt mới nhất; null khi chưa tạo đợt.
    /// </summary>
    public static CoursePhaseResponse Compute(string courseCode, PhaseSource source, DateTimeOffset now)
    {
        var formation = source.Rounds.Where(r => r.CourseCode == courseCode && r.RoundType == RoundTypes.TeamFormation).ToList();
        var open = formation.Where(r => r.Status == RoundStatuses.Open && r.DeadlineAt > now).MaxBy(r => r.OpensAt);
        var closed = formation.Any(r => r.Status == RoundStatuses.Closed || (r.Status == RoundStatuses.Open && r.DeadlineAt <= now));
        var phase = open is null && closed ? CoursePhases.SemesterRunning : CoursePhases.TeamFormation;
        var deadline = (open ?? formation.MaxBy(r => r.OpensAt))?.DeadlineAt;

        var changeRequestOpen = courseCode == CourseCodes.Exe201 && source.Rounds.Any(r => r.CourseCode == courseCode
            && r.RoundType == RoundTypes.ChangeRequest && r.Status == RoundStatuses.Open && r.DeadlineAt > now);
        var semester = source.Semester;
        return new CoursePhaseResponse(courseCode, phase, deadline, changeRequestOpen,
            semester.StartDate.AddDays((semester.MentoringStartWeek - 1) * 7));
    }
}
