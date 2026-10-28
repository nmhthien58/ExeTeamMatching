using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.Posts;
using Team.Services.Teams;

namespace Team.Api.Controllers.Posts;

/// <summary>Bài tuyển thành viên S21, bài tìm nhóm S10, S11 (BE-31, rule 18) – team.yaml tag Posts.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class PostsController(IPostService posts) : ControllerBase
{
    /// <summary>Trưởng nhóm tạo hoặc sửa bài tuyển; mỗi nhóm tối đa 1 bài mở.</summary>
    [HttpPut("teams/{teamId:guid}/recruitment-post")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<RecruitmentPostResponse>> UpsertRecruitment(Guid teamId, RecruitmentPostInput input, CancellationToken ct) =>
        Ok(await posts.UpsertRecruitmentAsync(teamId, input, User, ct));

    [HttpDelete("teams/{teamId:guid}/recruitment-post")]
    [Authorize(Policy = Policies.Student)]
    public async Task<IActionResult> CloseRecruitment(Guid teamId, CancellationToken ct)
    {
        await posts.CloseRecruitmentAsync(teamId, User, ct);
        return NoContent();
    }

    [HttpGet("seeking-posts")]
    public async Task<ActionResult<PagedResponse<SeekingPostResponse>>> ListSeeking([FromQuery] SeekingPostQuery query, CancellationToken ct) =>
        Ok(await posts.ListSeekingAsync(query, User, ct));

    [HttpGet("seeking-posts/me")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<SeekingPostResponse>> MySeeking(CancellationToken ct) => Ok(await posts.GetMySeekingAsync(User, ct));

    [HttpPut("seeking-posts/me")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<SeekingPostResponse>> UpsertMySeeking(SeekingPostInput input, CancellationToken ct) =>
        Ok(await posts.UpsertMySeekingAsync(input, User, ct));

    [HttpDelete("seeking-posts/me")]
    [Authorize(Policy = Policies.Student)]
    public async Task<IActionResult> CloseMySeeking(CancellationToken ct)
    {
        await posts.CloseMySeekingAsync(User, ct);
        return NoContent();
    }
}
