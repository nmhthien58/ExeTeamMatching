using Team.Repositories.Teams;
using Team.Services.Teams;

namespace Team.Services.Academic;

/// <summary>Kiểm tra học kỳ, sinh viên và đợt đang mở qua Academic – dùng chung cho các module của Team Service (rule 4, 5, 6, 18).</summary>
public sealed class RoundGuard(IAcademicClient academic)
{
    /// <summary>Quy tắc lập nhóm hiện hành (W05).</summary>
    public Task<Teams.TeamRuleSet> TeamRulesAsync(CancellationToken ct) => academic.GetTeamRulesAsync(ct);

    /// <summary>Học kỳ hiện hành và snapshot của sinh viên; 403 not-in-list nếu không thuộc học kỳ (rule 5).</summary>
    public async Task<(SemesterDto Semester, StudentSnapshot Student)> ResolveStudentAsync(Guid studentId, CancellationToken ct)
    {
        var semester = await academic.GetCurrentSemesterAsync(ct) ?? throw TeamErrors.RoundClosed();
        return (semester, await LookupAsync(semester.Id, studentId, ct));
    }

    /// <summary>Snapshot của một sinh viên trong học kỳ; 403 not-in-list nếu không có.</summary>
    public async Task<StudentSnapshot> LookupAsync(Guid semesterId, Guid studentId, CancellationToken ct)
    {
        var student = (await academic.LookupStudentsAsync(semesterId, [studentId], ct)).FirstOrDefault(s => s.Id == studentId);
        return student is { IsEnrolled: true } ? student : throw TeamErrors.NotInList();
    }

    /// <summary>Rule 18: còn đợt lập nhóm đang mở của môn trong học kỳ thì đang lập nhóm, ngược lại học kỳ đã bắt đầu.</summary>
    public async Task<string> PhaseAsync(string courseCode, Guid semesterId, CancellationToken ct) =>
        await IsOpenAsync(courseCode, RoundTypes.TeamFormation, semesterId, ct) ? Phases.TeamFormation : Phases.SemesterRunning;

    /// <summary>409 round-closed nếu đợt lập nhóm của môn không mở (rule 6, 18).</summary>
    public async Task RequireTeamFormationAsync(string courseCode, Guid semesterId, CancellationToken ct)
    {
        if (!await IsOpenAsync(courseCode, RoundTypes.TeamFormation, semesterId, ct))
        {
            throw TeamErrors.RoundClosed();
        }
    }

    /// <summary>
    /// Đổi thành viên (rule 6): nhóm thường cần đợt lập nhóm mở và nhóm còn Nháp / Hoạt động;
    /// nhóm EXE201 kế thừa được đổi cả trong đợt đổi nhóm (rule 7).
    /// </summary>
    public async Task RequireMembershipRoundAsync(TeamEntity team, CancellationToken ct)
    {
        if (team.IsInherited)
        {
            if (!await IsOpenAsync(team.CourseCode, RoundTypes.TeamFormation, team.SemesterId, ct)
                && !await IsOpenAsync(team.CourseCode, RoundTypes.ChangeRequest, team.SemesterId, ct))
            {
                throw TeamErrors.RoundClosed();
            }
            return;
        }
        if (team.Status is not (TeamStatuses.Draft or TeamStatuses.Active))
        {
            throw TeamErrors.RoundClosed();
        }
        await RequireTeamFormationAsync(team.CourseCode, team.SemesterId, ct);
    }

    private async Task<bool> IsOpenAsync(string courseCode, string roundType, Guid semesterId, CancellationToken ct) =>
        await academic.GetOpenRoundAsync(courseCode, roundType, ct) is { } round && round.SemesterId == semesterId;
}
