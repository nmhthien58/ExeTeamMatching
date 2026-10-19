using Collab.Repositories.Context;
using Collab.Repositories.Messages;
using Microsoft.EntityFrameworkCore;

namespace Collab.Repositories.Workspaces;

/// <summary>Kiểu người trong workspace (cột <c>person_type</c>).</summary>
public static class PersonTypes
{
    public const string Student = "STUDENT";
    public const string Lecturer = "LECTURER";
    public const string Mentor = "MENTOR";
}

/// <summary>Vai trò trong workspace (cột <c>role</c>).</summary>
public static class WorkspaceRoles
{
    public const string Leader = "LEADER";
    public const string Member = "MEMBER";
    public const string Instructor = "INSTRUCTOR";
    public const string Mentor = "MENTOR";
}

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken ct);
    Task<Workspace?> GetByTeamIdAsync(Guid teamId, CancellationToken ct);
    void Add(Workspace workspace, IEnumerable<Channel> channels);

    /// <summary>Thành viên đang hiệu lực (left_at null) khớp một trong các người cho trước.</summary>
    Task<WorkspaceMember?> FindActiveMemberAsync(Guid workspaceId, IReadOnlyCollection<(string Type, Guid Id)> people, CancellationToken ct);

    Task<WorkspaceMember?> FindMemberAsync(Guid workspaceId, string personType, Guid personId, CancellationToken ct);
    Task<WorkspaceMember?> FindActiveByRoleAsync(Guid workspaceId, string role, CancellationToken ct);
    void AddMember(WorkspaceMember member);

    /// <summary>Workspace mà một trong các người cho trước đang là thành viên, kèm vai trò.</summary>
    Task<List<(Workspace Workspace, string Role)>> ListForPeopleAsync(IReadOnlyCollection<(string Type, Guid Id)> people, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class WorkspaceRepository(CollabDbContext db) : IWorkspaceRepository
{
    public Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken ct) =>
        db.Set<Workspace>().FirstOrDefaultAsync(w => w.Id == workspaceId, ct);

    public Task<Workspace?> GetByTeamIdAsync(Guid teamId, CancellationToken ct) =>
        db.Set<Workspace>().FirstOrDefaultAsync(w => w.TeamId == teamId, ct);

    public void Add(Workspace workspace, IEnumerable<Channel> channels)
    {
        db.Set<Workspace>().Add(workspace);
        db.Set<Channel>().AddRange(channels);
    }

    public async Task<WorkspaceMember?> FindActiveMemberAsync(Guid workspaceId, IReadOnlyCollection<(string Type, Guid Id)> people, CancellationToken ct)
    {
        var ids = people.Select(p => p.Id).ToList();
        var candidates = await db.Set<WorkspaceMember>()
            .Where(m => m.WorkspaceId == workspaceId && m.LeftAt == null && ids.Contains(m.PersonId))
            .ToListAsync(ct);
        return candidates.FirstOrDefault(m => people.Contains((m.PersonType, m.PersonId)));
    }

    public Task<WorkspaceMember?> FindMemberAsync(Guid workspaceId, string personType, Guid personId, CancellationToken ct) =>
        db.Set<WorkspaceMember>().FirstOrDefaultAsync(
            m => m.WorkspaceId == workspaceId && m.PersonType == personType && m.PersonId == personId, ct);

    public Task<WorkspaceMember?> FindActiveByRoleAsync(Guid workspaceId, string role, CancellationToken ct) =>
        db.Set<WorkspaceMember>().FirstOrDefaultAsync(
            m => m.WorkspaceId == workspaceId && m.Role == role && m.LeftAt == null, ct);

    public void AddMember(WorkspaceMember member) => db.Set<WorkspaceMember>().Add(member);

    public async Task<List<(Workspace Workspace, string Role)>> ListForPeopleAsync(IReadOnlyCollection<(string Type, Guid Id)> people, CancellationToken ct)
    {
        var ids = people.Select(p => p.Id).ToList();
        var rows = await db.Set<WorkspaceMember>()
            .Where(m => m.LeftAt == null && ids.Contains(m.PersonId))
            .Join(db.Set<Workspace>(), m => m.WorkspaceId, w => w.Id, (m, w) => new { Member = m, Workspace = w })
            .Where(x => x.Workspace.ArchivedAt == null)
            .OrderBy(x => x.Workspace.TeamName)
            .ToListAsync(ct);
        return rows
            .Where(x => people.Contains((x.Member.PersonType, x.Member.PersonId)))
            .Select(x => (x.Workspace, x.Member.Role))
            .ToList();
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
