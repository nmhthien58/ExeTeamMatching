using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.AdvisorAssignments;

namespace Team.Api.Controllers.LecturerAssignments;

[ApiController]
[Authorize]
public sealed class LecturerAssignmentsController(IAdvisorAssignmentService advisorService) : ControllerBase
{
    /// <summary>
    /// Giảng viên hướng dẫn của nhóm và lý do ghép (S15, S25). null khi chưa ghép.
    /// </summary>
    [HttpGet("api/teams/{teamId:guid}/lecturer")]
    public async Task<ActionResult<LecturerAssignmentResponse?>> GetTeamLecturer(Guid teamId, CancellationToken ct)
    {
        var result = await advisorService.GetTeamLecturerAsync(teamId, User, ct);
        return Ok(result);
    }

    /// <summary>
    /// Nhóm tôi hướng dẫn (L01 số liệu, L04) kèm lý do ghép; nhóm tôi làm mentor có nhãn "Mentor".
    /// </summary>
    [HttpGet("api/lecturer-teams/me")]
    [Authorize(Policy = Policies.Lecturer)]
    public async Task<ActionResult<LecturerTeamsResponse>> GetLecturerTeamsMe(
        [FromQuery] string? courseCode,
        [FromQuery] string? classCode,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await advisorService.GetLecturerTeamsMeAsync(courseCode, classCode, search, User, ct);
        return Ok(result);
    }
}
