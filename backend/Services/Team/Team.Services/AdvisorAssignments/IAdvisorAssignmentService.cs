using System.Security.Claims;

namespace Team.Services.AdvisorAssignments;

public interface IAdvisorAssignmentService
{
    Task<LecturerAssignmentResponse?> GetTeamLecturerAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);

    Task<MentorAssignmentResponse?> GetTeamMentorAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);

    Task<LecturerTeamsResponse> GetLecturerTeamsMeAsync(
        string? courseCode,
        string? classCode,
        string? search,
        ClaimsPrincipal user,
        CancellationToken ct);

    Task<IReadOnlyList<AdvisedTeamResponse>> GetMentorTeamsMeAsync(ClaimsPrincipal user, CancellationToken ct);
}
