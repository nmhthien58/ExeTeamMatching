using Collab.Repositories.Tasks;
using Collab.Services.Files;
using System.Diagnostics.CodeAnalysis;
using FluentValidation;

namespace Collab.Services.Tasks;

/// <summary>Schema <c>TaskCard</c> (C04). <see cref="SubmissionStatus"/> chỉ có ở bài nộp.</summary>
public record TaskCardResponse
{
    public required Guid Id { get; init; }
    public required Guid WorkspaceId { get; init; }
    public required string TeamName { get; init; }
    public required string Kind { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required int Position { get; init; }
    public required IReadOnlyList<PersonRef> Assignees { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public required bool IsOverdue { get; init; }
    public string? SubmissionStatus { get; init; }
    public required int CommentCount { get; init; }

    /// <summary>Việc của workspace EXE101 cùng đề tài – chỉ đọc (rule 26).</summary>
    public required bool IsArchive { get; init; }
}

/// <summary>Schema <c>TaskDetail</c> (C05).</summary>
public sealed record TaskDetailResponse : TaskCardResponse
{
    public TaskDetailResponse()
    {
    }

    /// <summary>Chép các trường của thẻ; phần chi tiết gán bằng object initializer.</summary>
    [SetsRequiredMembers]
    public TaskDetailResponse(TaskCardResponse card) : base(card)
    {
        CreatedBy = null!;
        Submissions = [];
        Comments = [];
    }

    public string? Description { get; init; }
    public required PersonRef CreatedBy { get; init; }
    public Guid? SourceMeetingId { get; init; }
    public required IReadOnlyList<SubmissionResponse> Submissions { get; init; }
    public required IReadOnlyList<CommentResponse> Comments { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

/// <summary>
/// Dòng của <c>GET /internal/tasks/due</c>: TaskCard cộng người nhận để Job phát <c>DeliverableOverdue</c>
/// (events.md cần memberStudentIds, lecturerId – đề xuất bổ sung vào collab.yaml).
/// </summary>
public sealed record DueTaskResponse : TaskCardResponse
{
    public DueTaskResponse()
    {
    }

    [SetsRequiredMembers]
    public DueTaskResponse(TaskCardResponse card) : base(card)
    {
        AssigneeStudentIds = [];
        MemberStudentIds = [];
    }

    public required IReadOnlyList<Guid> AssigneeStudentIds { get; init; }
    public required IReadOnlyList<Guid> MemberStudentIds { get; init; }
    public Guid? LecturerId { get; init; }
}

/// <summary>Schema <c>Submission</c>.</summary>
public sealed record SubmissionResponse(
    Guid Id,
    Guid TaskId,
    ProjectFileResponse File,
    int Version,
    PersonRef SubmittedBy,
    DateTimeOffset SubmittedAt,
    bool IsLate,
    string ReviewStatus,
    string? ReviewComment,
    DateTimeOffset? ReviewedAt);

/// <summary>Query của <c>GET /api/workspaces/{id}/tasks</c>.</summary>
public sealed record TaskQuery(string? Status = null, string? Kind = null, bool Mine = false, bool Overdue = false, bool IncludeArchive = false);

/// <summary>Schema <c>TaskInput</c> – tạo và sửa việc / bài nộp.</summary>
public sealed record TaskInput(string Title, string? Description, string? Kind, IReadOnlyList<Guid>? AssigneeStudentIds, DateTimeOffset? DueAt);

public sealed record MoveTaskRequest(string Status, int? Position);

public sealed record AddTaskCommentRequest(string Body);

public sealed record SubmitDeliverableRequest(Guid FileVersionId);

public sealed record ReviewSubmissionRequest(string Status, string? Comment);

public sealed class TaskQueryValidator : AbstractValidator<TaskQuery>
{
    public TaskQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null || TaskStatuses.All.Contains(s)).WithMessage("status phải là TODO, DOING hoặc DONE.");
        RuleFor(x => x.Kind).Must(k => k is null or TaskKinds.Task or TaskKinds.Deliverable).WithMessage("kind phải là TASK hoặc DELIVERABLE.");
    }
}

public sealed class TaskInputValidator : AbstractValidator<TaskInput>
{
    public TaskInputValidator()
    {
        RuleFor(x => x.Title)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Nhập tên việc.")
            .MaximumLength(200).WithMessage("Tên việc tối đa 200 ký tự.");
        RuleFor(x => x.Description).MaximumLength(4000).WithMessage("Mô tả tối đa 4.000 ký tự.");
        RuleFor(x => x.Kind)
            .Must(k => k is null or TaskKinds.Task or TaskKinds.Deliverable).WithMessage("kind phải là TASK hoặc DELIVERABLE.");
        RuleFor(x => x.DueAt)
            .NotNull().When(x => x.Kind == TaskKinds.Deliverable).WithMessage("Bài nộp phải có hạn nộp.");
        RuleFor(x => x.AssigneeStudentIds)
            .Must(ids => ids is null || ids.Count <= 6).WithMessage("Tối đa 6 người làm.");
    }
}

public sealed class MoveTaskRequestValidator : AbstractValidator<MoveTaskRequest>
{
    public MoveTaskRequestValidator()
    {
        RuleFor(x => x.Status).Must(TaskStatuses.All.Contains).WithMessage("status phải là TODO, DOING hoặc DONE.");
        RuleFor(x => x.Position).GreaterThanOrEqualTo(0).When(x => x.Position is not null).WithMessage("Vị trí không hợp lệ.");
    }
}

public sealed class AddTaskCommentRequestValidator : AbstractValidator<AddTaskCommentRequest>
{
    public AddTaskCommentRequestValidator()
    {
        RuleFor(x => x.Body)
            .Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("Nhập nội dung bình luận.")
            .MaximumLength(2000).WithMessage("Bình luận tối đa 2.000 ký tự.");
    }
}

public sealed class SubmitDeliverableRequestValidator : AbstractValidator<SubmitDeliverableRequest>
{
    public SubmitDeliverableRequestValidator()
    {
        RuleFor(x => x.FileVersionId).NotEmpty().WithMessage("Chọn phiên bản file để nộp.");
    }
}

public sealed class ReviewSubmissionRequestValidator : AbstractValidator<ReviewSubmissionRequest>
{
    public ReviewSubmissionRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s is SubmissionStatuses.Accepted or SubmissionStatuses.NeedsRevision)
            .WithMessage("status phải là ACCEPTED hoặc NEEDS_REVISION.");
        RuleFor(x => x.Comment)
            .Must(c => !string.IsNullOrWhiteSpace(c)).When(x => x.Status == SubmissionStatuses.NeedsRevision)
            .WithMessage("Cần sửa thì phải ghi nhận xét.")
            .MaximumLength(2000).WithMessage("Nhận xét tối đa 2.000 ký tự.");
    }
}
