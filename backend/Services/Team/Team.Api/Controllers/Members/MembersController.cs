using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.Members;

namespace Team.Api.Controllers.Members;

/// <summary>Thành viên nhóm: S17 (BE-30) – team.yaml tag Members.</summary>
[ApiController]
[Authorize(Policy = Policies.Student)]
[Route("api/teams/{teamId:guid}")]
public sealed class MembersController(IMemberService members) : ControllerBase
{
    /// <summary>Trưởng nhóm xóa thành viên; nhóm EXE201 kế thừa → thay đổi chờ duyệt.</summary>
    [HttpDelete("members/{studentId:guid}")]
    public async Task<ActionResult<MemberChangeResult>> Remove(Guid teamId, Guid studentId, CancellationToken ct) =>
        Ok(await members.RemoveAsync(teamId, studentId, User, ct));

    [HttpPost("leave")]
    public async Task<ActionResult<MemberChangeResult>> Leave(Guid teamId, CancellationToken ct) => Ok(await members.LeaveAsync(teamId, User, ct));

    [HttpPost("transfer-leader")]
    public async Task<ActionResult<MemberChangeResult>> TransferLeader(Guid teamId, TransferLeaderRequest request, CancellationToken ct) =>
        Ok(await members.TransferLeaderAsync(teamId, request, User, ct));
}
