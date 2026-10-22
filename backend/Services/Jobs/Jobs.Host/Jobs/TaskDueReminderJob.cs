using BuildingBlocks.Messaging;
using Jobs.Host.Clients;
using Jobs.Host.Events;

namespace Jobs.Host.Jobs;

/// <summary>
/// TaskDueReminderJob (BE-72, rule 23): 08:00 hằng ngày hỏi Collaboration việc có hạn trong 24 giờ và đã trễ:
/// <list type="bullet">
/// <item>Việc sắp tới hạn → <c>TaskDueSoon</c> cho người làm; bài nộp sắp tới hạn mà chưa nộp → cho cả nhóm.</item>
/// <item>Bài nộp trễ hạn chưa nộp → <c>DeliverableOverdue</c> cho nhóm và giảng viên, chỉ trong 24 giờ đầu sau hạn
/// (lần chạy hằng ngày nên mỗi bài nộp được báo trễ một lần).</item>
/// </list>
/// </summary>
public sealed class TaskDueReminderJob(ICollabClient collab, IEventPublisher publisher, TimeProvider clock, ILogger<TaskDueReminderJob> logger)
{
    public const int WindowHours = 24;
    public const string Deliverable = "DELIVERABLE";
    public const string NotSubmitted = "NOT_SUBMITTED";

    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var dueSoon = 0;
        var overdue = 0;
        foreach (var task in await collab.GetDueTasksAsync(WindowHours, includeOverdue: true, ct))
        {
            if (task.DueAt is not { } dueAt)
            {
                continue;
            }

            var isDeliverable = task.Kind == Deliverable;
            var notSubmitted = task.SubmissionStatus == NotSubmitted;
            if (dueAt < now)
            {
                if (isDeliverable && notSubmitted && now - dueAt <= TimeSpan.FromHours(WindowHours))
                {
                    await publisher.PublishAsync(Topics.Reminder, JobEventTypes.DeliverableOverdue, task.WorkspaceId, new DeliverableOverdue(
                        task.WorkspaceId, task.Id, task.Title, dueAt, task.MemberStudentIds ?? [], task.LecturerId), ct);
                    overdue++;
                }
                continue;
            }

            var recipients = isDeliverable ? (notSubmitted ? task.MemberStudentIds : null) : task.AssigneeStudentIds;
            if (recipients is { Count: > 0 })
            {
                await publisher.PublishAsync(Topics.Reminder, JobEventTypes.TaskDueSoon, task.WorkspaceId,
                    new TaskDueSoon(task.WorkspaceId, task.Id, task.Title, dueAt, recipients), ct);
                dueSoon++;
            }
        }
        logger.LogInformation("Nhắc hạn việc: {DueSoon} sắp tới hạn, {Overdue} bài nộp trễ", dueSoon, overdue);
    }
}
