using System.Security.Claims;
using BuildingBlocks.Errors;
using Collab.Repositories.Files;
using Collab.Repositories.Tasks;
using Collab.Repositories.Workspaces;
using Collab.Services.Files;
using Collab.Services.Workspaces;

namespace Collab.Services.Tasks;

public interface IDeliverableService
{
    /// <summary>Nhóm nộp một phiên bản file đã có ở C06. Nộp sau hạn vẫn được (chip Trễ hạn giữ lại).</summary>
    Task<SubmissionResponse> SubmitAsync(Guid taskId, SubmitDeliverableRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Giảng viên hướng dẫn duyệt: ACCEPTED (việc chuyển Xong) hoặc NEEDS_REVISION kèm nhận xét.</summary>
    Task<SubmissionResponse> ReviewAsync(Guid submissionId, ReviewSubmissionRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Tổng quan trễ hạn cho giảng viên (L01): bài nộp quá hạn mà nhóm chưa nộp, ở mọi nhóm mình hướng dẫn.</summary>
    Task<IReadOnlyList<TaskCardResponse>> ListOverdueForInstructorAsync(ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Bài nộp và duyệt bài (BE-70, rule 23).</summary>
public sealed class DeliverableService(
    ITaskRepository tasks,
    IFileRepository files,
    IWorkspaceRepository workspaces,
    IWorkspaceLookup lookup,
    IWorkspaceAccessService access,
    IWorkspaceReader reader,
    SubmissionReader submissionReader,
    TimeProvider clock) : IDeliverableService
{
    public async Task<SubmissionResponse> SubmitAsync(Guid taskId, SubmitDeliverableRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var task = await tasks.GetAsync(taskId, ct) ?? throw TaskErrors.TaskNotFound(taskId);
        var writer = await reader.RequireWriteAsync(task.WorkspaceId, user, ct);
        if (!writer.IsStudent)
        {
            throw TaskErrors.StudentsOnly("Chỉ thành viên nhóm nộp bài");
        }
        if (task.Kind != TaskKinds.Deliverable)
        {
            throw TaskErrors.NotDeliverable();
        }

        var version = await files.GetVersionByIdAsync(request.FileVersionId, ct);
        var file = version is null ? null : await files.GetFileAsync(version.FileId, ct);
        if (version is null || file is null || file.WorkspaceId != task.WorkspaceId)
        {
            throw TaskErrors.Invalid("fileVersionId", "Chọn một phiên bản file của dự án này.");
        }

        var submission = new DeliverableSubmission
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            FileVersionId = version.Id,
            SubmittedById = writer.PersonId,
            SubmittedAt = clock.GetUtcNow(),
            ReviewStatus = SubmissionStatuses.Pending,
        };
        tasks.AddSubmission(submission);

        var people = new WorkspacePeople(await lookup.ListMembersAsync(task.WorkspaceId, ct));
        tasks.AddEvent(nameof(DeliverableSubmitted), task.WorkspaceId,
            new DeliverableSubmitted(task.WorkspaceId, taskId, submission.Id, version.Id, people.ActiveInstructorId));
        await tasks.SaveChangesAsync(ct);

        return (await submissionReader.MapAsync([submission], people, task.DueAt, isArchive: false, ct))[0];
    }

    public async Task<SubmissionResponse> ReviewAsync(Guid submissionId, ReviewSubmissionRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var submission = await tasks.GetSubmissionAsync(submissionId, ct) ?? throw new NotFoundException("bản nộp", submissionId);
        var task = await tasks.GetAsync(submission.TaskId, ct) ?? throw TaskErrors.TaskNotFound(submission.TaskId);
        var writer = await reader.RequireWriteAsync(task.WorkspaceId, user, ct);
        if (!writer.IsInstructor)
        {
            throw TaskErrors.InstructorOnly("Chỉ giảng viên hướng dẫn duyệt bài nộp");
        }
        if (submission.ReviewStatus != SubmissionStatuses.Pending)
        {
            throw TaskErrors.SubmissionReviewed();
        }

        var now = clock.GetUtcNow();
        submission.ReviewStatus = request.Status;
        submission.ReviewComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        submission.ReviewedById = writer.PersonId;
        submission.ReviewedAt = now;
        if (request.Status == SubmissionStatuses.Accepted && task.Status != TaskStatuses.Done)
        {
            task.Status = TaskStatuses.Done;
            task.CompletedAt = now;
            task.UpdatedAt = now;
        }

        var people = new WorkspacePeople(await lookup.ListMembersAsync(task.WorkspaceId, ct));
        tasks.AddEvent(nameof(DeliverableReviewed), task.WorkspaceId, new DeliverableReviewed(
            task.WorkspaceId, task.Id, submission.Id, submission.ReviewStatus, submission.ReviewComment, people.ActiveStudentIds));
        await tasks.SaveChangesAsync(ct);

        return (await submissionReader.MapAsync([submission], people, task.DueAt, isArchive: false, ct))[0];
    }

    public async Task<IReadOnlyList<TaskCardResponse>> ListOverdueForInstructorAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var instructing = (await workspaces.ListForPeopleAsync(access.PeopleOf(user), ct))
            .Where(w => w.Role == WorkspaceRoles.Instructor)
            .Select(w => w.Workspace.Id)
            .ToList();
        if (instructing.Count == 0)
        {
            return [];
        }

        var now = clock.GetUtcNow();
        var rows = await tasks.ListOverdueDeliverablesAsync(instructing, now, ct);
        var members = await lookup.ListMembersAsync(instructing, ct);
        return rows
            .Select(r => TaskMapper.ToCard(r.Row, r.Workspace.TeamName, new WorkspacePeople(members[r.Workspace.Id]), now, isArchive: false))
            .ToList();
    }
}
