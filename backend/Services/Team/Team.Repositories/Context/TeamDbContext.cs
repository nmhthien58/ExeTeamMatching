using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Team.Repositories.AdvisorMatching;
using Team.Repositories.AuditLog;
using Team.Repositories.BaselineImport;
using Team.Repositories.ChangeRequests;
using Team.Repositories.Invitations;
using Team.Repositories.JoinRequests;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.LecturerPreferences;
using Team.Repositories.LeaderVotes;
using Team.Repositories.Members;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Posts;
using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Repositories.TopicInterests;

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
            return SecondPhaseChanges() is { Count: > 0 } later
                ? await SaveInTwoPhasesAsync(later, acceptAllChangesOnSuccess, cancellationToken)
                : await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (Exception ex) when (TeamDbErrors.ToAppException(ex) is { } mapped)
        {
            throw mapped;
        }
    }

    /// <summary>
    /// Thay đổi phải ghi sau, vì EF không biết thứ tự mà DB cần:
    /// <list type="bullet">
    /// <item>INSERT/DELETE team_members khi cũng sửa teams – trigger đếm thành viên đổi xmin của nhóm ở giữa nên UPDATE teams báo concurrency giả.</item>
    /// <item>Domain chính mới khi domain chính cũ của nhóm bị bỏ cờ / xóa – unique index một phần <c>uq_team_domains_primary</c>.</item>
    /// <item>Trưởng nhóm mới khi trưởng nhóm cũ thành thành viên – <c>uq_team_members_one_leader</c>.</item>
    /// </list>
    /// </summary>
    private List<(EntityEntry Entry, EntityState State)> SecondPhaseChanges()
    {
        var later = new List<EntityEntry>();
        if (ChangeTracker.Entries<TeamEntity>().Any(e => e.State == EntityState.Modified))
        {
            later.AddRange(ChangeTracker.Entries<TeamMember>().Where(e => e.State is EntityState.Added or EntityState.Deleted));
        }

        var members = ChangeTracker.Entries<TeamMember>().Where(e => e.State == EntityState.Modified).ToList();
        var teamsLosingLeader = members
            .Where(e => e.OriginalValues.GetValue<string>(nameof(TeamMember.Role)) == "LEADER" && e.Entity.Role != "LEADER")
            .Select(e => e.Entity.TeamId)
            .ToHashSet();
        later.AddRange(members.Where(e => e.Entity.Role == "LEADER" && teamsLosingLeader.Contains(e.Entity.TeamId)
            && e.Property(x => x.Role).IsModified));

        var domains = ChangeTracker.Entries<TeamDomain>().ToList();
        var teamsDroppingPrimary = domains
            .Where(e => e.OriginalValues.GetValue<bool>(nameof(TeamDomain.IsPrimary))
                && (e.State == EntityState.Deleted || (e.State == EntityState.Modified && !e.Entity.IsPrimary)))
            .Select(e => e.Entity.TeamId)
            .ToHashSet();
        later.AddRange(domains.Where(e => e.Entity.IsPrimary && teamsDroppingPrimary.Contains(e.Entity.TeamId)
            && (e.State == EntityState.Added || (e.State == EntityState.Modified && e.Property(x => x.IsPrimary).IsModified))));

        return later.Select(e => (e, e.State)).ToList();
    }

    private async Task<int> SaveInTwoPhasesAsync(List<(EntityEntry Entry, EntityState State)> later, bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        await using var transaction = Database.CurrentTransaction is null ? await Database.BeginTransactionAsync(cancellationToken) : null;
        int saved;
        // Modified → Unchanged làm EF trả giá trị về như lúc đọc, nên giữ giá trị mới để gán lại ở bước sau.
        var values = later.Select(m => m.Entry.CurrentValues.Clone()).ToList();
        try
        {
            later.ForEach(m => m.Entry.State = EntityState.Unchanged);
            saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        finally
        {
            for (var i = 0; i < later.Count; i++)
            {
                later[i].Entry.State = later[i].State;
                if (later[i].State == EntityState.Modified)
                {
                    later[i].Entry.CurrentValues.SetValues(values[i]);
                }
            }
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
            e.ToTable("teams"); e.HasKey(x => x.Id); e.HasAlternateKey(x => new { x.Id, x.SemesterId }); e.HasAlternateKey(x => new { x.Id, x.CourseCode });
            e.Property(x => x.MemberCount).ValueGeneratedOnAddOrUpdate().Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            e.Property(x => x.MemberCount).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); UpdatedAtByTrigger(e.Property(x => x.UpdatedAt)); Xmin(e.Property(x => x.Version));
        });
        modelBuilder.Entity<TeamDomain>(e => { e.ToTable("team_domains"); e.HasKey(x => new { x.TeamId, x.DomainId }); });
        modelBuilder.Entity<TeamMember>(e => { e.ToTable("team_members"); e.HasKey(x => x.Id); e.Property(x => x.JoinedAt).HasDefaultValueSql("now()"); Xmin(e.Property(x => x.Version)); });
        modelBuilder.Entity<LeaderVote>(e => { e.ToTable("leader_votes"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<LeaderVoteBallot>(e => { e.ToTable("leader_vote_ballots"); e.HasKey(x => new { x.VoteId, x.StudentId }); e.HasOne<LeaderVote>().WithMany().HasForeignKey(x => x.VoteId); });
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

        // Module của Thành (Projects, BaselineImport, ChangeRequests, TopicInterests, LecturerPreferences, LecturerAssignments, MentorAssignments) và Thiên (AdvisorMatching).
        modelBuilder.Entity<Project>(e => { e.ToTable("projects"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); UpdatedAtByTrigger(e.Property(x => x.UpdatedAt)); Xmin(e.Property(x => x.Version)); });
        modelBuilder.Entity<ProjectVersion>(e => { e.ToTable("project_versions"); e.HasKey(x => new { x.ProjectId, x.Version }); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<TeamBaselineMember>(e => { e.ToTable("team_baseline_members"); e.HasKey(x => new { x.TeamId, x.StudentId }); e.Property(x => x.ImportedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<ChangeRequest>(e => { e.ToTable("change_requests"); e.HasKey(x => x.Id); e.Property(x => x.ChangedPercent).HasPrecision(5, 2); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); UpdatedAtByTrigger(e.Property(x => x.UpdatedAt)); Xmin(e.Property(x => x.Version)); });
        modelBuilder.Entity<ChangeRequestItem>(e => { e.ToTable("change_request_items"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<TopicInterest>(e => { e.ToTable("topic_interests"); e.HasKey(x => x.Id); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); Xmin(e.Property(x => x.Version)); });
        modelBuilder.Entity<LecturerPreference>(e => { e.ToTable("lecturer_preferences"); e.HasKey(x => new { x.TeamId, x.Rank }); e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()"); });
        modelBuilder.Entity<AdvisorMatchingRun>(e => { e.ToTable("advisor_matching_runs"); e.HasKey(x => x.Id); e.Property(x => x.Plan).HasColumnType("jsonb"); e.Property(x => x.CreatedAt).HasDefaultValueSql("now()"); Xmin(e.Property(x => x.Version)); });
        modelBuilder.Entity<LecturerAssignment>(e => { e.ToTable("lecturer_assignments"); e.HasKey(x => x.Id); e.Property(x => x.AssignedAt).HasDefaultValueSql("now()"); Xmin(e.Property(x => x.Version)); });
        modelBuilder.Entity<MentorAssignment>(e => { e.ToTable("mentor_assignments"); e.HasKey(x => x.Id); e.Property(x => x.AssignedAt).HasDefaultValueSql("now()"); Xmin(e.Property(x => x.Version)); });

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
        modelBuilder.Entity<TeamEntity>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).HasConstraintName("fk_teams_project").OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProjectVersion>().HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).HasConstraintName("fk_project_versions_project").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TeamBaselineMember>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => x.TeamId).HasConstraintName("fk_team_baseline_members_team").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ChangeRequest>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => new { x.TeamId, x.CourseCode }).HasPrincipalKey(x => new { x.Id, x.CourseCode }).HasConstraintName("fk_change_requests_team").OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChangeRequestItem>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => x.TeamId).HasConstraintName("fk_change_request_items_team").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ChangeRequestItem>().HasOne<ChangeRequest>().WithMany().HasForeignKey(x => x.ChangeRequestId).HasConstraintName("fk_change_request_items_request").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TopicInterest>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => x.TeamId).HasConstraintName("fk_topic_interests_team").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LecturerPreference>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => x.TeamId).HasConstraintName("fk_lecturer_preferences_team").OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LecturerAssignment>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => x.TeamId).HasConstraintName("fk_lecturer_assignments_team").OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LecturerAssignment>().HasOne<AdvisorMatchingRun>().WithMany().HasForeignKey(x => x.MatchingRunId).HasConstraintName("fk_lecturer_assignments_run").OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<MentorAssignment>().HasOne<TeamEntity>().WithMany().HasForeignKey(x => x.TeamId).HasConstraintName("fk_mentor_assignments_team").OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MentorAssignment>().HasOne<AdvisorMatchingRun>().WithMany().HasForeignKey(x => x.MatchingRunId).HasConstraintName("fk_mentor_assignments_run").OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>Cột hệ thống <c>xmin</c> của PostgreSQL làm concurrency token (BE-24).</summary>
    private static void Xmin(PropertyBuilder<uint> property) =>
        property.HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

    /// <summary>Trigger <c>set_updated_at</c> ghi cột này; EF đọc lại sau khi lưu.</summary>
    private static void UpdatedAtByTrigger(PropertyBuilder<DateTimeOffset> property) =>
        property.HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
}
