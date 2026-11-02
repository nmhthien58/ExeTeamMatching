using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.TopicInterests;

namespace Team.Api.Controllers.TopicInterests;

/// <summary>Gợi ý đề tài cho cố vấn (rule 19, L07) – team.yaml tag TopicInterests.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class TopicRecommendationsController(ITopicInterestService service) : ControllerBase
{
    /// <summary>Đề tài cho giảng viên hoặc mentor (L07 tab Gợi ý).</summary>
    [HttpGet("topic-recommendations/me")]
    public async Task<ActionResult<TopicRecommendationsResponse>> GetRecommendations(
        [FromQuery] string advisorKind,
        [FromQuery] string? courseCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await service.GetRecommendationsAsync(advisorKind, courseCode, page, pageSize, User, ct));
}
