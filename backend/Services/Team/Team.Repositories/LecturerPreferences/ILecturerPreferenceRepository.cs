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
    Task<LecturerPreferencePreviousInstructor?> GetPreviousInstructorAsync(Guid teamId, CancellationToken ct);
    Task ReplacePreferencesAsync(Guid teamId, IReadOnlyList<LecturerPreference> preferences, IEnumerable<OutboxMessage> outboxMessages, CancellationToken ct);
}
