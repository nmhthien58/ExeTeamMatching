using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Team.Repositories.AuditLog;
using Team.Repositories.Statistics;
using Team.Services.Academic;
using Team.Services.AutoAssign;
using Team.Services.Teams;

namespace Team.Services.Statistics;

public interface IStatisticsService
{
    /// <summary>W02 – số liệu dashboard của Bộ môn.</summary>
    Task<StatisticsOverviewResponse> GetOverviewAsync(StatisticsQuery query, CancellationToken ct);

    /// <summary>W20 – sinh viên chưa có nhóm của học kỳ hiện hành, lọc chuyên ngành, tìm MSSV / tên.</summary>
    Task<PagedResponse<StudentRef>> ListUnmatchedAsync(UnmatchedStudentsQuery query, CancellationToken ct);

    /// <summary>DeadlineReminderJob – sinh viên chưa có nhóm và nhóm đang lập dưới 4 người.</summary>
    Task<DeadlineReminderTargets> GetDeadlineReminderTargetsAsync(DeadlineReminderTargetsQuery query, CancellationToken ct);

    /// <summary>W20 – Admin nhắc sinh viên chưa có nhóm của học kỳ hiện hành; trả số sinh viên được nhắc.</summary>
    Task<int> RemindUnmatchedAsync(RemindUnmatchedRequest request, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Module Statistics của Team Service (BE-50).</summary>
public sealed class StatisticsService(
    IStatisticsRepository repository, IAcademicClient academic, UnassignedStudents unassigned, TeamReader reader, AuditLogWriter audit)
    : IStatisticsService
{
    public async Task<StatisticsOverviewResponse> GetOverviewAsync(StatisticsQuery query, CancellationToken ct)
    {
        var semesterId = query.SemesterId ?? await CurrentSemesterIdAsync(ct);
        var stats = await repository.GetAsync(semesterId, query.CourseCode, ct);
        var withoutTeam = (await UnassignedAsync(semesterId, query.CourseCode, ct)).Count;
        var catalogs = await reader.LoadCatalogsAsync(ct);
        return new StatisticsOverviewResponse(
            stats.StudentsWithTeam,
            withoutTeam,
            stats.TeamsBySize.OrderBy(s => s.Key).ToDictionary(s => s.Key.ToString(), s => s.Value),
            stats.PendingChangeRequests,
            stats.TeamsNeedingReview,
            stats.MembersBySpecialization
                .Select(s => new CodeCount(catalogs.Specialization(s.Key).Code ?? s.Key.ToString(), s.Value))
                .OrderByDescending(s => s.Count).ThenBy(s => s.Code).ToList(),
            stats.TeamsByDomain.Select(d => new CodeCount(d.Key, d.Value)).OrderBy(d => d.Code).ToList());
    }

    public async Task<PagedResponse<StudentRef>> ListUnmatchedAsync(UnmatchedStudentsQuery query, CancellationToken ct)
    {
        var semesterId = await CurrentSemesterIdAsync(ct);
        var ids = await UnassignedAsync(semesterId, query.CourseCode, ct);
        var students = (ids.Count == 0 ? [] : await academic.LookupStudentsAsync(semesterId, ids, ct))
            .Where(s => s.IsEnrolled && (query.SpecializationId is null || s.SpecializationId == query.SpecializationId))
            .Where(s => string.IsNullOrWhiteSpace(query.Search)
                || s.StudentCode.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase)
                || s.FullName.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.FullName).ThenBy(s => s.StudentCode)
            .ToList();

        var catalogs = await reader.LoadCatalogsAsync(ct);
        var page = students.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(s => new StudentRef(s.Id, s.StudentCode, s.FullName, catalogs.Specialization(s.SpecializationId)))
            .ToList();
        return new PagedResponse<StudentRef>(page, query.Page, query.PageSize, students.Count, (students.Count + query.PageSize - 1) / query.PageSize);
    }

    public async Task<DeadlineReminderTargets> GetDeadlineReminderTargetsAsync(DeadlineReminderTargetsQuery query, CancellationToken ct)
    {
        var teams = new List<Guid>();
        foreach (var course in Courses(query.CourseCode))
        {
            teams.AddRange(await repository.ListTeamsUnderSizeAsync(query.SemesterId, course, TeamRules.MinSize, ct));
        }
        return new DeadlineReminderTargets(await UnassignedAsync(query.SemesterId, query.CourseCode, ct), teams);
    }

    public async Task<int> RemindUnmatchedAsync(RemindUnmatchedRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var semesterId = await CurrentSemesterIdAsync(ct);
        var actor = user.ToAuditActor();
        var reminded = 0;
        foreach (var course in Courses(request.CourseCode))
        {
            var studentIds = await unassigned.ListAsync(semesterId, course, ct);
            if (studentIds.Count == 0)
            {
                continue;
            }
            repository.AddSemesterEvent(StatisticsEventTypes.UnmatchedStudentsReminded, semesterId,
                new UnmatchedStudentsReminded(semesterId, course, studentIds, user.GetUserId()));
            audit.Add(null, "Semester", semesterId, "UNMATCHED_REMINDED", actor, newValues: new { courseCode = course, studentCount = studentIds.Count });
            reminded += studentIds.Count;
        }
        await repository.SaveChangesAsync(ct);
        return reminded;
    }

    private async Task<List<Guid>> UnassignedAsync(Guid semesterId, string? courseCode, CancellationToken ct)
    {
        var ids = new List<Guid>();
        foreach (var course in Courses(courseCode))
        {
            ids.AddRange(await unassigned.ListAsync(semesterId, course, ct));
        }
        return ids;
    }

    private static IReadOnlyList<string> Courses(string? courseCode) => courseCode is null ? CourseCodes.All : [courseCode];

    private async Task<Guid> CurrentSemesterIdAsync(CancellationToken ct) =>
        (await academic.GetCurrentSemesterAsync(ct))?.Id ?? throw new NotFoundException("học kỳ hiện hành");
}
