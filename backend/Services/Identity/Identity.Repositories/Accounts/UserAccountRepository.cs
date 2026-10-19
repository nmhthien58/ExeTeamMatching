using Identity.Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace Identity.Repositories.Accounts;

public interface IUserAccountRepository
{
    /// <summary>Tìm theo email (citext – không phân biệt hoa thường), kèm vai trò.</summary>
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken ct);

    Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Tài khoản khác đang giữ <paramref name="externalRefId"/> cho vai trò <paramref name="roleId"/>.</summary>
    Task<bool> ExternalRefInUseAsync(short roleId, Guid externalRefId, Guid exceptAccountId, CancellationToken ct);

    void Add(UserAccount account);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class UserAccountRepository(IdentityDbContext db) : IUserAccountRepository
{
    public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken ct) =>
        db.UserAccounts.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Email == email, ct);

    public Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.UserAccounts.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> ExternalRefInUseAsync(short roleId, Guid externalRefId, Guid exceptAccountId, CancellationToken ct) =>
        db.UserAccountRoles.AnyAsync(x => x.RoleId == roleId && x.ExternalRefId == externalRefId && x.UserAccountId != exceptAccountId, ct);

    public void Add(UserAccount account) => db.UserAccounts.Add(account);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
