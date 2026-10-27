using Collab.Repositories.Context;
using Collab.Repositories.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Collab.Repositories.Checkpoints;

public interface ICheckpointRepository
{
    Task<Workspace?> WorkspaceAsync(Guid workspaceId, CancellationToken ct);
    Task<CheckpointReview?> ReviewAsync(Guid workspaceId, short checkpointNo, CancellationToken ct);
    Task<List<CheckpointReview>> ReviewsAsync(Guid workspaceId, CancellationToken ct);
    Task<List<Guid>> StudentIdsAsync(Guid workspaceId, CancellationToken ct);
    Task<string?> InstructorNameAsync(Guid workspaceId, Guid lecturerId, CancellationToken ct);
    Task<List<Workspace>> InstructorWorkspacesAsync(Guid lecturerId, CancellationToken ct);
    Task<List<CheckpointReview>> ReviewsForAsync(IReadOnlyCollection<Guid> workspaceIds, CancellationToken ct);
    void Add(CheckpointReview review);
    void Remove(CheckpointReview review);
    Task SaveAsync(CancellationToken ct);
    CollabDbContext Context { get; }
}

public sealed class CheckpointRepository(CollabDbContext db) : ICheckpointRepository
{
    public Task<Workspace?> WorkspaceAsync(Guid workspaceId, CancellationToken ct) =>
        db.Set<Workspace>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == workspaceId, ct);

    public Task<CheckpointReview?> ReviewAsync(Guid workspaceId, short checkpointNo, CancellationToken ct) =>
        db.Set<CheckpointReview>().FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.CheckpointNo == checkpointNo, ct);

    public Task<List<CheckpointReview>> ReviewsAsync(Guid workspaceId, CancellationToken ct) =>
        db.Set<CheckpointReview>().AsNoTracking().Where(x => x.WorkspaceId == workspaceId).ToListAsync(ct);

    public Task<List<Guid>> StudentIdsAsync(Guid workspaceId, CancellationToken ct) =>
        db.Set<WorkspaceMember>().AsNoTracking()
            .Where(x => x.WorkspaceId == workspaceId && x.PersonType == PersonTypes.Student && x.LeftAt == null)
            .Select(x => x.PersonId).ToListAsync(ct);

    public Task<string?> InstructorNameAsync(Guid workspaceId, Guid lecturerId, CancellationToken ct) =>
        db.Set<WorkspaceMember>().AsNoTracking()
            .Where(x => x.WorkspaceId == workspaceId && x.PersonType == PersonTypes.Lecturer &&
                        x.PersonId == lecturerId && x.Role == WorkspaceRoles.Instructor && x.LeftAt == null)
            .Select(x => x.DisplayName).FirstOrDefaultAsync(ct);

    public Task<List<Workspace>> InstructorWorkspacesAsync(Guid lecturerId, CancellationToken ct) =>
        (from member in db.Set<WorkspaceMember>().AsNoTracking()
         join workspace in db.Set<Workspace>().AsNoTracking() on member.WorkspaceId equals workspace.Id
         where member.PersonType == PersonTypes.Lecturer && member.PersonId == lecturerId &&
               member.Role == WorkspaceRoles.Instructor && member.LeftAt == null &&
               workspace.CourseCode == "EXE101" && workspace.ArchivedAt == null
         orderby workspace.TeamName
         select workspace).ToListAsync(ct);

    public Task<List<CheckpointReview>> ReviewsForAsync(IReadOnlyCollection<Guid> workspaceIds, CancellationToken ct) =>
        db.Set<CheckpointReview>().AsNoTracking().Where(x => workspaceIds.Contains(x.WorkspaceId)).ToListAsync(ct);

    public void Add(CheckpointReview review) => db.Set<CheckpointReview>().Add(review);
    public void Remove(CheckpointReview review) => db.Set<CheckpointReview>().Remove(review);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public CollabDbContext Context => db;
}
