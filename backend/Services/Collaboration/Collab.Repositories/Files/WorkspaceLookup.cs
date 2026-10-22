using Collab.Repositories.Context;
using Collab.Repositories.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Collab.Repositories.Files;

/// <summary>Truy vấn workspace dùng chung cho module Files và Tasks: danh bạ người và liên kết kho EXE101 (rule 26).</summary>
public interface IWorkspaceLookup
{
    /// <summary>Mọi người từng ở workspace (cả người đã rời – để hiện tên trong lịch sử).</summary>
    Task<List<WorkspaceMember>> ListMembersAsync(Guid workspaceId, CancellationToken ct);

    /// <summary>Như trên cho nhiều workspace, nhóm theo workspace.</summary>
    Task<ILookup<Guid, WorkspaceMember>> ListMembersAsync(IReadOnlyCollection<Guid> workspaceIds, CancellationToken ct);

    /// <summary>
    /// Workspace EXE201 mà người gọi đang là thành viên và lấy <paramref name="archiveWorkspaceId"/> làm kho EXE101
    /// (<c>previous_workspace_id</c>); null nếu không có.
    /// </summary>
    Task<Workspace?> FindLinkedWorkspaceAsync(Guid archiveWorkspaceId, IReadOnlyCollection<(string Type, Guid Id)> people, CancellationToken ct);
}

public sealed class WorkspaceLookup(CollabDbContext db) : IWorkspaceLookup
{
    public Task<List<WorkspaceMember>> ListMembersAsync(Guid workspaceId, CancellationToken ct) =>
        db.Set<WorkspaceMember>().AsNoTracking().Where(m => m.WorkspaceId == workspaceId).ToListAsync(ct);

    public async Task<ILookup<Guid, WorkspaceMember>> ListMembersAsync(IReadOnlyCollection<Guid> workspaceIds, CancellationToken ct) =>
        (await db.Set<WorkspaceMember>().AsNoTracking().Where(m => workspaceIds.Contains(m.WorkspaceId)).ToListAsync(ct))
            .ToLookup(m => m.WorkspaceId);

    public async Task<Workspace?> FindLinkedWorkspaceAsync(Guid archiveWorkspaceId, IReadOnlyCollection<(string Type, Guid Id)> people, CancellationToken ct)
    {
        var ids = people.Select(p => p.Id).ToList();
        var candidates = await db.Set<Workspace>().AsNoTracking()
            .Where(w => w.PreviousWorkspaceId == archiveWorkspaceId)
            .Join(db.Set<WorkspaceMember>().Where(m => m.LeftAt == null && ids.Contains(m.PersonId)),
                w => w.Id, m => m.WorkspaceId, (w, m) => new { Workspace = w, m.PersonType, m.PersonId })
            .ToListAsync(ct);
        return candidates.FirstOrDefault(c => people.Contains((c.PersonType, c.PersonId)))?.Workspace;
    }
}
