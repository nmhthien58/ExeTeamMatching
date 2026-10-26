using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.Invitations;

namespace Team.Repositories.JoinRequests;

public interface IJoinRequestRepository
{
    /// <summary>Được theo dõi.</summary>
    Task<JoinRequest?> GetAsync(Guid joinRequestId, CancellationToken ct);

    Task<List<JoinRequest>> ListByTeamAsync(Guid teamId, string? status, DateTimeOffset now, CancellationToken ct);

    Task<List<JoinRequest>> ListByStudentAsync(Guid studentId, string? status, DateTimeOffset now, CancellationToken ct);

    /// <summary>Yêu cầu PENDING đã quá hạn (được theo dõi) – InvitationExpiryJob.</summary>
    Task<List<JoinRequest>> ListOverdueAsync(DateTimeOffset now, CancellationToken ct);

    Task<ILookup<Guid, JoinRequestIdea>> ListIdeasAsync(IReadOnlyCollection<Guid> joinRequestIds, CancellationToken ct);

    void Add(object entity);

    /// <summary>Ghi sự kiện exe.invitation vào outbox (key = joinRequestId).</summary>
    void AddEvent<TPayload>(string eventType, Guid joinRequestId, TPayload payload);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class JoinRequestRepository(TeamDbContext db) : IJoinRequestRepository
{
    public Task<JoinRequest?> GetAsync(Guid joinRequestId, CancellationToken ct) =>
        db.Set<JoinRequest>().FirstOrDefaultAsync(r => r.Id == joinRequestId, ct);

    public Task<List<JoinRequest>> ListByTeamAsync(Guid teamId, string? status, DateTimeOffset now, CancellationToken ct) =>
        Filter(db.Set<JoinRequest>().AsNoTracking().Where(r => r.TeamId == teamId), status, now).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public Task<List<JoinRequest>> ListByStudentAsync(Guid studentId, string? status, DateTimeOffset now, CancellationToken ct) =>
        Filter(db.Set<JoinRequest>().AsNoTracking().Where(r => r.StudentId == studentId), status, now).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public Task<List<JoinRequest>> ListOverdueAsync(DateTimeOffset now, CancellationToken ct) =>
        db.Set<JoinRequest>().Where(r => r.Status == RequestStatusFilter.Pending && r.ExpiresAt <= now).ToListAsync(ct);

    public async Task<ILookup<Guid, JoinRequestIdea>> ListIdeasAsync(IReadOnlyCollection<Guid> joinRequestIds, CancellationToken ct) =>
        (await db.Set<JoinRequestIdea>().AsNoTracking().Where(i => joinRequestIds.Contains(i.JoinRequestId)).OrderBy(i => i.Position).ToListAsync(ct))
            .ToLookup(i => i.JoinRequestId);

    public void Add(object entity) => db.Add(entity);

    public void AddEvent<TPayload>(string eventType, Guid joinRequestId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Invitation, eventType, "JoinRequest", joinRequestId, payload);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static IQueryable<JoinRequest> Filter(IQueryable<JoinRequest> query, string? status, DateTimeOffset now) => status switch
    {
        null => query,
        RequestStatusFilter.Pending => query.Where(r => r.Status == RequestStatusFilter.Pending && r.ExpiresAt > now),
        RequestStatusFilter.Expired => query.Where(r => r.Status == RequestStatusFilter.Expired
            || (r.Status == RequestStatusFilter.Pending && r.ExpiresAt <= now)),
        _ => query.Where(r => r.Status == status),
    };
}
