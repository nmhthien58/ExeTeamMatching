using BuildingBlocks.Messaging;
using Microsoft.Extensions.Logging;

namespace Team.Services.AdvisorAssignments;

/// <summary>
/// Consume sự kiện <c>MentorRegistered</c> từ topic <c>exe.student</c> (BE-74).
/// Khi Bộ môn khóa mentor (isActive = false), tự động gỡ mentor khỏi mọi nhóm đang phụ trách và phát MentorUnassigned qua Outbox.
/// </summary>
public sealed class MentorRegisteredHandler(
    IAdvisorAssignmentService service,
    ILogger<MentorRegisteredHandler> logger) : IIntegrationEventHandler
{
    public async Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var payload = envelope.GetPayload<MentorRegisteredEvent>();
        var unassignedCount = await service.HandleMentorRegisteredAsync(payload, cancellationToken);
        if (unassignedCount > 0)
        {
            logger.LogInformation("Gỡ mentor {MentorId} khỏi {Count} nhóm do tài khoản bị khóa.", payload.MentorId, unassignedCount);
        }
    }
}
