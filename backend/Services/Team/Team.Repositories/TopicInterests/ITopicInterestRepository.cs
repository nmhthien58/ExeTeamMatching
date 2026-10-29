namespace Team.Repositories.TopicInterests;

public interface ITopicInterestRepository
{
    Task<TopicInterest?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<TopicInterest?> GetOpenInterestAsync(Guid teamId, string advisorKind, Guid advisorId, CancellationToken ct);
    Task<TopicInterest?> GetLatestInterestAsync(Guid teamId, string advisorKind, Guid advisorId, CancellationToken ct);
    Task<IReadOnlyList<TopicInterest>> ListByAdvisorAsync(string advisorKind, Guid advisorId, CancellationToken ct);
    Task<IReadOnlyList<TopicInterest>> ListByTeamIdAsync(Guid teamId, string? advisorKind, CancellationToken ct);
    Task<IReadOnlyList<TopicInterest>> ListOpenByTeamIdAsync(Guid teamId, string advisorKind, CancellationToken ct);
    Task<int> CountOpenByAdvisorAsync(string advisorKind, Guid advisorId, CancellationToken ct);
    Task<int> CountOpenByTeamAsync(Guid teamId, string advisorKind, Guid? excludeAdvisorId, CancellationToken ct);
    Task<bool> HasActiveAssignmentAsync(Guid teamId, string advisorKind, CancellationToken ct);
    Task<int> CountActiveAssignmentsByAdvisorAsync(string advisorKind, Guid advisorId, CancellationToken ct);
    Task<bool> WasLecturerEXE101InstructorAsync(Guid teamId, Guid lecturerId, CancellationToken ct);
    Task<int?> GetLecturerPreferenceRankAsync(Guid teamId, Guid lecturerId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ListTeamIdsInSemesterAsync(Guid semesterId, string? courseCode, CancellationToken ct);
    void Add(TopicInterest interest);
    void AddOutboxMessage<T>(string topic, string eventType, string aggregateType, Guid aggregateId, T payload);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
