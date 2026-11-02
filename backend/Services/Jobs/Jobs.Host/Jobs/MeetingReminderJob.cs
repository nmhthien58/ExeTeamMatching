using BuildingBlocks.Messaging;
using Jobs.Host.Clients;
using Jobs.Host.Events;

namespace Jobs.Host.Jobs;

/// <summary>
/// MeetingReminderJob (BE-72, rule 25): mỗi 15 phút hỏi Collaboration họp đã chốt sắp bắt đầu, phát <c>MeetingReminder</c>
/// ở mốc 24 giờ và 1 giờ rồi đánh dấu mốc đã gửi – mỗi mốc gửi một lần cho mỗi họp. Họp chốt sát giờ (đã ở trong mốc nhỏ hơn)
/// chỉ được nhắc ở mốc gần nhất; mốc lớn hơn vẫn đánh dấu đã gửi để không nhắc trùng.
/// </summary>
public sealed class MeetingReminderJob(ICollabClient collab, IEventPublisher publisher, TimeProvider clock, ILogger<MeetingReminderJob> logger)
{
    public static readonly IReadOnlyList<int> OffsetsMinutes = [1440, 60];

    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        foreach (var offset in OffsetsMinutes)
        {
            foreach (var meeting in await collab.GetUpcomingMeetingsAsync(offset, ct))
            {
                if (meeting.StartsAt is not { } startsAt)
                {
                    continue;
                }

                if (OffsetsMinutes.Any(o => o < offset && startsAt - now <= TimeSpan.FromMinutes(o)))
                {
                    await collab.MarkReminderSentAsync(meeting.Id, offset, ct);
                    logger.LogInformation("Bỏ nhắc họp {MeetingId} trước {Offset} phút: họp đã ở mốc nhắc gần hơn", meeting.Id, offset);
                    continue;
                }

                await publisher.PublishAsync(Topics.Reminder, JobEventTypes.MeetingReminder, meeting.WorkspaceId, new MeetingReminder(
                    meeting.WorkspaceId, meeting.Id, meeting.Title, startsAt, meeting.Location, offset, meeting.Invitees ?? []), ct);
                await collab.MarkReminderSentAsync(meeting.Id, offset, ct);
                logger.LogInformation("Nhắc họp {MeetingId} trước {Offset} phút", meeting.Id, offset);
            }
        }
    }
}
