using Jobs.Host.Clients;
using Jobs.Host.Configuration;
using Microsoft.Extensions.Options;

namespace Jobs.Host.Jobs;

/// <summary>
/// MentorMatchingJob (BE-81, rule 20): ghép mentor cho mọi nhóm chưa có ở tuần bắt đầu mentor của học kỳ (mặc định tuần 6), gọi Team
/// <c>POST /internal/advisor-matching/MENTOR</c> cho EXE101 và EXE201 như <see cref="LecturerMatchingJob"/>.
/// <para>
/// Tuần bắt đầu mentor nằm trong cài đặt học kỳ và Admin có thể đổi sau khi lên lịch, nên job không hẹn giờ một lần mà chạy định kỳ
/// (<c>Jobs:MentorMatchingCron</c>, mặc định 07:00 hằng ngày) và đọc lại học kỳ hiện hành mỗi lần: trước ngày bắt đầu tuần mentor hoặc
/// sau khi học kỳ kết thúc thì không làm gì. Team gọi lại trả lần SCHEDULED đã áp dụng (không ghép thêm, không tạo trùng), nên các lần
/// chạy sau ngày bắt đầu và Hangfire thử lại đều an toàn. Nhóm Admin đã "Ghép sớm" được Team bỏ qua.
/// </para>
/// </summary>
public sealed class MentorMatchingJob(
    IAcademicClient academic,
    ITeamClient team,
    IOptions<JobsOptions> options,
    TimeProvider clock,
    ILogger<MentorMatchingJob> logger)
{
    public const string RecurringId = "mentor-matching";

    public const int DefaultStartWeek = 6;

    public static readonly string[] Courses = ["EXE101", "EXE201"];

    /// <summary>Job định kỳ: tới tuần bắt đầu mentor thì ghép cho từng môn; lỗi của một môn không chặn môn kia, ném lại để Hangfire thử lại.</summary>
    public async Task CheckAsync(CancellationToken ct)
    {
        var semester = await academic.GetCurrentSemesterAsync(ct);
        if (semester is null)
        {
            logger.LogInformation("Chưa có học kỳ hiện hành, bỏ qua ghép mentor");
            return;
        }

        var zone = options.Value.ResolveTimeZone();
        var now = clock.GetUtcNow();
        var startsAt = StartsAt(semester, zone);
        if (now < startsAt)
        {
            logger.LogInformation("Học kỳ {Semester}: chưa tới tuần bắt đầu mentor ({StartsAt})", semester.Code, startsAt);
            return;
        }
        if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime) > semester.EndDate)
        {
            return;
        }

        var errors = new List<Exception>();
        foreach (var course in Courses)
        {
            try
            {
                await RunAsync(semester.Id, course, ct);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Ghép mentor {Course} (học kỳ {Semester}) lỗi", course, semester.Code);
                errors.Add(ex);
            }
        }
        if (errors.Count > 0)
        {
            throw errors.Count == 1 ? errors[0] : new AggregateException(errors);
        }
    }

    /// <summary>Ghép mentor một môn (cùng cách <see cref="LecturerMatchingJob.RunAsync"/>). Lỗi HTTP được ném ra để Hangfire thử lại.</summary>
    public async Task RunAsync(Guid semesterId, string courseCode, CancellationToken ct)
    {
        var run = await team.RunAdvisorMatchingAsync(AdvisorKinds.Mentor, semesterId, courseCode, ct);
        logger.LogInformation("Ghép mentor {Course} (học kỳ {SemesterId}): lần chạy {RunId} {Status}, ghép được {Matched} nhóm, chưa ghép được {Unmatched} nhóm",
            courseCode, semesterId, run.Id, run.Status, run.MatchedCount, run.UnmatchedCount);
        if (run.UnmatchedCount > 0)
        {
            logger.LogWarning("Ghép mentor {Course}: {Unmatched} nhóm chưa có mentor, Admin xử lý ở W28", courseCode, run.UnmatchedCount);
        }
    }

    /// <summary>
    /// 00:00 (theo <paramref name="zone"/>) của ngày bắt đầu tuần mentor: <c>startDate + 7·(tuần − 1)</c> ngày – cùng công thức
    /// <c>mentoringStartsOn</c> của Academic và <c>matchingAt</c> của W28.
    /// </summary>
    public static DateTimeOffset StartsAt(SemesterDto semester, TimeZoneInfo zone)
    {
        var day = semester.StartDate.AddDays(7 * ((semester.MentoringStartWeek ?? DefaultStartWeek) - 1)).ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(day, zone.GetUtcOffset(day));
    }
}
