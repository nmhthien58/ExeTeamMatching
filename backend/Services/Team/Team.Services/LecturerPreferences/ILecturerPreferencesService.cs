using System.Security.Claims;

namespace Team.Services.LecturerPreferences;

public interface ILecturerPreferencesService
{
    Task<LecturerPreferenceListResponse> GetPreferencesAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);
    Task<LecturerPreferenceListResponse> UpdatePreferencesAsync(Guid teamId, UpdateLecturerPreferencesInput input, ClaimsPrincipal user, CancellationToken ct);
    Task<IReadOnlyList<LecturerPreferenceMeItem>> GetPreferencesMeAsync(string? courseCode, ClaimsPrincipal user, CancellationToken ct);
}
