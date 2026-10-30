using Academic.Repositories.Semesters;
using Academic.Services.Caching;
using BuildingBlocks.Errors;

namespace Academic.Services.Semesters;

public interface ISemesterService
{
    Task<IReadOnlyList<SemesterResponse>> ListAsync(CancellationToken ct);

    /// <summary>Học kỳ hiện hành (cache Redis); 404 khi chưa có.</summary>
    Task<SemesterResponse> GetCurrentAsync(CancellationToken ct);

    Task<SemesterResponse> CreateAsync(SemesterInput input, CancellationToken ct);

    Task<SemesterResponse> UpdateAsync(Guid id, SemesterInput input, CancellationToken ct);
}

/// <summary>Học kỳ (BE-16, W03, rule 6). Dữ liệu đã qua <see cref="SemesterInputValidator"/>.</summary>
public sealed class SemesterService(ISemesterRepository semesters, AcademicCache cache) : ISemesterService
{
    public async Task<IReadOnlyList<SemesterResponse>> ListAsync(CancellationToken ct) =>
        (await semesters.ListAsync(ct)).Select(SemesterResponse.From).ToList();

    public async Task<SemesterResponse> GetCurrentAsync(CancellationToken ct) =>
        await cache.GetOrLoadAsync(AcademicCacheKeys.CurrentSemester, async token =>
            await semesters.GetCurrentAsync(token) is { } s ? SemesterResponse.From(s) : null, ct)
        ?? throw new NotFoundException("học kỳ hiện hành");

    public async Task<SemesterResponse> CreateAsync(SemesterInput input, CancellationToken ct)
    {
        await EnsureCodeFreeAsync(input.Code!, null, ct);
        var semester = new Semester();
        Apply(semester, input);
        semesters.Add(semester);
        await semesters.SaveAsync(semester, ct);
        await cache.InvalidateAsync(AcademicCacheKeys.CurrentSemester, ct);
        await cache.InvalidateAsync(AcademicCacheKeys.CurrentPhaseSource, ct);
        return SemesterResponse.From(semester);
    }

    public async Task<SemesterResponse> UpdateAsync(Guid id, SemesterInput input, CancellationToken ct)
    {
        var semester = await semesters.FindAsync(id, ct) ?? throw new NotFoundException("học kỳ", id);
        await EnsureCodeFreeAsync(input.Code!, id, ct);
        Apply(semester, input);
        await semesters.SaveAsync(semester, ct);
        await cache.InvalidateAsync(AcademicCacheKeys.CurrentSemester, ct);
        await cache.InvalidateAsync(AcademicCacheKeys.CurrentPhaseSource, ct);
        return SemesterResponse.From(semester);
    }

    private async Task EnsureCodeFreeAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        if (await semesters.CodeExistsAsync(code, exceptId, ct))
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["code"] = [$"Học kỳ {code} đã tồn tại."] });
        }
    }

    private static void Apply(Semester s, SemesterInput input)
    {
        s.Code = input.Code!;
        s.Name = input.Name!.Trim();
        s.StartDate = input.StartDate!.Value;
        s.EndDate = input.EndDate!.Value;
        s.IsCurrent = input.IsCurrent ?? s.IsCurrent;
        s.MentoringStartWeek = (short)(input.MentoringStartWeek ?? s.MentoringStartWeek);
        s.ExpectedMentorSessions = (short)(input.ExpectedMentorSessions ?? s.ExpectedMentorSessions);
        s.SlotBookingDeadlineWeek = (short)(input.SlotBookingDeadlineWeek ?? s.SlotBookingDeadlineWeek);
        s.MeetingSlotMinutes = (short)(input.MeetingSlotMinutes ?? s.MeetingSlotMinutes);
        s.CheckpointTargetWeeks = input.CheckpointTargetWeeks?.Select(w => (short)w).ToArray();
    }
}
