namespace Team.Services.LecturerPreferences;

public static class AssignmentEventTypes
{
    public const string LecturerPreferencesUpdated = "LecturerPreferencesUpdated";
}

public sealed record LecturerPreferencesUpdated(
    Guid TeamId,
    string TeamName,
    IReadOnlyList<Guid> LecturerIds,
    IReadOnlyList<Guid> AddedLecturerIds);
