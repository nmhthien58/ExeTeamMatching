using BuildingBlocks.Messaging;
using Jobs.Host.Clients;
using Jobs.Host.Events;

namespace Jobs.Host.Jobs;

/// <summary>
/// DeadlineReminderJob (BE-52): 08:00 hằng ngày khi đợt lập nhóm đang mở; ở mốc 72 giờ và 24 giờ trước hạn phát
/// <c>DeadlineApproaching</c> cho sinh viên chưa có nhóm và nhóm dưới 4 người.
/// </summary>
public sealed class DeadlineReminderJob(
    IAcademicClient academic,
    ITeamClient team,
    IEventPublisher publisher,
    TimeProvider clock,
    ILogger<DeadlineReminderJob> logger)
{
    public static readonly IReadOnlyList<string> CourseCodes = ["EXE101", "EXE201"];

    public async Task RunAsync(CancellationToken ct)
    {
        foreach (var courseCode in CourseCodes)
        {
            var round = await academic.GetOpenRoundAsync(courseCode, RoundTypes.TeamFormation, ct);
            if (round is null || Milestone(round.DeadlineAt - clock.GetUtcNow()) is not { } hoursLeft)
            {
                continue;
            }

            var targets = await team.GetDeadlineReminderTargetsAsync(round.SemesterId, courseCode, ct);
            if (targets.StudentIdsWithoutTeam.Count == 0 && targets.TeamIdsUnderMinSize.Count == 0)
            {
                logger.LogInformation("Đợt {RoundId} còn {Hours} giờ, không còn ai cần nhắc", round.Id, hoursLeft);
                continue;
            }

            await publisher.PublishAsync(Topics.Registration, JobEventTypes.DeadlineApproaching, round.Id, new DeadlineApproaching(
                round.Id, round.SemesterId, courseCode, round.DeadlineAt, hoursLeft, targets.StudentIdsWithoutTeam, targets.TeamIdsUnderMinSize), ct);
            logger.LogInformation("Nhắc hạn {Hours} giờ cho đợt {RoundId}: {Students} sinh viên, {Teams} nhóm",
                hoursLeft, round.Id, targets.StudentIdsWithoutTeam.Count, targets.TeamIdsUnderMinSize.Count);
        }
    }

    /// <summary>
    /// Mốc nhắc cho lần chạy hằng ngày: còn (48, 72] giờ → 72, còn (0, 24] giờ → 24, còn lại không nhắc.
    /// Hai lần chạy cách nhau 24 giờ nên mỗi mốc gửi đúng một lần.
    /// </summary>
    public static int? Milestone(TimeSpan timeLeft) => timeLeft.TotalHours switch
    {
        > 48 and <= 72 => 72,
        > 0 and <= 24 => 24,
        _ => null,
    };
}
