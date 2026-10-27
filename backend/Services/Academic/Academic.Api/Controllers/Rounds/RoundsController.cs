using Academic.Services.Rounds;
using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academic.Api.Controllers.Rounds;

/// <summary>Đợt đăng ký: W04, banner S01, L01 (BE-16) – academic.yaml tag Rounds.</summary>
[ApiController]
[Authorize]
[Route("api/registration-rounds")]
public sealed class RoundsController(IRoundService rounds) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<RoundResponse>> List([FromQuery] RoundQuery query, CancellationToken ct) => rounds.ListAsync(query, ct);

    /// <summary>Đợt đang mở, lọc theo môn nếu có.</summary>
    [HttpGet("active")]
    public Task<IReadOnlyList<RoundResponse>> Active([FromQuery] string? courseCode, CancellationToken ct) => rounds.ListOpenAsync(courseCode, ct);

    /// <summary>Tạo đợt ở trạng thái SCHEDULED; Admin mở bằng <c>POST /{id}/open</c>.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<RoundResponse>> Create(RoundInput input, CancellationToken ct)
    {
        var round = await rounds.CreateAsync(input, User, ct);
        return Created($"/api/registration-rounds/{round.Id}", round);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    public Task<RoundResponse> Update(Guid id, RoundInput input, CancellationToken ct) => rounds.UpdateAsync(id, input, ct);

    [HttpPost("{id:guid}/open")]
    [Authorize(Policy = Policies.Admin)]
    public Task<RoundResponse> Open(Guid id, CancellationToken ct) => rounds.OpenAsync(id, ct);

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = Policies.Admin)]
    public Task<RoundResponse> Close(Guid id, CancellationToken ct) => rounds.CloseAsync(id, ct);
}
