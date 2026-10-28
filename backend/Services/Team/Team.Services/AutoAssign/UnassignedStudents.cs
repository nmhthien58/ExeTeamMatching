using Team.Repositories.AutoAssign;
using Team.Services.Matching;

namespace Team.Services.AutoAssign;

/// <summary>
/// Sinh viên của môn trong học kỳ chưa có nhóm (W20, W21, thống kê, nhắc hạn).
/// </summary>
/// <remarks>
/// shortcut: Team không có danh sách sinh viên của học kỳ – academic.yaml chỉ cho tra theo id (<c>/internal/students/lookup</c>),
/// <c>GET /api/students</c> chưa có. Matching giữ read model sinh viên (exe.student, exe.team) và mọi sinh viên chưa có nhóm đều nằm trong
/// phương án PlanAutoAssignment (bù + nhóm mới + còn lại). Read model đi sau Kafka một chút nên lọc lại theo team_members.
/// Đổi sang danh sách của Academic khi có endpoint nội bộ liệt kê sinh viên học kỳ.
/// </remarks>
public sealed class UnassignedStudents(IMatchingClient matching, IAutoAssignRepository repository)
{
    public async Task<IReadOnlyList<Guid>> ListAsync(Guid semesterId, string courseCode, CancellationToken ct)
    {
        var ids = (await matching.PlanAutoAssignmentAsync(semesterId, courseCode, ct)).AllStudentIds.ToList();
        var inTeam = await repository.StudentsInTeamAsync(semesterId, ids, ct);
        return ids.Where(id => !inTeam.Contains(id)).ToList();
    }
}
