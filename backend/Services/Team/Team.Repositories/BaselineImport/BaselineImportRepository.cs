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

    public async Task<IReadOnlyList<Exe101TeamWithMembers>> GetExe101TeamsBySemesterAsync(Guid semesterId, CancellationToken ct)
    {
        var teams = await db.Set<TeamEntity>().AsNoTracking()
            .Where(t => t.CourseCode == "EXE101" && t.SemesterId == semesterId && t.Status != "DISBANDED")
            .Select(t => new { t.Id, t.Name, t.SemesterId, t.ProjectId })
            .ToListAsync(ct);

        if (teams.Count == 0) return [];

        var teamIds = teams.Select(t => t.Id).ToList();

        var members = await db.Set<TeamMember>().AsNoTracking()
            .Where(m => teamIds.Contains(m.TeamId))
            .OrderBy(m => m.Role == "LEADER" ? 0 : 1)
            .ThenBy(m => m.JoinedAt)
            .Select(m => new OldTeamMemberInfo(m.TeamId, m.StudentId, m.StudentCode, m.FullName, m.SpecializationId, m.Role, m.MajorId))
            .ToListAsync(ct);

        var membersByTeam = members.GroupBy(m => m.TeamId).ToDictionary(g => g.Key, g => (IReadOnlyList<OldTeamMemberInfo>)g.ToList());

        var projectIds = teams.Where(t => t.ProjectId != null).Select(t => t.ProjectId!.Value).Distinct().ToList();
        var projects = await db.Set<Projects.Project>().AsNoTracking()
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return teams.Select(t => new Exe101TeamWithMembers(
            t.Id,
            t.Name,
            t.SemesterId,
            t.ProjectId,
            t.ProjectId != null && projects.TryGetValue(t.ProjectId.Value, out var pName) ? pName : null,
            membersByTeam.TryGetValue(t.Id, out var mList) ? mList : []
        )).ToList();
    }

    public async Task<IReadOnlyList<Exe101TeamWithMembers>> GetPreviousExe101TeamsAsync(Guid currentSemesterId, CancellationToken ct)
    {
        var previousSemesterId = await db.Set<TeamEntity>().AsNoTracking()
            .Where(t => t.CourseCode == "EXE101" && t.SemesterId != currentSemesterId && t.Status != "DISBANDED")
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => (Guid?)t.SemesterId)
            .FirstOrDefaultAsync(ct);

        if (previousSemesterId == null) return [];

        return await GetExe101TeamsBySemesterAsync(previousSemesterId.Value, ct);
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
