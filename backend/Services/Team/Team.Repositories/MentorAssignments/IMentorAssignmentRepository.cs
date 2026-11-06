namespace Team.Repositories.MentorAssignments;

public interface IMentorAssignmentRepository
{
    Task<MentorAssignment?> GetActiveByTeamIdAsync(Guid teamId, CancellationToken ct);
    Task<List<MentorAssignment>> ListActiveByMentorIdAsync(Guid mentorId, CancellationToken ct);
    Task<List<MentorAssignment>> ListActiveByMentorIdTrackedAsync(Guid mentorId, CancellationToken ct);
    /// <summary>Số nhóm đang mentor của từng mentor, chỉ nhóm chưa giải tán; có học kỳ thì lọc theo học kỳ.</summary>
    Task<Dictionary<Guid, int>> CountActiveTeamsByMentorIdsAsync(IReadOnlyList<Guid> mentorIds, Guid? semesterId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
