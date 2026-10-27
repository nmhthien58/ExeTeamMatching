using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.AdvisorAssignments;

namespace Team.Api.Controllers.MentorAssignments;

[ApiController]
[Authorize]
public sealed class MentorAssignmentsController(IAdvisorAssignmentService advisorService) : ControllerBase
{
    /// <summary>
    /// Mentor của nhóm (S15 mục Cố vấn). null nếu chưa ghép.
    /// </summary>
    [HttpGet("api/teams/{teamId:guid}/mentor")]
    public async Task<ActionResult<MentorAssignmentResponse?>> GetTeamMentor(Guid teamId, CancellationToken ct)
    {
        var result = await advisorService.GetTeamMentorAsync(teamId, User, ct);
        return Ok(result);
    }

    /// <summary>
    /// Nhóm tôi đang mentor (M01) kèm lý do ghép.
    /// </summary>
    [HttpGet("api/mentor-teams/me")]
    [Authorize(Policy = Policies.Advisor)]
    public async Task<ActionResult<IReadOnlyList<AdvisedTeamResponse>>> GetMentorTeamsMe(CancellationToken ct)
    {
        var result = await advisorService.GetMentorTeamsMeAsync(User, ct);
        return Ok(result);
    }
}
