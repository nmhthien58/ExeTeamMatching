using BuildingBlocks.Messaging;
using Hangfire;
using Jobs.Host.Jobs;

namespace Jobs.Host.Events;

/// <summary>
/// Nhận <c>RoundOpened</c> (exe.registration) và hẹn <see cref="RoundAutoCloseJob"/> đúng hạn chót (BE-53).
/// Hạn đã qua (sự kiện tới muộn) thì chạy ngay. Các bước sau đều idempotent nên nhận trùng sự kiện không gây sai.
/// </summary>
public sealed class RoundOpenedHandler(IBackgroundJobClient jobs, TimeProvider clock, ILogger<RoundOpenedHandler> logger) : IIntegrationEventHandler
{
    public Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var round = envelope.GetPayload<RoundOpened>();
        var delay = round.DeadlineAt - clock.GetUtcNow();
        var jobId = jobs.Schedule<RoundAutoCloseJob>(
            j => j.CloseAsync(round.RoundId, round.SemesterId, round.CourseCode, round.RoundType, CancellationToken.None),
            delay > TimeSpan.Zero ? delay : TimeSpan.Zero);
        logger.LogInformation("Hẹn đóng đợt {RoundId} ({Course}) lúc {Deadline}, job {JobId}", round.RoundId, round.CourseCode, round.DeadlineAt, jobId);
        return Task.CompletedTask;
    }
}
