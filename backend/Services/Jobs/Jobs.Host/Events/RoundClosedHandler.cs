using BuildingBlocks.Messaging;
using Hangfire;
using Jobs.Host.Clients;
using Jobs.Host.Jobs;

namespace Jobs.Host.Events;

/// <summary>
/// Nhận <c>RoundClosed</c> (exe.registration). Đợt lập nhóm bị Bộ môn kết thúc sớm (khóa trước hạn chót) thì chạy ngay chuỗi
/// chốt nhóm → ghép sinh viên còn lại → ghép giảng viên (<see cref="RoundAutoCloseJob.FinalizeAsync"/>), không đợi job hẹn giờ.
/// Khóa đúng hạn do <see cref="RoundAutoCloseJob.CloseAsync"/> tự chạy chuỗi nên bỏ qua ở đây. Giờ khóa lấy theo lúc Academic tạo sự kiện.
/// </summary>
public sealed class RoundClosedHandler(IBackgroundJobClient jobs, ILogger<RoundClosedHandler> logger) : IIntegrationEventHandler
{
    public Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var round = envelope.GetPayload<RoundClosed>();
        if (round.RoundType != RoundTypes.TeamFormation || !RoundAutoCloseJob.ClosedEarly(envelope.OccurredAt, round.DeadlineAt))
        {
            return Task.CompletedTask;
        }

        var jobId = jobs.Enqueue<RoundAutoCloseJob>(j => j.FinalizeAsync(round.RoundId, round.SemesterId, round.CourseCode, CancellationToken.None));
        logger.LogInformation("Đợt {RoundId} ({Course}) kết thúc sớm, chạy chốt nhóm và ghép tự động, job {JobId}", round.RoundId, round.CourseCode, jobId);
        return Task.CompletedTask;
    }
}
