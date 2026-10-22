namespace Collab.Services.Tasks;

// Payload sự kiện exe.collab của module Tasks (docs/contracts/events.md), key = workspaceId.

public sealed record TaskAssigned(Guid WorkspaceId, Guid TaskId, string Title, DateTimeOffset? DueAt, IReadOnlyList<Guid> AssigneeStudentIds);

public sealed record DeliverableCreated(Guid WorkspaceId, Guid TaskId, string Title, DateTimeOffset DueAt, IReadOnlyList<Guid> MemberStudentIds);

public sealed record DeliverableSubmitted(Guid WorkspaceId, Guid TaskId, Guid SubmissionId, Guid FileVersionId, Guid? LecturerId);

public sealed record DeliverableReviewed(
    Guid WorkspaceId, Guid TaskId, Guid SubmissionId, string ReviewStatus, string? Comment, IReadOnlyList<Guid> MemberStudentIds);
