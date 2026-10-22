using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Collab.Repositories.Context;
using Collab.Repositories.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Collab.Repositories.Tasks;

/// <summary>Bộ lọc bảng việc (C04); lọc trễ hạn làm ở Services vì bài nộp tính trễ theo lần nộp đầu.</summary>
public sealed record TaskFilter(string? Status, string? Kind, Guid? AssigneeStudentId);

/// <summary>Việc kèm dữ liệu phụ để vẽ thẻ: người làm, số bình luận, bài nộp mới nhất và thời điểm nộp lần đầu.</summary>
public sealed record TaskRow(
    TaskItem Task, IReadOnlyList<Guid> AssigneeIds, int CommentCount, DeliverableSubmission? LatestSubmission, DateTimeOffset? FirstSubmittedAt);

/// <summary>Việc kèm workspace (danh sách gom nhiều nhóm: nhắc hạn, tổng quan của cố vấn).</summary>
public sealed record WorkspaceTaskRow(TaskRow Row, Workspace Workspace);

public interface ITaskRepository
{
    Task<List<TaskRow>> ListAsync(Guid workspaceId, TaskFilter filter, CancellationToken ct);

    Task<TaskItem?> GetAsync(Guid taskId, CancellationToken ct);

    Task<TaskRow?> GetRowAsync(Guid taskId, CancellationToken ct);

    Task<List<TaskAssignee>> ListAssigneesAsync(Guid taskId, CancellationToken ct);

    /// <summary>Việc của một cột, theo thứ tự hiển thị – để chèn / dời vị trí.</summary>
    Task<List<TaskItem>> ListColumnAsync(Guid workspaceId, string status, CancellationToken ct);

    void Add(TaskItem task, IEnumerable<Guid> assigneeIds);

    void SetAssignees(Guid taskId, IReadOnlyCollection<TaskAssignee> current, IEnumerable<Guid> assigneeIds);

    void Remove(TaskItem task);

    Task<List<TaskComment>> ListCommentsAsync(Guid taskId, CancellationToken ct);

    void AddComment(TaskComment comment);

    Task<List<DeliverableSubmission>> ListSubmissionsAsync(Guid taskId, CancellationToken ct);

    Task<DeliverableSubmission?> GetSubmissionAsync(Guid submissionId, CancellationToken ct);

    void AddSubmission(DeliverableSubmission submission);

    /// <summary>Việc chưa xong có hạn trong <paramref name="until"/> (và đã trễ nếu <paramref name="includeOverdue"/>), mọi workspace.</summary>
    Task<List<WorkspaceTaskRow>> ListDueAsync(DateTimeOffset now, DateTimeOffset until, bool includeOverdue, CancellationToken ct);

    /// <summary>Bài nộp đã quá hạn mà nhóm chưa nộp lần nào, của các workspace cho trước ("Trễ hạn nộp" ở L01).</summary>
    Task<List<WorkspaceTaskRow>> ListOverdueDeliverablesAsync(IReadOnlyCollection<Guid> workspaceIds, DateTimeOffset now, CancellationToken ct);

    /// <summary>Ghi sự kiện exe.collab vào outbox, cùng transaction với <see cref="SaveChangesAsync"/>.</summary>
    void AddEvent<TPayload>(string eventType, Guid workspaceId, TPayload payload);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class TaskRepository(CollabDbContext db) : ITaskRepository
{
    public async Task<List<TaskRow>> ListAsync(Guid workspaceId, TaskFilter filter, CancellationToken ct)
    {
        var query = db.Set<TaskItem>().AsNoTracking().Where(t => t.WorkspaceId == workspaceId);
        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }
        if (filter.Kind is { } kind)
        {
            query = query.Where(t => t.Kind == kind);
        }
        if (filter.AssigneeStudentId is { } studentId)
        {
            query = query.Where(t => db.Set<TaskAssignee>().Any(a => a.TaskId == t.Id && a.StudentId == studentId));
        }

        var tasks = await query.OrderBy(t => t.Status).ThenBy(t => t.Position).ThenBy(t => t.CreatedAt).ToListAsync(ct);
        return await ToRowsAsync(tasks, ct);
    }

    public Task<TaskItem?> GetAsync(Guid taskId, CancellationToken ct) =>
        db.Set<TaskItem>().FirstOrDefaultAsync(t => t.Id == taskId, ct);

