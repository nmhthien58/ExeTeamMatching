using Team.Services.Teams;

namespace Team.Services.LecturerPreferences;

public sealed record LecturerRef(
    Guid LecturerId,
    string FullName,
    IReadOnlyList<CatalogRef> Domains);

public sealed record LecturerPreferenceItem(
    Guid LecturerId,
    string FullName,
    IReadOnlyList<CatalogRef> Domains,
    int Rank,
    bool IsInterested);

public sealed record LecturerPreferenceListResponse(
    int MaxPreferences,
    bool Locked,
    DateTimeOffset? LockAt,
    IReadOnlyList<LecturerPreferenceItem> Preferences,
    IReadOnlyList<LecturerRef> InterestedLecturers,
    LecturerRef? PreviousInstructor);

public sealed record UpdateLecturerPreferencesInput(
    IReadOnlyList<Guid> LecturerIds);

public sealed record LecturerPreferenceMeItem(
    TeamCardResponse Team,
    int Rank,
    bool IAmInterested);
