using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.AutoAssign;

namespace Team.Api.Controllers.AutoAssign;

/// <summary>Ghép tự động: W20, W21 (BE-49) – team.yaml tag Admin.</summary>
[ApiController]
[Authorize(Policy = Policies.Admin)]
[Route("api/auto-assignment")]
public sealed class AutoAssignController(IAutoAssignService autoAssign) : ControllerBase
{
    /// <summary>Chạy lại ghép tự động: Team gọi Matching.PlanAutoAssignment rồi áp dụng.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<AutoAssignResultResponse>> Run(AutoAssignRunRequest request, CancellationToken ct) =>
        Ok(await autoAssign.RunAsync(request, User, ct));

    /// <summary>Kết quả ghép gần nhất; 404 khi chưa ghép lần nào.</summary>
    [HttpGet("result")]
    public async Task<ActionResult<AutoAssignResultResponse>> Result([FromQuery] AutoAssignResultQuery query, CancellationToken ct) =>
        Ok(await autoAssign.GetResultAsync(query, ct));
}

/// <summary>Endpoint nội bộ cho Job Service (token dịch vụ, không qua Gateway).</summary>
[ApiController]
[Authorize(Policy = Policies.Internal)]
[Route("internal/teams/auto-assign")]
public sealed class InternalAutoAssignController(IAutoAssignService autoAssign) : ControllerBase
{
    /// <summary>AutoAssignUnmatchedJob – áp dụng phương án PlanAutoAssignment (rule 14).</summary>
    [HttpPost("apply")]
    public async Task<ActionResult<AutoAssignResultResponse>> Apply(AutoAssignApplyRequest request, CancellationToken ct) =>
        Ok(await autoAssign.ApplyAsync(request, ct));
}
