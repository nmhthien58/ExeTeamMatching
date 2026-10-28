using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.BaselineImport;
using Team.Repositories.Context;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.Members;
using Team.Repositories.Projects;
using Team.Repositories.Teams;

namespace Team.Repositories.ChangeRequests;

public interface IChangeRequestRepository
{
    Task<List<TeamMember>> ListMembersAsync(Guid teamId, CancellationToken ct);
    Task<List<TeamBaselineMember>> ListBaselineMembersAsync(Guid teamId, CancellationToken ct);
    Task<List<ChangeRequestItem>> ListPendingItemsAsync(Guid teamId, CancellationToken ct);
    Task<List<ChangeRequestItem>> ListItemsAsync(Guid requestId, CancellationToken ct);
    Task<List<ChangeRequestItem>> ListApprovedItemsBeforeAsync(Guid teamId, DateTimeOffset createdAt, CancellationToken ct);
    Task<ChangeRequestItem?> GetPendingItemAsync(Guid teamId, Guid itemId, CancellationToken ct);
    Task<ChangeRequestItem?> GetPendingProjectChangeAsync(Guid teamId, CancellationToken ct);
    Task<ChangeRequest?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> HasPendingAsync(Guid teamId, CancellationToken ct);
    Task<List<ChangeRequest>> ListByTeamAsync(Guid teamId, CancellationToken ct);
    Task<(List<ChangeRequest> Items, int TotalCount)> SearchAsync(string? status, Guid? roundId, int page, int pageSize, CancellationToken ct);
    Task<Project?> GetProjectAsync(Guid? projectId, CancellationToken ct);
    Task<ProjectVersion?> GetProjectVersionForRequestAsync(Guid requestId, CancellationToken ct);
    Task<ProjectVersion?> GetProjectVersionAsync(Guid projectId, short version, CancellationToken ct);
    Task<LecturerAssignment?> GetAssignedLecturerAsync(Guid teamId, CancellationToken ct);
    void Add(object entity);
    void Remove(object entity);
    void AddEvent(string eventType, Guid requestId, object payload);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class ChangeRequestRepository(TeamDbContext db) : IChangeRequestRepository
{
    public Task<List<TeamMember>> ListMembersAsync(Guid teamId, CancellationToken ct) =>
        db.Set<TeamMember>().Where(m => m.TeamId == teamId).OrderBy(m => m.JoinedAt).ToListAsync(ct);

    public Task<List<TeamBaselineMember>> ListBaselineMembersAsync(Guid teamId, CancellationToken ct) =>
        db.Set<TeamBaselineMember>().AsNoTracking().Where(m => m.TeamId == teamId).OrderBy(m => m.StudentCode).ToListAsync(ct);

    public Task<List<ChangeRequestItem>> ListPendingItemsAsync(Guid teamId, CancellationToken ct) =>
        db.Set<ChangeRequestItem>().Where(i => i.TeamId == teamId && i.ChangeRequestId == null).OrderBy(i => i.CreatedAt).ToListAsync(ct);

    public Task<List<ChangeRequestItem>> ListItemsAsync(Guid requestId, CancellationToken ct) =>
        db.Set<ChangeRequestItem>().AsNoTracking().Where(i => i.ChangeRequestId == requestId).OrderBy(i => i.CreatedAt).ToListAsync(ct);

    public Task<List<ChangeRequestItem>> ListApprovedItemsBeforeAsync(Guid teamId, DateTimeOffset createdAt, CancellationToken ct) =>
        (from item in db.Set<ChangeRequestItem>().AsNoTracking()
         join request in db.Set<ChangeRequest>().AsNoTracking() on item.ChangeRequestId equals request.Id
         where request.TeamId == teamId && request.Status == "APPROVED" && request.CreatedAt < createdAt
         orderby request.CreatedAt, item.CreatedAt
         select item).ToListAsync(ct);

    public Task<ChangeRequestItem?> GetPendingItemAsync(Guid teamId, Guid itemId, CancellationToken ct) =>
        db.Set<ChangeRequestItem>().FirstOrDefaultAsync(i => i.Id == itemId && i.TeamId == teamId && i.ChangeRequestId == null, ct);

    public Task<ChangeRequestItem?> GetPendingProjectChangeAsync(Guid teamId, CancellationToken ct) =>
        db.Set<ChangeRequestItem>().FirstOrDefaultAsync(i => i.TeamId == teamId && i.ChangeRequestId == null && i.Action == "PROJECT_CHANGE", ct);

    public Task<ChangeRequest?> GetAsync(Guid id, CancellationToken ct) =>
        db.Set<ChangeRequest>().FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> HasPendingAsync(Guid teamId, CancellationToken ct) =>
        db.Set<ChangeRequest>().AnyAsync(r => r.TeamId == teamId && r.Status == "PENDING", ct);

    public Task<List<ChangeRequest>> ListByTeamAsync(Guid teamId, CancellationToken ct) =>
        db.Set<ChangeRequest>().AsNoTracking().Where(r => r.TeamId == teamId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public async Task<(List<ChangeRequest> Items, int TotalCount)> SearchAsync(
        string? status, Guid? roundId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Set<ChangeRequest>().AsNoTracking();
        if (status is not null) query = query.Where(r => r.Status == status);
        if (roundId is not null) query = query.Where(r => r.RoundId == roundId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<Project?> GetProjectAsync(Guid? projectId, CancellationToken ct) =>
        projectId is null ? Task.FromResult<Project?>(null) : db.Set<Project>().FirstOrDefaultAsync(p => p.Id == projectId, ct);

    public Task<ProjectVersion?> GetProjectVersionForRequestAsync(Guid requestId, CancellationToken ct) =>
        db.Set<ProjectVersion>().AsNoTracking().FirstOrDefaultAsync(v => v.ChangeRequestId == requestId, ct);

    public Task<ProjectVersion?> GetProjectVersionAsync(Guid projectId, short version, CancellationToken ct) =>
        db.Set<ProjectVersion>().AsNoTracking().FirstOrDefaultAsync(v => v.ProjectId == projectId && v.Version == version, ct);

    public Task<LecturerAssignment?> GetAssignedLecturerAsync(Guid teamId, CancellationToken ct) =>
        db.Set<LecturerAssignment>().AsNoTracking().FirstOrDefaultAsync(a => a.TeamId == teamId && a.Status == "ASSIGNED", ct);

    public void Add(object entity) => db.Add(entity);
    public void Remove(object entity) => db.Remove(entity);

    public void AddEvent(string eventType, Guid requestId, object payload) =>
        db.AddOutboxMessage(BuildingBlocks.Messaging.Topics.ChangeRequest, eventType, "ChangeRequest", requestId, payload);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
