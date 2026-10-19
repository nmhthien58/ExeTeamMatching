using Identity.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace Identity.Repositories.Tokens;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);

    /// <summary>Các token còn hiệu lực (chưa thu hồi) của một tài khoản.</summary>
    Task<List<RefreshToken>> ListActiveAsync(Guid userAccountId, CancellationToken ct);

    void Add(RefreshToken token);

    /// <summary>Lưu thay đổi. Trả false nếu token đã bị request khác thu hồi trước (xung đột <c>revoked_at</c>).</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken ct);
}

public sealed class RefreshTokenRepository(IdentityDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public Task<List<RefreshToken>> ListActiveAsync(Guid userAccountId, CancellationToken ct) =>
        db.RefreshTokens.Where(x => x.UserAccountId == userAccountId && x.RevokedAt == null).ToListAsync(ct);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public async Task<bool> TrySaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
