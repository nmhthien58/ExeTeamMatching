using Team.Repositories.Members;
using Team.Repositories.Teams;
using BuildingBlocks.Outbox;

namespace Team.Repositories.BaselineImport;

public interface IBaselineImportRepository
{
    Task<HashSet<Guid>> GetStudentsAlreadyInTeamAsync(Guid semesterId, IEnumerable<Guid> studentIds, CancellationToken ct);

    Task<HashSet<string>> GetExistingTeamNamesAsync(Guid semesterId, string courseCode, IEnumerable<string> teamNames, CancellationToken ct);

    Task<IReadOnlyList<Exe101CandidateTeam>> GetExe101CandidateTeamsAsync(IEnumerable<string> teamNames, CancellationToken ct);

    Task<IReadOnlyList<Exe101TeamWithMembers>> GetPreviousExe101TeamsAsync(Guid currentSemesterId, CancellationToken ct);

    Task SaveImportedTeamsAsync(
        IReadOnlyList<TeamEntity> teams,
        IReadOnlyList<TeamMember> members,
        IReadOnlyList<TeamBaselineMember> baselineMembers,
        IReadOnlyList<OutboxMessage> outboxMessages,
        CancellationToken ct);
}
