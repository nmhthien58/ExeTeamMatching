using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.TopicInterests;

namespace Team.Api.Controllers.TopicInterests;

/// <summary>Quan tâm đề tài của cố vấn (rule 19, L07, S23) – team.yaml tag TopicInterests.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class TopicInterestsController(ITopicInterestService service) : ControllerBase
{
    /// <summary>Đề tài tôi đang quan tâm (L07 tab Đã quan tâm).</summary>
    [HttpGet("topic-interests/me")]
    public async Task<ActionResult<IReadOnlyList<TopicInterestResponse>>> GetMyInterests(
        [FromQuery] string advisorKind,
        CancellationToken ct) =>
        Ok(await service.GetMyInterestsAsync(advisorKind, User, ct));

    /// <summary>Cố vấn đang quan tâm nhóm (S23 – thành viên thấy tên giảng viên; Admin W19, W28 thấy tất cả).</summary>
    [HttpGet("teams/{teamId:guid}/topic-interests")]
    public async Task<ActionResult<IReadOnlyList<TopicInterestResponse>>> GetTeamInterests(
        Guid teamId,
        [FromQuery] string? advisorKind,
        CancellationToken ct) =>
        Ok(await service.GetTeamInterestsAsync(teamId, advisorKind, User, ct));

    /// <summary>Giảng viên / mentor quan tâm nhóm (L07). Không giữ chỗ.</summary>
    [HttpPost("teams/{teamId:guid}/topic-interests")]
    public async Task<ActionResult<TopicInterestResponse>> OpenInterest(
        Guid teamId,
        [FromBody] OpenTopicInterestInput input,
        CancellationToken ct)
    {
        var interest = await service.OpenInterestAsync(teamId, input, User, ct);
        return StatusCode(201, interest);
    }

    /// <summary>Rút quan tâm (L07).</summary>
    [HttpDelete("topic-interests/{id:guid}")]
    public async Task<IActionResult> WithdrawInterest(Guid id, CancellationToken ct)
    {
        await service.WithdrawInterestAsync(id, User, ct);
        return NoContent();
    }
}
