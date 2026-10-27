using Team.Services.Teams;

namespace Team.Services.AdvisorAssignments;

public sealed record LecturerAssignmentResponse(
    Guid Id,
    Guid TeamId,
    Guid LecturerId,
    string LecturerName,
    string Status,
    string Reason,
    short? PreferenceRank,
    string Source,
    DateTimeOffset AssignedAt,
    string? Note);

public sealed record MentorAssignmentResponse(
    Guid Id,
    Guid TeamId,
    Guid MentorId,
    string MentorName,
    string? MentorOrganization,
    bool IsInternal,
    string Status,
    string Reason,
    string Source,
    DateTimeOffset AssignedAt,
    DateTimeOffset? EndedAt);

public sealed record AdvisedTeamResponse(
    TeamCardResponse Team,
    string Reason,
    short? PreferenceRank);

public sealed record LecturerTeamsResponse(
    int? MaxTeams,
    IReadOnlyList<AdvisedTeamResponse> Instructing,
    IReadOnlyList<AdvisedTeamResponse> Mentoring);
