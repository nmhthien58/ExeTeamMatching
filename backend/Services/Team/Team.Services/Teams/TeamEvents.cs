namespace Team.Services.Teams;

// Payload sự kiện exe.team của module Teams (docs/contracts/events.md), key = teamId.

public static class TeamEventTypes
{
    public const string TeamCreated = "TeamCreated";
    public const string ProjectCreated = "ProjectCreated";
    public const string ProjectChanged = "ProjectChanged";
    public const string TeamLinkedToProject = "TeamLinkedToProject";
}

public sealed record TeamCreated(
    Guid TeamId, Guid SemesterId, string CourseCode, string Name, Guid LeaderStudentId, string LeaderName, Guid? ProjectId,
    bool IsAutoAssigned, bool IsInherited, Guid? SourceTeamId);

public sealed record ProjectCreated(Guid ProjectId, Guid TeamId, string Name, IReadOnlyList<int> DomainIds, Guid SemesterId, string CourseCode);

public sealed record ProjectChanged(Guid ProjectId, Guid TeamId, int Version, string ChangeType, string Name, string? Reason, Guid? ChangeRequestId);

public sealed record TeamLinkedToProject(Guid TeamId, Guid ProjectId, Guid PreviousTeamId, string LinkedBy);
