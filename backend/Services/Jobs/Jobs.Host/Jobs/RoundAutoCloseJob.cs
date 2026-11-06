using Hangfire;
using Jobs.Host.Clients;

namespace Jobs.Host.Jobs;

/// <summary>
/// RoundAutoCloseJob (BE-53, rule 6, 13): delayed job lên lịch đúng hạn chót khi nhận <c>RoundOpened</c>.
/// Mỗi bước là một job riêng để Hangfire thử lại từng bước: khóa đợt (Academic phát RoundClosed) → chốt nhóm
/// (Team finalize-round) → ghép sinh viên còn lại (<see cref="AutoAssignUnmatchedJob"/>) → ghép giảng viên hướng dẫn
/// (<see cref="LecturerMatchingJob"/>, continuation chỉ chạy khi ghép sinh viên thành công – rule 9). Đợt đổi nhóm chỉ khóa.
/// </summary>
public sealed class RoundAutoCloseJob(
    IAcademicClient academic,
    ITeamClient team,
    IBackgroundJobClient jobs,
    ILogger<RoundAutoCloseJob> logger)
{
    public async Task CloseAsync(Guid roundId, Guid semesterId, string courseCode, string roundType, CancellationToken ct)
    {
        var round = await academic.CloseRoundAsync(roundId, ct);
        logger.LogInformation("Đã khóa đợt {RoundId} ({Course}, {RoundType}), trạng thái {Status}", roundId, courseCode, roundType, round.Status);
        if (roundType == RoundTypes.TeamFormation)
        {
            jobs.Enqueue<RoundAutoCloseJob>(j => j.FinalizeAsync(roundId, semesterId, courseCode, CancellationToken.None));
        }
    }

    public async Task FinalizeAsync(Guid roundId, Guid semesterId, string courseCode, CancellationToken ct)
    {
        var result = await team.FinalizeRoundAsync(roundId, semesterId, courseCode, ct);
        logger.LogInformation("Chốt đợt {RoundId}: {Finalized} nhóm đã chốt, {NeedsReview} nhóm cần xem lại",
            roundId, result.FinalizedCount, result.NeedsReviewCount);
        var autoAssignId = jobs.Enqueue<AutoAssignUnmatchedJob>(j => j.RunAsync(semesterId, courseCode, CancellationToken.None));
        jobs.ContinueJobWith<LecturerMatchingJob>(autoAssignId, j => j.RunAsync(semesterId, courseCode, CancellationToken.None));
    }
}
