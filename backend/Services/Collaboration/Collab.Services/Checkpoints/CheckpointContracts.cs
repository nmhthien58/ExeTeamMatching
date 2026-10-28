namespace Collab.Services.Checkpoints;

public sealed record MarkCheckpointRequest(DateOnly PresentedOn, string? Comment);

public sealed record CheckpointItem(
    short CheckpointNo,
    bool Presented,
    DateOnly? PresentedOn,
    string? Comment,
    int? TargetWeek,
    bool Overdue,
    bool CanUndo);

public sealed record CheckpointProgress(
    Guid WorkspaceId,
    string TeamName,
    int DoneCount,
    IReadOnlyList<CheckpointItem> Items);
