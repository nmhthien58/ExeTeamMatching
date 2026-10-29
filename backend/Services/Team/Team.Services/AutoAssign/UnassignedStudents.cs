using Team.Repositories.AutoAssign;
using Team.Services.Academic;
using Team.Services.Matching;

namespace Team.Services.AutoAssign;

/// <summary>
/// Sinh viên của môn trong học kỳ chưa có nhóm (W20, W21, thống kê, nhắc hạn): danh sách học kỳ của Academic
/// (<c>GET /internal/students</c>) trừ sinh viên đã có dòng trong team_members.
/// </summary>
/// <remarks>
/// shortcut: khi Academic chưa có endpoint đó (404), lấy danh sách từ read model của Matching – mọi sinh viên chưa có nhóm đều nằm trong
/// phương án PlanAutoAssignment (bù + nhóm mới + còn lại). Bỏ nhánh này khi Academic đã triển khai <c>/internal/students</c>.
/// </remarks>
public sealed class UnassignedStudents(IAcademicClient academic, IMatchingClient matching, IAutoAssignRepository repository)
{
    public async Task<IReadOnlyList<Guid>> ListAsync(Guid semesterId, string courseCode, CancellationToken ct)
    {
        var roster = await academic.ListSemesterStudentsAsync(semesterId, courseCode, ct);
        var ids = roster is not null
            ? roster.Where(s => s.IsEnrolled).Select(s => s.Id).ToList()
            : (await matching.PlanAutoAssignmentAsync(semesterId, courseCode, ct)).AllStudentIds.ToList();
        var inTeam = await repository.StudentsInTeamAsync(semesterId, ids, ct);
        return ids.Where(id => !inTeam.Contains(id)).ToList();
    }
}
