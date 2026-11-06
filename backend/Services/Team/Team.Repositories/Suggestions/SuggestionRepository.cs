using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.Invitations;

namespace Team.Repositories.Suggestions;

/// <summary>Truy vấn của module Suggestions (BE-39).</summary>
public interface ISuggestionRepository
{
    /// <summary>Sinh viên nhóm đang mời (lời mời còn chờ, chưa hết hạn) – để S18 hiện "Đã mời".</summary>
    Task<HashSet<Guid>> ListInvitedStudentIdsAsync(Guid teamId, DateTimeOffset now, CancellationToken ct);
}

public sealed class SuggestionRepository(TeamDbContext db) : ISuggestionRepository
{
    public async Task<HashSet<Guid>> ListInvitedStudentIdsAsync(Guid teamId, DateTimeOffset now, CancellationToken ct) =>
        (await db.Set<Invitation>().AsNoTracking()
            .Where(i => i.TeamId == teamId && i.Status == "PENDING" && i.ExpiresAt > now)
            .Select(i => i.InviteeStudentId)
            .ToListAsync(ct)).ToHashSet();
}
