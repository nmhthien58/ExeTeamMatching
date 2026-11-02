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

/// <summary>Request body của <c>POST /internal/mentors/team-counts</c> (BE-74).</summary>
public sealed record MentorTeamCountsRequest(
    IReadOnlyList<Guid> MentorIds,
    Guid? SemesterId = null);

/// <summary>Schema <c>MentorTeamCount</c> trả về cho từng mentorId (BE-74).</summary>
public sealed record MentorTeamCountResponse(
    Guid MentorId,
    int CurrentTeamCount);

/// <summary>Payload sự kiện <c>MentorUnassigned</c> trên topic <c>exe.assignment</c> (docs/contracts/events.md).</summary>
public sealed record MentorUnassignedEvent(
    Guid AssignmentId,
    Guid TeamId,
    Guid MentorId,
    string Reason);

/// <summary>Payload sự kiện <c>MentorRegistered</c> từ topic <c>exe.student</c> (Academic Service).</summary>
public sealed record MentorRegisteredEvent(
    Guid MentorId,
    bool IsActive,
    string? FullName = null,
    string? Email = null,
    string? Organization = null,
    bool? IsInternal = null,
    Guid? LecturerId = null,
    IReadOnlyList<int>? DomainIds = null,
    int? MaxTeams = null);
