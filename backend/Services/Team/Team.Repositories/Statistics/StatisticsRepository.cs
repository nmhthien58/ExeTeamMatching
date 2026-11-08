using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.ChangeRequests;
using Team.Repositories.Context;
using Team.Repositories.Members;
using Team.Repositories.Teams;

namespace Team.Repositories.Statistics;

/// <summary>Số liệu của nhóm cho dashboard W02 (phần Team biết; sinh viên chưa có nhóm tính ở Services).</summary>
public sealed record TeamStatistics(
    int StudentsWithTeam, IReadOnlyDictionary<int, int> TeamsBySize, int PendingChangeRequests, int TeamsNeedingReview,
    IReadOnlyDictionary<int, int> MembersBySpecialization, IReadOnlyDictionary<string, int> TeamsByDomain);

/// <summary>Truy vấn của module Statistics (BE-50). Nhóm đã giải tán không tính.</summary>
public interface IStatisticsRepository
{
    Task<TeamStatistics> GetAsync(Guid semesterId, string? courseCode, CancellationToken ct);

    /// <summary>Nhóm đang lập (Nháp / Hoạt động) dưới <paramref name="minSize"/> người – nhắc hạn (DeadlineReminderJob).</summary>
    Task<List<Guid>> ListTeamsUnderSizeAsync(Guid semesterId, string courseCode, int minSize, CancellationToken ct);

    /// <summary>Ghi sự kiện exe.team của học kỳ vào outbox (key = semesterId).</summary>
    void AddSemesterEvent<TPayload>(string eventType, Guid semesterId, TPayload payload);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class StatisticsRepository(TeamDbContext db) : IStatisticsRepository
{
    public async Task<TeamStatistics> GetAsync(Guid semesterId, string? courseCode, CancellationToken ct)
    {
        var teams = db.Set<TeamEntity>().AsNoTracking()
            .Where(t => t.SemesterId == semesterId && t.Status != "DISBANDED" && (courseCode == null || t.CourseCode == courseCode));
        var members = db.Set<TeamMember>().AsNoTracking().Where(m => teams.Any(t => t.Id == m.TeamId));

        return new TeamStatistics(
            await members.CountAsync(ct),
            await teams.Where(t => t.MemberCount > 0).GroupBy(t => t.MemberCount)
                .ToDictionaryAsync(g => (int)g.Key, g => g.Count(), ct),
            await db.Set<ChangeRequest>().AsNoTracking().CountAsync(r => r.Status == "PENDING" && teams.Any(t => t.Id == r.TeamId), ct),
            await teams.CountAsync(t => t.Status == "NEEDS_REVIEW", ct),
            await members.Where(m => m.SpecializationId != null).GroupBy(m => m.SpecializationId!.Value).ToDictionaryAsync(g => g.Key, g => g.Count(), ct),
            await db.Set<TeamDomain>().AsNoTracking().Where(d => teams.Any(t => t.Id == d.TeamId)).GroupBy(d => d.DomainCode)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), ct));
    }

    public Task<List<Guid>> ListTeamsUnderSizeAsync(Guid semesterId, string courseCode, int minSize, CancellationToken ct) =>
        db.Set<TeamEntity>().AsNoTracking()
            .Where(t => t.SemesterId == semesterId && t.CourseCode == courseCode && (t.Status == "DRAFT" || t.Status == "ACTIVE")
                && t.MemberCount < minSize)
            .OrderBy(t => t.Name)
            .Select(t => t.Id)
            .ToListAsync(ct);

    public void AddSemesterEvent<TPayload>(string eventType, Guid semesterId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Team, eventType, "Semester", semesterId, payload);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
