namespace Team.Repositories.MentorAssignments;

public interface IMentorAssignmentRepository
{
    Task<MentorAssignment?> GetActiveByTeamIdAsync(Guid teamId, CancellationToken ct);
    Task<List<MentorAssignment>> ListActiveByMentorIdAsync(Guid mentorId, CancellationToken ct);
}
