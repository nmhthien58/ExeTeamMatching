using BuildingBlocks.Outbox;
using Identity.Repositories.Accounts;
using Identity.Repositories.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Identity.Repositories.Context;

/// <summary>
/// DbContext của <c>identity_db</c> (schema <c>identity</c>, BE-10). Map đúng bảng trong deploy/db/01_identity_db.sql:
/// tên cột snake_case qua UseSnakeCaseNamingConvention, tên bảng và khóa khai báo dưới đây.
/// </summary>
public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<UserAccountRole> UserAccountRoles => Set<UserAccountRole>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddProcessedEvents(Schema);

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<UserAccount>(e =>
        {
            e.ToTable("user_accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasColumnType("citext");
            e.Property(x => x.GoogleSubject).HasMaxLength(255);
            e.Property(x => x.FullName).HasMaxLength(150);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("uq_user_accounts_email");
            e.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.UserAccountId)
                .HasConstraintName("fk_user_account_roles_account").OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserAccountRole>(e =>
        {
            e.ToTable("user_account_roles");
            e.HasKey(x => new { x.UserAccountId, x.RoleId });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId)
                .HasConstraintName("fk_user_account_roles_role").OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasColumnType("char(64)");
            e.Property(x => x.CreatedByIp).HasColumnType("inet");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            // Hai request refresh cùng lúc với một token: chỉ một UPDATE khớp (WHERE revoked_at IS NULL).
            e.Property(x => x.RevokedAt).IsConcurrencyToken();
            e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("uq_refresh_tokens_token_hash");
            e.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId)
                .HasConstraintName("fk_refresh_tokens_user_account").OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedByTokenId)
                .HasConstraintName("fk_refresh_tokens_replaced_by").OnDelete(DeleteBehavior.SetNull);
        });
    }
}
