using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.Teams;

namespace Team.Api.Controllers.Teams;

/// <summary>Nhóm: S01, S05, S08, S13–S16, W15, W16 (BE-25, BE-26, BE-27) – team.yaml tag Teams.</summary>
[ApiController]
[Authorize]
[Route("api/teams")]
public sealed class TeamsController(ITeamService teams) : ControllerBase
{
    /// <summary>Tìm kiếm, lọc, sắp xếp, phân trang nhóm.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TeamCardResponse>>> Search([FromQuery] TeamQuery query, CancellationToken ct) =>
        Ok(await teams.SearchAsync(query, User, ct));

    /// <summary>Tạo nhóm, người tạo là trưởng nhóm.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<TeamDetailResponse>> Create(TeamInput input, CancellationToken ct)
    {
        var team = await teams.CreateAsync(input, User, ct);
        return Created($"/api/teams/{team.Id}", team);
    }

    [HttpGet("me")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<TeamDetailResponse>> Mine(CancellationToken ct) => Ok(await teams.GetMineAsync(User, ct));

    [HttpGet("me/home")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<TeamHomeResponse>> Home(CancellationToken ct) => Ok(await teams.GetHomeAsync(User, ct));

    [HttpGet("{teamId:guid}")]
    public async Task<ActionResult<TeamDetailResponse>> Get(Guid teamId, CancellationToken ct) => Ok(await teams.GetAsync(teamId, User, ct));

    /// <summary>Trưởng nhóm (trong đợt lập nhóm) hoặc Admin sửa tên nhóm, đề tài, domain.</summary>
    [HttpPut("{teamId:guid}")]
    public async Task<ActionResult<TeamDetailResponse>> Update(Guid teamId, TeamInput input, CancellationToken ct) =>
        Ok(await teams.UpdateAsync(teamId, input, User, ct));
}
