using System.Security.Claims;
using BuildingBlocks.Errors;
using Collab.Repositories.Files;
using Collab.Repositories.Tasks;
using Collab.Repositories.Workspaces;
using Collab.Services.Files;
using Collab.Services.Workspaces;

namespace Collab.Services.Tasks;

public interface ITaskService
{
    /// <summary>C04 – bảng việc; <c>IncludeArchive</c> thêm việc đã xong của workspace EXE101 cùng đề tài (BE-77).</summary>
    Task<IReadOnlyList<TaskCardResponse>> ListAsync(Guid workspaceId, TaskQuery query, WorkspaceAccess viewer, CancellationToken ct);

    /// <summary>Thành viên tạo việc; giảng viên hướng dẫn tạo bài nộp (rule 23).</summary>
    Task<TaskDetailResponse> CreateAsync(Guid workspaceId, TaskInput input, WorkspaceAccess writer, CancellationToken ct);

    Task<TaskDetailResponse> GetAsync(Guid taskId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Sửa tên, mô tả, người làm (null = giữ nguyên), hạn (null = bỏ hạn). Không đổi được loại việc.</summary>
    Task<TaskDetailResponse> UpdateAsync(Guid taskId, TaskInput input, ClaimsPrincipal user, CancellationToken ct);

    Task DeleteAsync(Guid taskId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Đổi trạng thái / vị trí (kéo thả). Vị trí bỏ trống = cuối cột.</summary>
    Task<TaskCardResponse> MoveAsync(Guid taskId, MoveTaskRequest request, ClaimsPrincipal user, CancellationToken ct);

    Task<IReadOnlyList<CommentResponse>> ListCommentsAsync(Guid taskId, ClaimsPrincipal user, CancellationToken ct);

    Task<CommentResponse> AddCommentAsync(Guid taskId, AddTaskCommentRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>
    /// Module Meetings (BE-68) gọi khi chuyển dòng biên bản thành việc: người tổ chức họp (trưởng nhóm, giảng viên, mentor)
    /// là người tạo, việc gắn <c>source_meeting_id</c>.
    /// </summary>
    Task<IReadOnlyList<TaskCardResponse>> CreateFromMeetingAsync(Guid workspaceId, Guid meetingId, IReadOnlyList<string> titles,
        WorkspaceAccess organizer, CancellationToken ct);

    /// <summary>TaskDueReminderJob: việc / bài nộp chưa xong có hạn trong <paramref name="withinHours"/> giờ tới (và đã trễ).</summary>
    Task<IReadOnlyList<DueTaskResponse>> ListDueAsync(int withinHours, bool includeOverdue, CancellationToken ct);
}

/// <summary>Module Tasks của Collaboration – bảng việc (BE-70).</summary>
public sealed class TaskService(
    ITaskRepository tasks,
    IWorkspaceRepository workspaces,
    IWorkspaceLookup lookup,
    IWorkspaceReader reader,
    SubmissionReader submissionReader,
    TimeProvider clock) : ITaskService
{
    public const int MaxDueWindowHours = 168;

    public async Task<IReadOnlyList<TaskCardResponse>> ListAsync(Guid workspaceId, TaskQuery query, WorkspaceAccess viewer, CancellationToken ct)
    {
        if (query.Mine && !viewer.IsStudent)
        {
            return []; // "Của tôi" chỉ có nghĩa với sinh viên
        }

        var workspace = await workspaces.GetByIdAsync(workspaceId, ct) ?? throw new NotFoundException("workspace", workspaceId);
        var now = clock.GetUtcNow();
        var filter = new TaskFilter(query.Status, query.Kind, query.Mine ? viewer.PersonId : null);
        var people = new WorkspacePeople(await lookup.ListMembersAsync(workspaceId, ct));
        var cards = (await tasks.ListAsync(workspaceId, filter, ct))
            .Select(r => TaskMapper.ToCard(r, workspace.TeamName, people, now, isArchive: false))
            .ToList();

        // Kho EXE101 chỉ có việc đã xong, không có "của tôi" (rule 26).
        var archiveWanted = query.IncludeArchive && !query.Mine && (query.Status is null || query.Status == TaskStatuses.Done);
        if (archiveWanted && workspace.PreviousWorkspaceId is { } archiveId)
        {
            var archive = await workspaces.GetByIdAsync(archiveId, ct);
            var archivePeople = new WorkspacePeople(await lookup.ListMembersAsync(archiveId, ct));
            cards.AddRange((await tasks.ListAsync(archiveId, filter with { Status = TaskStatuses.Done }, ct))
                .Select(r => TaskMapper.ToCard(r, archive?.TeamName ?? workspace.TeamName, archivePeople, now, isArchive: true)));
        }

        return query.Overdue ? cards.Where(c => c.IsOverdue).ToList() : cards;
    }

    public async Task<TaskDetailResponse> CreateAsync(Guid workspaceId, TaskInput input, WorkspaceAccess writer, CancellationToken ct)
    {
        var workspace = await workspaces.GetByIdAsync(workspaceId, ct) ?? throw new NotFoundException("workspace", workspaceId);
        var kind = input.Kind ?? TaskKinds.Task;
        RequireEditor(kind, writer);

        var people = new WorkspacePeople(await lookup.ListMembersAsync(workspaceId, ct));
        var assignees = ValidateAssignees(input.AssigneeStudentIds ?? [], people);
        var column = await tasks.ListColumnAsync(workspaceId, TaskStatuses.ToDo, ct);
        var now = clock.GetUtcNow();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Kind = kind,
            Title = input.Title.Trim(),
            Description = Normalize(input.Description),
            Status = TaskStatuses.ToDo,
            DueAt = input.DueAt,
            Position = column.Count == 0 ? 0 : column.Max(t => t.Position) + 1,
            CreatedByType = writer.PersonType,
            CreatedById = writer.PersonId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        tasks.Add(task, assignees);
        if (assignees.Count > 0)
        {
            tasks.AddEvent(nameof(TaskAssigned), workspaceId, new TaskAssigned(workspaceId, task.Id, task.Title, task.DueAt, assignees));
        }
        if (kind == TaskKinds.Deliverable)
        {
            tasks.AddEvent(nameof(DeliverableCreated), workspaceId,
                new DeliverableCreated(workspaceId, task.Id, task.Title, task.DueAt!.Value, people.ActiveStudentIds));
        }
        await tasks.SaveChangesAsync(ct);

        return TaskMapper.ToDetail(new TaskRow(task, assignees, 0, null, null), workspace, people, now, isArchive: false, [], []);
    }

    public async Task<TaskDetailResponse> GetAsync(Guid taskId, ClaimsPrincipal user, CancellationToken ct)
    {
        var row = await tasks.GetRowAsync(taskId, ct) ?? throw TaskErrors.TaskNotFound(taskId);
        var read = await reader.RequireReadAsync(row.Task.WorkspaceId, user, ct);
        return await DetailAsync(row, read.IsArchive, ct);
    }

    public async Task<TaskDetailResponse> UpdateAsync(Guid taskId, TaskInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var task = await tasks.GetAsync(taskId, ct) ?? throw TaskErrors.TaskNotFound(taskId);
        var writer = await reader.RequireWriteAsync(task.WorkspaceId, user, ct);
        RequireEditor(task.Kind, writer);
        if (input.Kind is { } kind && kind != task.Kind)
        {
            throw TaskErrors.Invalid("kind", "Không đổi được loại việc.");
        }
        if (task.Kind == TaskKinds.Deliverable && input.DueAt is null)
        {
            throw TaskErrors.Invalid("dueAt", "Bài nộp phải có hạn nộp.");
        }

        task.Title = input.Title.Trim();
        task.Description = Normalize(input.Description);
        task.DueAt = input.DueAt;
        task.UpdatedAt = clock.GetUtcNow();

        if (input.AssigneeStudentIds is { } wanted)
        {
            var people = new WorkspacePeople(await lookup.ListMembersAsync(task.WorkspaceId, ct));
            var assignees = ValidateAssignees(wanted, people);
            var current = await tasks.ListAssigneesAsync(taskId, ct);
            var added = assignees.Where(id => current.All(a => a.StudentId != id)).ToList();
            tasks.SetAssignees(taskId, current, assignees);
            if (added.Count > 0)
            {
                tasks.AddEvent(nameof(TaskAssigned), task.WorkspaceId, new TaskAssigned(task.WorkspaceId, task.Id, task.Title, task.DueAt, added));
            }
        }

        await tasks.SaveChangesAsync(ct);
        return await GetAsync(taskId, user, ct);
    }

    public async Task DeleteAsync(Guid taskId, ClaimsPrincipal user, CancellationToken ct)
    {
        var task = await tasks.GetAsync(taskId, ct) ?? throw TaskErrors.TaskNotFound(taskId);
        var writer = await reader.RequireWriteAsync(task.WorkspaceId, user, ct);
        if (task.Kind == TaskKinds.Deliverable)
        {
            RequireEditor(task.Kind, writer);
            if ((await tasks.ListSubmissionsAsync(taskId, ct)).Count > 0)
            {
                throw TaskErrors.DeliverableHasSubmissions();
            }
        }
        else if (!writer.IsStudent || !(writer.IsLeader || (task.CreatedByType == PersonTypes.Student && task.CreatedById == writer.PersonId)))
        {
            throw TaskErrors.StudentsOnly("Chỉ người tạo việc hoặc trưởng nhóm được xóa việc");
        }

        tasks.Remove(task);
        await tasks.SaveChangesAsync(ct);
    }

    public async Task<TaskCardResponse> MoveAsync(Guid taskId, MoveTaskRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var task = await tasks.GetAsync(taskId, ct) ?? throw TaskErrors.TaskNotFound(taskId);
        var writer = await reader.RequireWriteAsync(task.WorkspaceId, user, ct);
        if (!writer.IsStudent)
        {
            throw TaskErrors.StudentsOnly("Chỉ thành viên nhóm chuyển trạng thái việc");
        }

        var now = clock.GetUtcNow();
        if (task.Status != request.Status)
        {
            Renumber((await tasks.ListColumnAsync(task.WorkspaceId, task.Status, ct)).Where(t => t.Id != task.Id));
            task.Status = request.Status;
            task.CompletedAt = request.Status == TaskStatuses.Done ? now : null;
        }

        var column = (await tasks.ListColumnAsync(task.WorkspaceId, request.Status, ct)).Where(t => t.Id != task.Id).ToList();
        column.Insert(Math.Clamp(request.Position ?? column.Count, 0, column.Count), task);
        Renumber(column);
        task.UpdatedAt = now;
        await tasks.SaveChangesAsync(ct);

        var workspace = await workspaces.GetByIdAsync(task.WorkspaceId, ct);
        var people = new WorkspacePeople(await lookup.ListMembersAsync(task.WorkspaceId, ct));
        var row = await tasks.GetRowAsync(taskId, ct) ?? throw TaskErrors.TaskNotFound(taskId);
        return TaskMapper.ToCard(row, workspace!.TeamName, people, now, isArchive: false);
    }

    public async Task<IReadOnlyList<CommentResponse>> ListCommentsAsync(Guid taskId, ClaimsPrincipal user, CancellationToken ct)
    {
        var task = await tasks.GetAsync(taskId, ct) ?? throw TaskErrors.TaskNotFound(taskId);
        await reader.RequireReadAsync(task.WorkspaceId, user, ct);
        var people = new WorkspacePeople(await lookup.ListMembersAsync(task.WorkspaceId, ct));
        return (await tasks.ListCommentsAsync(taskId, ct)).Select(c => TaskMapper.ToComment(c, people)).ToList();
    }

    public async Task<CommentResponse> AddCommentAsync(Guid taskId, AddTaskCommentRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var task = await tasks.GetAsync(taskId, ct) ?? throw TaskErrors.TaskNotFound(taskId);
        var writer = await reader.RequireWriteAsync(task.WorkspaceId, user, ct);
        var people = new WorkspacePeople(await lookup.ListMembersAsync(task.WorkspaceId, ct));
        var comment = new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AuthorPersonType = writer.PersonType,
            AuthorPersonId = writer.PersonId,
            AuthorName = people.NameOf(writer.PersonType, writer.PersonId),
            Body = request.Body.Trim(),
            CreatedAt = clock.GetUtcNow(),
        };
        tasks.AddComment(comment);
        await tasks.SaveChangesAsync(ct);
        return TaskMapper.ToComment(comment, people);
    }

    public async Task<IReadOnlyList<TaskCardResponse>> CreateFromMeetingAsync(Guid workspaceId, Guid meetingId, IReadOnlyList<string> titles,
        WorkspaceAccess organizer, CancellationToken ct)
    {
        WorkspaceMemberHandler.Check(organizer, WorkspaceAccessLevel.Writer);
        var cleaned = titles.Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        if (cleaned.Count == 0 || cleaned.Any(t => t.Length > 200))
        {
            throw TaskErrors.Invalid("titles", "Mỗi dòng biên bản phải có 1–200 ký tự.");
        }

        var workspace = await workspaces.GetByIdAsync(workspaceId, ct) ?? throw new NotFoundException("workspace", workspaceId);
        var column = await tasks.ListColumnAsync(workspaceId, TaskStatuses.ToDo, ct);
        var nextPosition = column.Count == 0 ? 0 : column.Max(t => t.Position) + 1;
        var now = clock.GetUtcNow();
        var created = cleaned.Select((title, i) => new TaskItem
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Kind = TaskKinds.Task,
            Title = title,
            Status = TaskStatuses.ToDo,
            Position = nextPosition + i,
            CreatedByType = organizer.PersonType,
            CreatedById = organizer.PersonId,
            SourceMeetingId = meetingId,
            CreatedAt = now,
            UpdatedAt = now,
        }).ToList();
        foreach (var task in created)
        {
            tasks.Add(task, []);
        }
        await tasks.SaveChangesAsync(ct);

        var people = new WorkspacePeople(await lookup.ListMembersAsync(workspaceId, ct));
        return created.Select(t => TaskMapper.ToCard(new TaskRow(t, [], 0, null, null), workspace.TeamName, people, now, isArchive: false)).ToList();
    }

    public async Task<IReadOnlyList<DueTaskResponse>> ListDueAsync(int withinHours, bool includeOverdue, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rows = await tasks.ListDueAsync(now, now.AddHours(Math.Clamp(withinHours, 1, MaxDueWindowHours)), includeOverdue, ct);
        var members = await lookup.ListMembersAsync(rows.Select(r => r.Workspace.Id).Distinct().ToList(), ct);
        var peopleByWorkspace = members.ToDictionary(g => g.Key, g => new WorkspacePeople(g));

        return rows.Select(r =>
        {
            var people = peopleByWorkspace.GetValueOrDefault(r.Workspace.Id) ?? new WorkspacePeople([]);
            return new DueTaskResponse(TaskMapper.ToCard(r.Row, r.Workspace.TeamName, people, now, isArchive: false))
            {
                AssigneeStudentIds = r.Row.AssigneeIds,
                MemberStudentIds = people.ActiveStudentIds,
                LecturerId = people.ActiveInstructorId,
            };
        }).ToList();
    }

    private async Task<TaskDetailResponse> DetailAsync(TaskRow row, bool isArchive, CancellationToken ct)
    {
        var workspace = await workspaces.GetByIdAsync(row.Task.WorkspaceId, ct) ?? throw new NotFoundException("workspace", row.Task.WorkspaceId);
        var people = new WorkspacePeople(await lookup.ListMembersAsync(workspace.Id, ct));
        var submissions = await submissionReader.MapAsync(await tasks.ListSubmissionsAsync(row.Task.Id, ct), people, row.Task.DueAt, isArchive, ct);
        var comments = (await tasks.ListCommentsAsync(row.Task.Id, ct)).Select(c => TaskMapper.ToComment(c, people)).ToList();
        return TaskMapper.ToDetail(row, workspace, people, clock.GetUtcNow(), isArchive, submissions, comments);
    }

    /// <summary>Việc thường: sinh viên của nhóm. Bài nộp: chỉ giảng viên hướng dẫn (mentor không tạo – rule 23).</summary>
    private static void RequireEditor(string kind, WorkspaceAccess writer)
    {
        if (kind == TaskKinds.Deliverable && !writer.IsInstructor)
        {
            throw TaskErrors.InstructorOnly("Chỉ giảng viên hướng dẫn tạo và sửa bài nộp");
        }
        if (kind == TaskKinds.Task && !writer.IsStudent)
        {
            throw TaskErrors.StudentsOnly("Chỉ thành viên nhóm tạo và sửa việc");
        }
    }

    private static List<Guid> ValidateAssignees(IEnumerable<Guid> assigneeIds, WorkspacePeople people)
    {
        var ids = assigneeIds.Distinct().ToList();
        if (ids.Except(people.ActiveStudentIds).Any())
        {
            throw TaskErrors.Invalid("assigneeStudentIds", "Người làm phải là thành viên hiện tại của nhóm.");
        }
        return ids;
    }

    private static void Renumber(IEnumerable<TaskItem> column)
    {
        var position = 0;
        foreach (var task in column)
        {
            task.Position = position++;
        }
    }

    private static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
