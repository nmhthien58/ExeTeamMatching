using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.AdvisorMatching;
using Team.Repositories.AuditLog;
using Team.Repositories.Context;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Teams;
using Team.Services.AdvisorAssignments;
using Team.Services.AdvisorMatching;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>
/// BE-74 trên team_db thật (bật bằng <c>EXE_INTEGRATION=1</c>, chuỗi kết nối qua <c>EXE_TEAM_DB</c> như <see cref="TeamDbIntegrationTests"/>):
/// câu đếm số nhóm của mentor bỏ nhóm DISBANDED, và gỡ mentor khi bị khóa lưu dòng gán, nhật ký, Outbox cùng một lần.
/// </summary>
public sealed class MentorAssignmentDbIntegrationTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly Guid _semesterId = Guid.NewGuid();
    private readonly Guid _otherSemesterId = Guid.NewGuid();
    private readonly Guid _mentorId = Guid.NewGuid();
    private readonly List<Guid> _teamIds = [];

    private static TeamDbContext NewDb() => new(new DbContextOptionsBuilder<TeamDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("EXE_TEAM_DB")
            ?? "Host=localhost;Port=5432;Database=team_db;Username=team_svc;Password=team_dev_pw")
        .UseSnakeCaseNamingConvention().Options);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!Enabled)
        {
            return;
        }
        await using var db = NewDb();
        await db.Set<MentorAssignment>().Where(a => a.MentorId == _mentorId).ExecuteDeleteAsync();
        await db.Set<OutboxMessage>().Where(m => _teamIds.Contains(m.AggregateId)).ExecuteDeleteAsync();
        await db.Set<TeamEntity>().Where(t => t.SemesterId == _semesterId || t.SemesterId == _otherSemesterId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Count_excludes_disbanded_teams_and_filters_semester()
    {
        if (!Enabled)
        {
            return;
        }

        await AssignAsync(_semesterId, "ACTIVE");
        await AssignAsync(_semesterId, "DRAFT");
        await AssignAsync(_semesterId, "DISBANDED");
        await AssignAsync(_otherSemesterId, "ACTIVE");
        await AssignAsync(_otherSemesterId, "DISBANDED");

        await using var db = NewDb();
        var repo = new MentorAssignmentRepository(db);
        var other = Guid.NewGuid();

        var inSemester = await repo.CountActiveTeamsByMentorIdsAsync([_mentorId, other], _semesterId, CancellationToken.None);
        Assert.Equal(2, inSemester[_mentorId]);
        Assert.False(inSemester.ContainsKey(other));

        var all = await repo.CountActiveTeamsByMentorIdsAsync([_mentorId], null, CancellationToken.None);
        Assert.Equal(3, all[_mentorId]);
    }

    [Fact]
    public async Task Deactivation_saves_ended_row_audit_and_outbox_once()
    {
        if (!Enabled)
        {
            return;
        }

        var teamId = await AssignAsync(_semesterId, "ACTIVE");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var db = NewDb();
            var mentorRepo = new MentorAssignmentRepository(db);
            var teams = new TeamRepository(db);
            var writer = new MentorAssignmentWriter(new AdvisorMatchingRepository(db), new AuditLogWriter(db), TimeProvider.System);
            var service = new AdvisorAssignmentService(new LecturerAssignmentRepository(db), mentorRepo, teams,
                new TeamReader(teams, new FakeAcademic()), writer);

            var count = await service.HandleMentorRegisteredAsync(new MentorRegisteredEvent(_mentorId, IsActive: false), CancellationToken.None);
            Assert.Equal(attempt == 0 ? 1 : 0, count);
        }

        await using var check = NewDb();
        var row = await check.Set<MentorAssignment>().SingleAsync(a => a.MentorId == _mentorId);
        Assert.Equal(("ENDED", "MENTOR_DEACTIVATED"), (row.Status, row.EndReason));
        var log = await check.Set<AuditLogEntry>().SingleAsync(a => a.EntityId == row.Id);
        Assert.Equal(("MENTOR_UNASSIGNED", "System", teamId), (log.Action, log.ActorRole, log.TeamId!.Value));
        var outbox = await check.Set<OutboxMessage>()
            .Where(m => m.EventType == "MentorUnassigned" && m.AggregateId == teamId).ToListAsync();
        Assert.Single(outbox);
    }

    private async Task<Guid> AssignAsync(Guid semesterId, string status)
    {
        await using var db = NewDb();
        var team = new TeamEntity
        {
            Id = Guid.NewGuid(), SemesterId = semesterId, CourseCode = "EXE101", Name = $"Nhóm mentor {Guid.NewGuid():N}", Status = status,
        };
        db.Add(team);
        db.Add(new MentorAssignment
        {
            Id = Guid.NewGuid(), TeamId = team.Id, MentorId = _mentorId, MentorName = "Mentor thử", Status = "ASSIGNED",
            Reason = "DOMAIN", Source = "AUTO", AssignedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        _teamIds.Add(team.Id);
        return team.Id;
    }
}
