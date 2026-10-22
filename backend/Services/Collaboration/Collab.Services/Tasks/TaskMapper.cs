using Collab.Repositories.Files;
using Collab.Repositories.Tasks;
using Collab.Repositories.Workspaces;
using Collab.Services.Files;

namespace Collab.Services.Tasks;

/// <summary>Đổi việc / bài nộp sang schema của collab.yaml.</summary>
internal static class TaskMapper
{
    public const string NotSubmitted = "NOT_SUBMITTED";

    /// <summary>
    /// Việc: chưa xong mà quá hạn. Bài nộp: quá hạn mà chưa nộp đúng hạn lần nào – nộp muộn vẫn giữ chip Trễ hạn (rule 23).
    /// </summary>
    public static bool IsOverdue(TaskRow row, DateTimeOffset now) => row.Task.DueAt is { } due && due < now && (
        row.Task.Kind == TaskKinds.Deliverable
            ? row.FirstSubmittedAt is not { } first || first > due
            : row.Task.Status != TaskStatuses.Done);

    public static TaskCardResponse ToCard(TaskRow row, string teamName, WorkspacePeople people, DateTimeOffset now, bool isArchive) => new()
    {
        Id = row.Task.Id,
        WorkspaceId = row.Task.WorkspaceId,
        TeamName = teamName,
        Kind = row.Task.Kind,
        Title = row.Task.Title,
        Status = row.Task.Status,
        Position = row.Task.Position,
        Assignees = row.AssigneeIds.Select(people.Student).ToList(),
        DueAt = row.Task.DueAt,
        IsOverdue = IsOverdue(row, now),
        SubmissionStatus = SubmissionStatusOf(row),
        CommentCount = row.CommentCount,
        IsArchive = isArchive,
    };

    public static TaskDetailResponse ToDetail(TaskRow row, Workspace workspace, WorkspacePeople people, DateTimeOffset now, bool isArchive,
        IReadOnlyList<SubmissionResponse> submissions, IReadOnlyList<CommentResponse> comments) =>
        new(ToCard(row, workspace.TeamName, people, now, isArchive))
        {
            Description = row.Task.Description,
            CreatedBy = people.Ref(row.Task.CreatedByType, row.Task.CreatedById),
            SourceMeetingId = row.Task.SourceMeetingId,
            Submissions = submissions,
            Comments = comments,
            CompletedAt = row.Task.CompletedAt,
        };

    public static string? SubmissionStatusOf(TaskRow row) =>
        row.Task.Kind == TaskKinds.Deliverable ? row.LatestSubmission?.ReviewStatus ?? NotSubmitted : null;

    public static SubmissionResponse ToSubmission(DeliverableSubmission submission, FileVersion version, FileWithLatest file,
        WorkspacePeople people, DateTimeOffset? dueAt, bool isArchive) => new(
        submission.Id,
        submission.TaskId,
        FileService.ToResponse(file, isArchive),
        version.Version,
        people.Student(submission.SubmittedById),
        submission.SubmittedAt,
        IsLate: dueAt is { } due && submission.SubmittedAt > due,
        submission.ReviewStatus,
        submission.ReviewComment,
        submission.ReviewedAt);

    public static CommentResponse ToComment(TaskComment comment, WorkspacePeople people) => new(
        comment.Id, people.Ref(comment.AuthorPersonType, comment.AuthorPersonId, comment.AuthorName), comment.Body, null, comment.CreatedAt);
}
