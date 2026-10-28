using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.Members;
using Team.Repositories.Teams;

namespace Team.Repositories.Finalization;

/// <summary>Truy vấn của module Finalization (BE-48); thêm / xóa / phát sự kiện dùng <see cref="ITeamRepository"/>, <see cref="IMemberRepository"/>.</summary>
public interface IFinalizationRepository
{
    /// <summary>Nhóm của một môn trong học kỳ ở các trạng thái cho trước (được theo dõi).</summary>
    Task<List<TeamEntity>> ListTeamsAsync(Guid semesterId, string courseCode, IReadOnlyCollection<string> statuses, CancellationToken ct);

    /// <summary>Thành viên của nhiều nhóm (không theo dõi), theo nhóm.</summary>
    Task<ILookup<Guid, TeamMember>> ListMembersAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct);

    /// <summary>
    /// Chạy nhiều lần SaveChanges trong một transaction – chuyển sinh viên giữa hai nhóm phải xóa dòng cũ trước khi thêm dòng mới
    /// (<c>uq_team_members_semester_student</c>, một trưởng nhóm). Lỗi trigger DEFERRABLE lúc COMMIT đổi sang ProblemDetails như SaveChanges.
    /// </summary>
    Task InTransactionAsync(Func<Task> work, CancellationToken ct);
}

public sealed class FinalizationRepository(TeamDbContext db) : IFinalizationRepository
{
    public Task<List<TeamEntity>> ListTeamsAsync(Guid semesterId, string courseCode, IReadOnlyCollection<string> statuses, CancellationToken ct) =>
        db.Set<TeamEntity>()
            .Where(t => t.SemesterId == semesterId && t.CourseCode == courseCode && statuses.Contains(t.Status))
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<ILookup<Guid, TeamMember>> ListMembersAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct) =>
        (await db.Set<TeamMember>().AsNoTracking().Where(m => teamIds.Contains(m.TeamId)).ToListAsync(ct)).ToLookup(m => m.TeamId);

    public async Task InTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await work();
        try
        {
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (TeamDbErrors.ToAppException(ex) is { } mapped)
        {
            throw mapped;
        }
    }
}
