using Jobs.Host.Clients;

namespace Jobs.Host.Jobs;

/// <summary>
/// LecturerMatchingJob (BE-81, rule 9): chạy tiếp sau <see cref="AutoAssignUnmatchedJob"/> của đợt lập nhóm (continuation Hangfire
/// do <see cref="RoundAutoCloseJob.FinalizeAsync"/> tạo), gọi Team <c>POST /internal/advisor-matching/LECTURER</c> để ghép giảng viên
/// hướng dẫn cho mọi nhóm chưa có. Team gọi lại không tạo trùng (trả lần SCHEDULED đã áp dụng) nên Hangfire thử lại an toàn.
/// Lỗi HTTP (gồm 503 <c>matching-unavailable</c>, <c>academic-unavailable</c>) được ném ra để Hangfire thử lại.
/// </summary>
public sealed class LecturerMatchingJob(ITeamClient team, ILogger<LecturerMatchingJob> logger)
{
    public async Task RunAsync(Guid semesterId, string courseCode, CancellationToken ct)
    {
        var run = await team.RunAdvisorMatchingAsync(AdvisorKinds.Lecturer, semesterId, courseCode, ct);
        logger.LogInformation("Ghép giảng viên {Course} (học kỳ {SemesterId}): lần chạy {RunId} {Status}, ghép được {Matched} nhóm, chưa ghép được {Unmatched} nhóm",
            courseCode, semesterId, run.Id, run.Status, run.MatchedCount, run.UnmatchedCount);
        if (run.UnmatchedCount > 0)
        {
            logger.LogWarning("Ghép giảng viên {Course}: {Unmatched} nhóm chưa có giảng viên, Admin xử lý ở W19", courseCode, run.UnmatchedCount);
        }
    }
}
