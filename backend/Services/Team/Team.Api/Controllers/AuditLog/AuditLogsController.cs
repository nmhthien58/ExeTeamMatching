using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.AuditLog;
using Team.Services.Teams;

namespace Team.Api.Controllers.AuditLog;

/// <summary>Lịch sử nhóm S22, L05 và nhật ký hệ thống W24 (BE-32, rule 10) – team.yaml tag AuditLog.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class AuditLogsController(IAuditLogService logs) : ControllerBase
{
    /// <summary>Thành viên nhóm, giảng viên hướng dẫn / mentor của nhóm, Admin.</summary>
    [HttpGet("teams/{teamId:guid}/audit-logs")]
    public async Task<ActionResult<PagedResponse<AuditLogResponse>>> ForTeam(Guid teamId, [FromQuery] AuditLogPageQuery query, CancellationToken ct) =>
        Ok(await logs.ListForTeamAsync(teamId, query, User, ct));

    [HttpGet("audit-logs")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<PagedResponse<AuditLogResponse>>> Search([FromQuery] AuditLogQuery query, CancellationToken ct) =>
        Ok(await logs.SearchAsync(query, ct));
}