    public async Task<TaskRow?> GetRowAsync(Guid taskId, CancellationToken ct)
    {
        var task = await db.Set<TaskItem>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct);
        return task is null ? null : (await ToRowsAsync([task], ct))[0];
    }

    public Task<List<TaskAssignee>> ListAssigneesAsync(Guid taskId, CancellationToken ct) =>
        db.Set<TaskAssignee>().Where(a => a.TaskId == taskId).ToListAsync(ct);

    public Task<List<TaskItem>> ListColumnAsync(Guid workspaceId, string status, CancellationToken ct) =>
        db.Set<TaskItem>()
            .Where(t => t.WorkspaceId == workspaceId && t.Status == status)
            .OrderBy(t => t.Position).ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public void Add(TaskItem task, IEnumerable<Guid> assigneeIds)
    {
        db.Set<TaskItem>().Add(task);
        db.Set<TaskAssignee>().AddRange(assigneeIds.Distinct().Select(id => new TaskAssignee { TaskId = task.Id, StudentId = id }));
    }

    public void SetAssignees(Guid taskId, IReadOnlyCollection<TaskAssignee> current, IEnumerable<Guid> assigneeIds)
    {
        var wanted = assigneeIds.ToHashSet();
        db.Set<TaskAssignee>().RemoveRange(current.Where(a => !wanted.Contains(a.StudentId)));
        db.Set<TaskAssignee>().AddRange(wanted
            .Where(id => current.All(a => a.StudentId != id))
            .Select(id => new TaskAssignee { TaskId = taskId, StudentId = id }));
    }

    public void Remove(TaskItem task) => db.Set<TaskItem>().Remove(task);

    public Task<List<TaskComment>> ListCommentsAsync(Guid taskId, CancellationToken ct) =>
        db.Set<TaskComment>().AsNoTracking().Where(c => c.TaskId == taskId).OrderBy(c => c.CreatedAt).ToListAsync(ct);

    public void AddComment(TaskComment comment) => db.Set<TaskComment>().Add(comment);

    public Task<List<DeliverableSubmission>> ListSubmissionsAsync(Guid taskId, CancellationToken ct) =>
        db.Set<DeliverableSubmission>().AsNoTracking().Where(s => s.TaskId == taskId).OrderByDescending(s => s.SubmittedAt).ToListAsync(ct);

    public Task<DeliverableSubmission?> GetSubmissionAsync(Guid submissionId, CancellationToken ct) =>
        db.Set<DeliverableSubmission>().FirstOrDefaultAsync(s => s.Id == submissionId, ct);

    public void AddSubmission(DeliverableSubmission submission) => db.Set<DeliverableSubmission>().Add(submission);

    public async Task<List<WorkspaceTaskRow>> ListDueAsync(DateTimeOffset now, DateTimeOffset until, bool includeOverdue, CancellationToken ct)
    {
        var tasks = await db.Set<TaskItem>().AsNoTracking()
            .Where(t => t.Status != TaskStatuses.Done && t.DueAt != null && t.DueAt <= until && (includeOverdue || t.DueAt >= now))
            .OrderBy(t => t.DueAt)
            .ToListAsync(ct);
        return await WithWorkspacesAsync(await ToRowsAsync(tasks, ct), ct);
    }

    public async Task<List<WorkspaceTaskRow>> ListOverdueDeliverablesAsync(IReadOnlyCollection<Guid> workspaceIds, DateTimeOffset now, CancellationToken ct)
    {
        var tasks = await db.Set<TaskItem>().AsNoTracking()
            .Where(t => workspaceIds.Contains(t.WorkspaceId) && t.Kind == TaskKinds.Deliverable && t.Status != TaskStatuses.Done
                && t.DueAt < now
                && !db.Set<DeliverableSubmission>().Any(s => s.TaskId == t.Id))
            .OrderBy(t => t.DueAt)
            .ToListAsync(ct);
        return await WithWorkspacesAsync(await ToRowsAsync(tasks, ct), ct);
    }

    public void AddEvent<TPayload>(string eventType, Guid workspaceId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Collab, eventType, "Workspace", workspaceId, payload);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private async Task<List<TaskRow>> ToRowsAsync(List<TaskItem> tasks, CancellationToken ct)
    {
        var ids = tasks.Select(t => t.Id).ToList();
        var assignees = await db.Set<TaskAssignee>().AsNoTracking().Where(a => ids.Contains(a.TaskId)).ToListAsync(ct);
        var comments = await db.Set<TaskComment>().AsNoTracking()
            .Where(c => ids.Contains(c.TaskId))
            .GroupBy(c => c.TaskId)
            .Select(g => new { TaskId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TaskId, x => x.Count, ct);
        var submissions = await db.Set<DeliverableSubmission>().AsNoTracking()
            .Where(s => ids.Contains(s.TaskId))
            .ToListAsync(ct);
        var byTask = submissions.GroupBy(s => s.TaskId).ToDictionary(g => g.Key, g => g.ToList());

        return tasks.Select(t => new TaskRow(
            t,
            assignees.Where(a => a.TaskId == t.Id).Select(a => a.StudentId).ToList(),
            comments.GetValueOrDefault(t.Id),
            byTask.GetValueOrDefault(t.Id)?.MaxBy(s => s.SubmittedAt),
            byTask.GetValueOrDefault(t.Id)?.Min(s => s.SubmittedAt))).ToList();
    }

    private async Task<List<WorkspaceTaskRow>> WithWorkspacesAsync(List<TaskRow> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Task.WorkspaceId).Distinct().ToList();
        var workspaces = await db.Set<Workspace>().AsNoTracking().Where(w => ids.Contains(w.Id)).ToDictionaryAsync(w => w.Id, ct);
        return rows.Select(r => new WorkspaceTaskRow(r, workspaces[r.Task.WorkspaceId])).ToList();
    }
}

/// <summary>Giá trị cột <c>task_items.kind</c>.</summary>
public static class TaskKinds
{
    public const string Task = "TASK";
    public const string Deliverable = "DELIVERABLE";
}

/// <summary>Giá trị cột <c>task_items.status</c>.</summary>
public static class TaskStatuses
{
    public const string ToDo = "TODO";
    public const string Doing = "DOING";
    public const string Done = "DONE";

    public static readonly IReadOnlyList<string> All = [ToDo, Doing, Done];
}

/// <summary>Giá trị cột <c>deliverable_submissions.review_status</c>.</summary>
public static class SubmissionStatuses
{
    public const string Pending = "PENDING";
    public const string Accepted = "ACCEPTED";
    public const string NeedsRevision = "NEEDS_REVISION";
}
