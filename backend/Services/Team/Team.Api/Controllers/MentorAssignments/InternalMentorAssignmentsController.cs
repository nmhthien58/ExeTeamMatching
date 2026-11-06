using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.AdvisorAssignments;

namespace Team.Api.Controllers.MentorAssignments;

/// <summary>
/// Endpoint nội bộ cho Academic Service và Job Service (BE-74).
/// </summary>
[ApiController]
[Authorize(Policy = Policies.Internal)]
[Route("internal/mentors")]
public sealed class InternalMentorAssignmentsController(IAdvisorAssignmentService advisorService) : ControllerBase
{
    /// <summary>
    /// Đếm số nhóm đang mentor cho danh sách mentor (tránh N+1 từ Academic W27, lọc hasCapacity, chặn max-below-current).
    /// Mentor không có nhóm nào trả 0.
    /// </summary>
    [HttpPost("team-counts")]
    public async Task<ActionResult<IReadOnlyList<MentorTeamCountResponse>>> GetTeamCounts(
        [FromBody] MentorTeamCountsRequest request,
        CancellationToken ct)
    {
        var result = await advisorService.GetMentorTeamCountsAsync(request.MentorIds, request.SemesterId, ct);
        return Ok(result);
    }
}
