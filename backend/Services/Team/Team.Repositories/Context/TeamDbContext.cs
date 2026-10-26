using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Team.Repositories.AuditLog;
using Team.Repositories.Invitations;
using Team.Repositories.JoinRequests;
using Team.Repositories.Members;
using Team.Repositories.Posts;
using Team.Repositories.Teams;

namespace Team.Repositories.Context;

/// <summary>
/// DbContext của <c>team_db</c> (schema <c>team</c>). Map đúng bảng trong deploy/db/03_team_db.sql:
/// tên cột snake_case qua UseSnakeCaseNamingConvention, tên bảng và khóa khai báo dưới đây.
/// Chỉ Thiên và Cường sửa file này (AGENTS.md mục 2); module khác cần thêm bảng thì đề xuất qua PR.
/// </summary>
/// <remarks>
/// Lỗi ràng buộc của DB đổi sang ProblemDetails trong <see cref="SaveChangesAsync(bool, CancellationToken)"/> (<see cref="TeamDbErrors"/>).
/// Trigger đếm thành viên cập nhật dòng <c>teams</c>, nên <c>xmin</c> của nhóm đổi mỗi khi thêm / xóa thành viên:
/// <see cref="Teams.TeamEntity.MemberCount"/> và <c>Version</c> trong bộ nhớ cũ đi sau lần lưu đó; cần sửa nhóm tiếp thì đọc lại.
/// Lỗi trigger DEFERRABLE chỉ được đổi sang ProblemDetails khi COMMIT nằm trong SaveChanges, nên Services không tự mở transaction
/// (Outbox ghi cùng một lần SaveChanges đã là một transaction).
/// </remarks>
public class TeamDbContext(DbContextOptions<TeamDbContext> options) : DbContext(options)
{
    public const string Schema = "team";

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return MemberChangesBesideTeamEdits() is { Count: > 0 } members
                ? await SaveTeamsBeforeMembersAsync(members, acceptAllChangesOnSuccess, cancellationToken)
                : await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (Exception ex) when (TeamDbErrors.ToAppException(ex) is { } mapped)
        {
            throw mapped;
        }
    }

    // EF ghi INSERT/DELETE team_members trước UPDATE teams; trigger đếm thành viên đổi xmin của nhóm ở giữa
    // nên UPDATE báo concurrency giả. Khi có cả hai: lưu nhóm trước, thành viên sau, trong cùng transaction.
    private List<(EntityEntry Entry, EntityState State)> MemberChangesBesideTeamEdits() =>
        ChangeTracker.Entries<TeamEntity>().Any(e => e.State == EntityState.Modified)
            ? ChangeTracker.Entries<TeamMember>()
                .Where(e => e.State is EntityState.Added or EntityState.Deleted)
                .Select(e => ((EntityEntry)e, e.State))
                .ToList()
            : [];

    private async Task<int> SaveTeamsBeforeMembersAsync(List<(EntityEntry Entry, EntityState State)> members, bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        await using var transaction = Database.CurrentTransaction is null ? await Database.BeginTransactionAsync(cancellationToken) : null;
        int saved;
        try
        {
            members.ForEach(m => m.Entry.State = EntityState.Unchanged);
            saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        finally
        {
            members.ForEach(m => m.Entry.State = m.State);
        }
        saved += await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return saved;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddOutboxMessages(Schema);

        modelBuilder.Entity<TeamEntity>(e =>
        {
            e.ToTable("teams"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.Id, x.SemesterId });
            e.Property(x => x.MemberCount).ValueGeneratedOnAddOrUpdate().Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            e.Property(x => x.MemberCount).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); UpdatedAtByTrigger(e.Property(x => x.UpdatedAt)); Xmin(e.Property(x => x.Version));
        });
        modelBuilder.Entity<TeamDomain>(e => { e.ToTable("team_domains"); e.HasKey(x => new { x.TeamId, x.DomainId }); });
        modelBuilder.Entity<TeamMember>(e => { e.ToTable("team_members"); e.HasKey(x => x.Id); e.Property(x => x.JoinedAt).HasDefaultValueSql("now()"); Xmin(e.Property(x => x.Version)); });
        modelBuilder.Entity<Invitation>(e => { e.ToTable("invitations"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); e.Property(x => x.ExpiresAt).HasDefaultValueSql("now() + interval '72 hours'"); Xmin(e.Property(x => x.Version)); });
        modelBuilder.Entity<JoinRequest>(e => { e.ToTable("join_requests"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); e.Property(x => x.ExpiresAt).HasDefaultValueSql("now() + interval '72 hours'"); Xmin(e.Property(x => x.Version)); });
        modelBuilder.Entity<JoinRequestIdea>(e => { e.ToTable("join_request_ideas"); e.HasKey(x => new { x.JoinRequestId, x.Position }); });
        modelBuilder.Entity<RecruitmentPost>(e => { e.ToTable("recruitment_posts"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); UpdatedAtByTrigger(e.Property(x => x.UpdatedAt)); Xmin(e.Property(x => x.Version)); });
        modelBuilder.Entity<RecruitmentPostSpecialization>(e => { e.ToTable("recruitment_post_specializations"); e.HasKey(x => new { x.RecruitmentPostId, x.SpecializationId }); });
        modelBuilder.Entity<SeekingPost>(e => { e.ToTable("seeking_posts"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); UpdatedAtByTrigger(e.Property(x => x.UpdatedAt)); Xmin(e.Property(x => x.Version)); });
        modelBuilder.Entity<SeekingPostDomain>(e => { e.ToTable("seeking_post_domains"); e.HasKey(x => new { x.SeekingPostId, x.DomainId }); });
        modelBuilder.Entity<AuditLogEntry>(e =>
        {
            e.ToTable("audit_logs"); e.HasKey(x => x.Id); e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.OldValues).HasColumnType("jsonb"); e.Property(x => x.NewValues).HasColumnType("jsonb");
            e.Property(x => x.OccurredAt).HasDefaultValueSql("now()");
        });

        // Khóa ngoại – để EF ghi đúng thứ tự (bảng cha trước) và khớp hành vi xóa của DB script.
        modelBuilder.Entity<TeamEntity>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => x.SourceTeamId).HasConstraintName("fk_teams_source_team").OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TeamDomain>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => x.TeamId).HasConstraintName("fk_team_domains_team").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TeamMember>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => new { x.TeamId, x.SemesterId }).HasPrincipalKey(x => new { x.Id, x.SemesterId }).HasConstraintName("fk_team_members_team").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Invitation>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => x.TeamId).HasConstraintName("fk_invitations_team").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<JoinRequest>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => x.TeamId).HasConstraintName("fk_join_requests_team").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<JoinRequestIdea>().HasOne<JoinRequest>().WithMany().HasForeignKey(x => x.JoinRequestId).HasConstraintName("fk_join_request_ideas_request").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RecruitmentPost>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => x.TeamId).HasConstraintName("fk_recruitment_posts_team").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RecruitmentPostSpecialization>().HasOne<RecruitmentPost>().WithMany().HasForeignKey(x => x.RecruitmentPostId).HasConstraintName("fk_rps_post").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SeekingPostDomain>().HasOne<SeekingPost>().WithMany().HasForeignKey(x => x.SeekingPostId).HasConstraintName("fk_spd_post").OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>Cột hệ thống <c>xmin</c> của PostgreSQL làm concurrency token (BE-24).</summary>
    private static void Xmin(PropertyBuilder<uint> property) =>
        property.HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

    /// <summary>Trigger <c>set_updated_at</c> ghi cột này; EF đọc lại sau khi lưu.</summary>
    private static void UpdatedAtByTrigger(PropertyBuilder<DateTimeOffset> property) =>
        property.HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
}
