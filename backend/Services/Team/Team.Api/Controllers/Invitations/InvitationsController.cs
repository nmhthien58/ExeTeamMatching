using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.Invitations;
using Team.Services.Members;

namespace Team.Api.Controllers.Invitations;

/// <summary>Lời mời: S04, S12, S18–S20 (BE-28, rule 8) – team.yaml tag Invitations.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class InvitationsController(IInvitationService invitations) : ControllerBase
{
    [HttpGet("teams/{teamId:guid}/invitations")]
    public async Task<ActionResult<IReadOnlyList<InvitationResponse>>> ListForTeam(Guid teamId, [FromQuery] RequestStatusQuery query, CancellationToken ct) =>
        Ok(await invitations.ListForTeamAsync(teamId, query.Status, User, ct));

    /// <summary>Trưởng nhóm gửi lời mời; hết hạn sau 72 giờ.</summary>
    [HttpPost("teams/{teamId:guid}/invitations")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<InvitationResponse>> Send(Guid teamId, SendInvitationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await invitations.SendAsync(teamId, request, User, ct));

    [HttpGet("invitations/me")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<IReadOnlyList<InvitationResponse>>> Mine([FromQuery] RequestStatusQuery query, CancellationToken ct) =>
        Ok(await invitations.ListMineAsync(query.Status, User, ct));

    [HttpPost("invitations/{id:guid}/accept")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<MemberChangeResult>> Accept(Guid id, CancellationToken ct) => Ok(await invitations.AcceptAsync(id, User, ct));

    [HttpPost("invitations/{id:guid}/decline")]
    [Authorize(Policy = Policies.Student)]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
    {
        await invitations.DeclineAsync(id, User, ct);
        return NoContent();
    }

    [HttpPost("invitations/{id:guid}/cancel")]
    [Authorize(Policy = Policies.Student)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await invitations.CancelAsync(id, User, ct);
        return NoContent();
    }
}

/// <summary>Endpoint nội bộ cho Job Service (token dịch vụ, không qua Gateway).</summary>
[ApiController]
[Authorize(Policy = Policies.Internal)]
[Route("internal/invitations")]
public sealed class InternalInvitationsController(IInvitationService invitations) : ControllerBase
{
    /// <summary>InvitationExpiryJob – lời mời / yêu cầu quá 72 giờ → hết hạn.</summary>
    [HttpPost("expire-overdue")]
    public async Task<ActionResult<ExpireResult>> ExpireOverdue(CancellationToken ct) => Ok(await invitations.ExpireOverdueAsync(ct));
}
