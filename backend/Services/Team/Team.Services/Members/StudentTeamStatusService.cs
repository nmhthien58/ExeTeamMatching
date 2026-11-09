using BuildingBlocks.Errors;
using Team.Repositories.Teams;
using Team.Services.Academic;

namespace Team.Services.Members;

/// <summary>Schema <c>StudentTeamStatus</c> của <c>GET /internal/students/{id}/team-status</c>.</summary>
public sealed record StudentTeamStatus(Guid StudentId, Guid SemesterId, bool HasTeam);

/// <summary>Academic hỏi "sinh viên đã có nhóm ở học kỳ hiện hành chưa?" để chặn bật "Tôi đang tìm nhóm".</summary>
public sealed class StudentTeamStatusService(ITeamRepository teams, IAcademicClient academic)
{
    public async Task<StudentTeamStatus> GetAsync(Guid studentId, CancellationToken ct)
    {
        var semester = await academic.GetCurrentSemesterAsync(ct) ?? throw new NotFoundException("học kỳ hiện hành");
        return new StudentTeamStatus(studentId, semester.Id, await teams.IsStudentInTeamAsync(semester.Id, studentId, ct));
    }
}
