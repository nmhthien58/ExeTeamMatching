using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.Members;
using Team.Repositories.Teams;

namespace Team.Repositories.AutoAssign;

/// <summary>Kết quả ghép tự động đọc lại từ dữ liệu nhóm: nhóm tạo mới có <c>is_auto_assigned</c>, người được bù có <c>join_source = AUTO_ASSIGN</c>.</summary>
public sealed record AutoAssignSnapshot(DateTimeOffset RunAt, IReadOnlyList<Guid> NewTeamIds, IReadOnlyList<Guid> FilledTeamIds);

/// <summary>Truy vấn của module AutoAssign (BE-49).</summary>
public interface IAutoAssignRepository
{
    /// <summary>Sinh viên trong <paramref name="studentIds"/> đã thuộc một nhóm của học kỳ.</summary>
    Task<HashSet<Guid>> StudentsInTeamAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct);

    /// <summary>Lần ghép tự động gần nhất của môn trong học kỳ; null nếu chưa ghép lần nào.</summary>
    Task<AutoAssignSnapshot?> GetSnapshotAsync(Guid semesterId, string? courseCode, CancellationToken ct);
}

public sealed class AutoAssignRepository(TeamDbContext db) : IAutoAssignRepository
{
    public const string JoinSource = "AUTO_ASSIGN";

    public async Task<HashSet<Guid>> StudentsInTeamAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
        (await db.Set<TeamMember>().AsNoTracking()
            .Where(m => m.SemesterId == semesterId && studentIds.Contains(m.StudentId))
            .Select(m => m.StudentId)
            .ToListAsync(ct)).ToHashSet();

    public async Task<AutoAssignSnapshot?> GetSnapshotAsync(Guid semesterId, string? courseCode, CancellationToken ct)
    {
        var teams = db.Set<TeamEntity>().AsNoTracking()
            .Where(t => t.SemesterId == semesterId && t.Status != "DISBANDED" && (courseCode == null || t.CourseCode == courseCode));
        var autoMembers = db.Set<TeamMember>().AsNoTracking()
            .Where(m => m.JoinSource == JoinSource && teams.Any(t => t.Id == m.TeamId));

        var runAt = await autoMembers.MaxAsync(m => (DateTimeOffset?)m.JoinedAt, ct);
        if (runAt is null)
        {
            return null;
        }
        var newTeams = await teams.Where(t => t.IsAutoAssigned).OrderBy(t => t.Name).Select(t => t.Id).ToListAsync(ct);
        var filled = await teams.Where(t => !t.IsAutoAssigned && autoMembers.Any(m => m.TeamId == t.Id))
            .OrderBy(t => t.Name).Select(t => t.Id).ToListAsync(ct);
        return new AutoAssignSnapshot(runAt.Value, newTeams, filled);
    }
}
