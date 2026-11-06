using BuildingBlocks.Outbox;
using Team.Repositories.TopicInterests;

namespace Team.Repositories.LecturerPreferences;

public sealed record LecturerPreferencePreviousInstructor(Guid LecturerId, string LecturerName);

public interface ILecturerPreferenceRepository
{
    Task<IReadOnlyList<LecturerPreference>> ListByTeamIdAsync(Guid teamId, CancellationToken ct);
    Task<IReadOnlyList<LecturerPreference>> ListByLecturerIdAsync(Guid lecturerId, CancellationToken ct);
    Task<bool> HasAssignedInstructorAsync(Guid teamId, CancellationToken ct);
    Task<IReadOnlyList<TopicInterest>> ListOpenLecturerInterestsByTeamIdAsync(Guid teamId, CancellationToken ct);
    Task<bool> IsLecturerInterestedAsync(Guid teamId, Guid lecturerId, CancellationToken ct);
    Task<HashSet<Guid>> GetInterestedTeamIdsAsync(Guid lecturerId, IEnumerable<Guid> teamIds, CancellationToken ct);
    Task<LecturerPreferencePreviousInstructor?> GetPreviousInstructorAsync(Guid teamId, CancellationToken ct);
    /// <summary>Số nhóm đang hướng dẫn (ASSIGNED) của từng giảng viên trong học kỳ và môn, không tính nhóm DISBANDED.</summary>
    Task<Dictionary<Guid, int>> CountActiveAssignmentsAsync(Guid semesterId, string courseCode, IReadOnlyList<Guid> lecturerIds, CancellationToken ct);
    void AddEvent<TPayload>(string eventType, Guid teamId, TPayload payload);
    Task ReplacePreferencesAsync(Guid teamId, IReadOnlyList<LecturerPreference> preferences, CancellationToken ct);
}
