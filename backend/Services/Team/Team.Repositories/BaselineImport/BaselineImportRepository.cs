using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.Members;
using Team.Repositories.Teams;

namespace Team.Repositories.BaselineImport;

public sealed class BaselineImportRepository(TeamDbContext db) : IBaselineImportRepository
{
    public async Task<HashSet<Guid>> GetStudentsAlreadyInTeamAsync(Guid semesterId, IEnumerable<Guid> studentIds, CancellationToken ct)
    {
        var idList = studentIds.Distinct().ToList();
        if (idList.Count == 0) return [];

        var existing = await db.Set<TeamMember>().AsNoTracking()
            .Where(m => m.SemesterId == semesterId && idList.Contains(m.StudentId))
            .Select(m => m.StudentId)
            .ToListAsync(ct);

        return existing.ToHashSet();
    }

    public async Task<HashSet<string>> GetExistingTeamNamesAsync(Guid semesterId, string courseCode, IEnumerable<string> teamNames, CancellationToken ct)
    {
        var names = teamNames.Select(n => n.ToLower()).Distinct().ToList();
        if (names.Count == 0) return [];

        var existing = await db.Set<TeamEntity>().AsNoTracking()
            .Where(t => t.SemesterId == semesterId && t.CourseCode == courseCode && names.Contains(t.Name.ToLower()))
            .Select(t => t.Name.ToLower())
            .ToListAsync(ct);

        return existing.ToHashSet();
    }

    public async Task<IReadOnlyList<Exe101CandidateTeam>> GetExe101CandidateTeamsAsync(IEnumerable<string> teamNames, CancellationToken ct)
    {
        var names = teamNames.Select(n => n.ToLower()).Distinct().ToList();
        if (names.Count == 0) return [];

        var teams = await db.Set<TeamEntity>().AsNoTracking()
            .Where(t => t.CourseCode == "EXE101" && names.Contains(t.Name.ToLower()) && t.ProjectId != null)
            .Select(t => new { t.Id, t.Name, t.SemesterId, t.ProjectId })
            .ToListAsync(ct);

        if (teams.Count == 0) return [];

        var teamIds = teams.Select(t => t.Id).ToList();

        var members = await db.Set<TeamMember>().AsNoTracking()
            .Where(m => teamIds.Contains(m.TeamId))
            .Select(m => new { m.TeamId, m.StudentCode })
            .ToListAsync(ct);

        var membersByTeam = members
            .GroupBy(m => m.TeamId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(m => m.StudentCode).ToList());

        return teams.Select(t => new Exe101CandidateTeam(
            t.Id,
            t.Name,
            t.SemesterId,
            t.ProjectId,
            membersByTeam.TryGetValue(t.Id, out var codes) ? codes : []
        )).ToList();
    }

    public async Task SaveImportedTeamsAsync(
        IReadOnlyList<TeamEntity> teams,
        IReadOnlyList<TeamMember> members,
        IReadOnlyList<TeamBaselineMember> baselineMembers,
        IReadOnlyList<OutboxMessage> outboxMessages,
        CancellationToken ct)
    {
        db.Set<TeamEntity>().AddRange(teams);
        db.Set<TeamMember>().AddRange(members);
        db.Set<TeamBaselineMember>().AddRange(baselineMembers);
        db.Set<OutboxMessage>().AddRange(outboxMessages);

        await db.SaveChangesAsync(ct);
    }
}
