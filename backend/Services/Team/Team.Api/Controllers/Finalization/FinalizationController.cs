using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Team.Services.Finalization;
using Team.Services.Teams;

namespace Team.Api.Controllers.Finalization;

/// <summary>Admin chốt / mở lại nhóm, thêm, bớt và chuyển thành viên: W16, W21, W22 (BE-48) – team.yaml tag Admin.</summary>
[ApiController]
[Authorize(Policy = Policies.Admin)]
[Route("api/teams/{teamId:guid}")]
public sealed class FinalizationController(IFinalizationService finalization) : ControllerBase
{
    [HttpPost("members")]
    public async Task<ActionResult<TeamDetailResponse>> AddMember(Guid teamId, AdminAddMemberRequest request, CancellationToken ct) =>
        Ok(await finalization.AddMemberAsync(teamId, request, User, ct));

    /// <summary>Bớt thành viên khỏi nhóm; body (lý do) không bắt buộc. Bớt người cuối cùng thì nhóm bị giải tán.</summary>
    [HttpPost("members/{studentId:guid}/remove")]
    public async Task<ActionResult<TeamDetailResponse>> RemoveMember(Guid teamId, Guid studentId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AdminRemoveMemberRequest? request, CancellationToken ct) =>
        Ok(await finalization.RemoveMemberAsync(teamId, studentId, request ?? new AdminRemoveMemberRequest(null), User, ct));

    [HttpPost("members/{studentId:guid}/move")]
    public async Task<IActionResult> MoveMember(Guid teamId, Guid studentId, MoveMemberRequest request, CancellationToken ct)
    {
        await finalization.MoveMemberAsync(teamId, studentId, request, User, ct);
        return NoContent();
    }

    /// <summary>Kiểm tra rule 1, 2; 409 composition-invalid nếu chưa đủ.</summary>
    [HttpPost("finalize")]
    public async Task<ActionResult<TeamDetailResponse>> Finalize(Guid teamId, CancellationToken ct) =>
        Ok(await finalization.FinalizeAsync(teamId, User, ct));

    [HttpPost("reopen")]
    public async Task<ActionResult<TeamDetailResponse>> Reopen(Guid teamId, CancellationToken ct) =>
        Ok(await finalization.ReopenAsync(teamId, User, ct));
}

/// <summary>Endpoint nội bộ cho Job Service (token dịch vụ, không qua Gateway).</summary>
[ApiController]
[Authorize(Policy = Policies.Internal)]
[Route("internal/teams")]
public sealed class InternalFinalizationController(IFinalizationService finalization) : ControllerBase
{
    /// <summary>RoundAutoCloseJob sau khi đợt lập nhóm đóng (rule 13).</summary>
    [HttpPost("finalize-round")]
    public async Task<ActionResult<FinalizeRoundResult>> FinalizeRound(FinalizeRoundRequest request, CancellationToken ct) =>
        Ok(await finalization.FinalizeRoundAsync(request, ct));
}
