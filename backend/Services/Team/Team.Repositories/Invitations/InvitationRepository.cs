using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;

namespace Team.Repositories.Invitations;

/// <summary>
/// Lọc theo trạng thái hiển thị: lời mời PENDING đã quá hạn (job chưa chạy) được coi là EXPIRED.
/// </summary>
public static class RequestStatusFilter
{
    public const string Pending = "PENDING";
    public const string Expired = "EXPIRED";
}

public interface IInvitationRepository
{
    /// <summary>Được theo dõi.</summary>
    Task<Invitation?> GetAsync(Guid invitationId, CancellationToken ct);

    Task<List<Invitation>> ListByTeamAsync(Guid teamId, string? status, DateTimeOffset now, CancellationToken ct);

    Task<List<Invitation>> ListForInviteeAsync(Guid studentId, string? status, DateTimeOffset now, CancellationToken ct);

    /// <summary>Lời mời PENDING đã quá hạn (được theo dõi) – InvitationExpiryJob.</summary>
    Task<List<Invitation>> ListOverdueAsync(DateTimeOffset now, CancellationToken ct);

    void Add(Invitation invitation);

    /// <summary>Ghi sự kiện exe.invitation vào outbox (key = invitationId).</summary>
    void AddEvent<TPayload>(string eventType, Guid invitationId, TPayload payload);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class InvitationRepository(TeamDbContext db) : IInvitationRepository
{
    public Task<Invitation?> GetAsync(Guid invitationId, CancellationToken ct) =>
        db.Set<Invitation>().FirstOrDefaultAsync(i => i.Id == invitationId, ct);

    public Task<List<Invitation>> ListByTeamAsync(Guid teamId, string? status, DateTimeOffset now, CancellationToken ct) =>
        Filter(db.Set<Invitation>().AsNoTracking().Where(i => i.TeamId == teamId), status, now).OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

    public Task<List<Invitation>> ListForInviteeAsync(Guid studentId, string? status, DateTimeOffset now, CancellationToken ct) =>
        Filter(db.Set<Invitation>().AsNoTracking().Where(i => i.InviteeStudentId == studentId), status, now)
            .OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

    public Task<List<Invitation>> ListOverdueAsync(DateTimeOffset now, CancellationToken ct) =>
        db.Set<Invitation>().Where(i => i.Status == RequestStatusFilter.Pending && i.ExpiresAt <= now).ToListAsync(ct);

    public void Add(Invitation invitation) => db.Add(invitation);

    public void AddEvent<TPayload>(string eventType, Guid invitationId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Invitation, eventType, "Invitation", invitationId, payload);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static IQueryable<Invitation> Filter(IQueryable<Invitation> query, string? status, DateTimeOffset now) => status switch
    {
        null => query,
        RequestStatusFilter.Pending => query.Where(i => i.Status == RequestStatusFilter.Pending && i.ExpiresAt > now),
        RequestStatusFilter.Expired => query.Where(i => i.Status == RequestStatusFilter.Expired
            || (i.Status == RequestStatusFilter.Pending && i.ExpiresAt <= now)),
        _ => query.Where(i => i.Status == status),
    };
}
