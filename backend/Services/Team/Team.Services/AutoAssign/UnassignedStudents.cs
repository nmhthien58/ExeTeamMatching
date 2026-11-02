using Team.Repositories.AutoAssign;
using Team.Services.Academic;

namespace Team.Services.AutoAssign;

/// <summary>
/// Sinh viên của môn trong học kỳ chưa có nhóm (W20, W21, thống kê, nhắc hạn): danh sách học kỳ của Academic
/// (<c>GET /internal/students</c>) trừ sinh viên đã có dòng trong team_members.
/// </summary>
public sealed class UnassignedStudents(IAcademicClient academic, IAutoAssignRepository repository)
{
    public async Task<IReadOnlyList<Guid>> ListAsync(Guid semesterId, string courseCode, CancellationToken ct)
    {
        var ids = (await academic.ListSemesterStudentsAsync(semesterId, courseCode, ct)).Where(s => s.IsEnrolled).Select(s => s.Id).ToList();
        var inTeam = await repository.StudentsInTeamAsync(semesterId, ids, ct);
        return ids.Where(id => !inTeam.Contains(id)).ToList();
    }
}
