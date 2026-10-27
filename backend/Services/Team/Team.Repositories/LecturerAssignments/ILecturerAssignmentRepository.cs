namespace Team.Repositories.LecturerAssignments;

public interface ILecturerAssignmentRepository
{
    Task<LecturerAssignment?> GetActiveByTeamIdAsync(Guid teamId, CancellationToken ct);
    Task<List<LecturerAssignment>> ListActiveByLecturerIdAsync(Guid lecturerId, CancellationToken ct);
}
