using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.AdvisorMatching;
using Team.Repositories.AuditLog;
using Team.Repositories.Context;
using Team.Services.AdvisorMatching;

namespace Team.Tests;

/// <summary>
/// <see cref="MentorAssignmentWriter"/> thật trên một <see cref="TeamDbContext"/> không kết nối: nhật ký và Outbox chỉ nằm trong
/// ChangeTracker để test đọc lại (không gọi SaveChanges).
/// </summary>
internal sealed class MentorWriterHarness
{
    private readonly TeamDbContext _db;

    private MentorWriterHarness(TeamDbContext db)
    {
        _db = db;
        Writer = new MentorAssignmentWriter(new AdvisorMatchingRepository(db), new AuditLogWriter(db), TimeProvider.System);
    }

    public MentorAssignmentWriter Writer { get; }

    public static MentorWriterHarness Create() => new(new TeamDbContext(new DbContextOptionsBuilder<TeamDbContext>()
        .UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options));

    public List<OutboxMessage> Outbox() => _db.ChangeTracker.Entries<OutboxMessage>().Select(e => e.Entity).ToList();

    public List<AuditLogEntry> Audits() => _db.ChangeTracker.Entries<AuditLogEntry>().Select(e => e.Entity).ToList();
}
