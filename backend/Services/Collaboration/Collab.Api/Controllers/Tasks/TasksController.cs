using BuildingBlocks.Auth;
using Collab.Services.Files;
using Collab.Services.Tasks;
using Collab.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Collab.Api.Controllers.Tasks;

/// <summary>Bảng việc C04, chi tiết việc / bài nộp C05 (BE-70, rule 23) – collab.yaml tag Tasks.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class TasksController(ITaskService tasks, IDeliverableService deliverables) : ControllerBase
{
    /// <summary>Bảng việc; lọc nhanh Của tôi / Bài nộp / Trễ hạn; <c>includeArchive</c> thêm việc đã xong của EXE101.</summary>
    [HttpGet("workspaces/{workspaceId:guid}/tasks")]
    [Authorize(Policy = CollabPolicies.WorkspaceMember)]
    public async Task<ActionResult<IReadOnlyList<TaskCardResponse>>> List(Guid workspaceId, [FromQuery] TaskQuery query, CancellationToken ct) =>
        Ok(await tasks.ListAsync(workspaceId, query, HttpContext.GetWorkspaceAccess(), ct));

    /// <summary>Thành viên tạo việc; giảng viên hướng dẫn tạo bài nộp (kind = DELIVERABLE, bắt buộc dueAt).</summary>
    [HttpPost("workspaces/{workspaceId:guid}/tasks")]
    [Authorize(Policy = CollabPolicies.WorkspaceWriter)]
    public async Task<ActionResult<TaskDetailResponse>> Create(Guid workspaceId, TaskInput input, CancellationToken ct)
    {
        var task = await tasks.CreateAsync(workspaceId, input, HttpContext.GetWorkspaceAccess(), ct);
        return Created($"/api/tasks/{task.Id}", task);
    }

    [HttpGet("tasks/{taskId:guid}")]
    public async Task<ActionResult<TaskDetailResponse>> Get(Guid taskId, CancellationToken ct) =>
        Ok(await tasks.GetAsync(taskId, User, ct));

    [HttpPatch("tasks/{taskId:guid}")]
    public async Task<ActionResult<TaskDetailResponse>> Update(Guid taskId, TaskInput input, CancellationToken ct) =>
        Ok(await tasks.UpdateAsync(taskId, input, User, ct));

    [HttpDelete("tasks/{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid taskId, CancellationToken ct)
    {
        await tasks.DeleteAsync(taskId, User, ct);
        return NoContent();
    }

    /// <summary>Kéo thả trên web, menu "Chuyển sang…" trên mobile.</summary>
    [HttpPost("tasks/{taskId:guid}/move")]
    public async Task<ActionResult<TaskCardResponse>> Move(Guid taskId, MoveTaskRequest request, CancellationToken ct) =>
        Ok(await tasks.MoveAsync(taskId, request, User, ct));

    [HttpGet("tasks/{taskId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentResponse>>> Comments(Guid taskId, CancellationToken ct) =>
        Ok(await tasks.ListCommentsAsync(taskId, User, ct));

    [HttpPost("tasks/{taskId:guid}/comments")]
    public async Task<ActionResult<CommentResponse>> Comment(Guid taskId, AddTaskCommentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await tasks.AddCommentAsync(taskId, request, User, ct));

    /// <summary>Nhóm nộp bài – một phiên bản file đã có ở C06.</summary>
    [HttpPost("tasks/{taskId:guid}/submissions")]
    public async Task<ActionResult<SubmissionResponse>> Submit(Guid taskId, SubmitDeliverableRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await deliverables.SubmitAsync(taskId, request, User, ct));

    /// <summary>Giảng viên hướng dẫn duyệt bản nộp.</summary>
    [HttpPost("submissions/{submissionId:guid}/review")]
    public async Task<ActionResult<SubmissionResponse>> Review(Guid submissionId, ReviewSubmissionRequest request, CancellationToken ct) =>
        Ok(await deliverables.ReviewAsync(submissionId, request, User, ct));
}

/// <summary>Endpoint nội bộ cho Job Service (TaskDueReminderJob – BE-72), chỉ token dịch vụ, không qua Gateway.</summary>
[ApiController]
[Authorize(Policy = Policies.Internal)]
[Route("internal/tasks")]
public sealed class InternalTasksController(ITaskService tasks) : ControllerBase
{
    /// <summary>Việc / bài nộp chưa xong có hạn trong <paramref name="withinHours"/> giờ tới, kèm việc đã trễ hạn.</summary>
    [HttpGet("due")]
    public async Task<ActionResult<IReadOnlyList<DueTaskResponse>>> Due([FromQuery] int withinHours = 24, [FromQuery] bool includeOverdue = true,
        CancellationToken ct = default) =>
        Ok(await tasks.ListDueAsync(withinHours, includeOverdue, ct));
}
