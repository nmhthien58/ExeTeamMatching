namespace Team.Repositories.MentorAssignments;

public interface IMentorAssignmentRepository
{
    Task<MentorAssignment?> GetActiveByTeamIdAsync(Guid teamId, CancellationToken ct);
    Task<List<MentorAssignment>> ListActiveByMentorIdAsync(Guid mentorId, CancellationToken ct);
    Task<List<MentorAssignment>> ListActiveByMentorIdTrackedAsync(Guid mentorId, CancellationToken ct);
    Task<Dictionary<Guid, int>> CountActiveTeamsByMentorIdsAsync(IReadOnlyList<Guid> mentorIds, Guid? semesterId, CancellationToken ct);
    void AddOutboxMessage<TPayload>(string topic, string eventType, string aggregateType, Guid aggregateId, TPayload payload);
    Task SaveChangesAsync(CancellationToken ct);
}
