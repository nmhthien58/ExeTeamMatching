using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.Members;

namespace Team.Api.Controllers.Members;

/// <summary>Endpoint nội bộ cho Academic (token dịch vụ, không qua Gateway).</summary>
[ApiController]
[Authorize(Policy = Policies.Internal)]
[Route("internal/students")]
public sealed class InternalMembersController(StudentTeamStatusService status) : ControllerBase
{
    /// <summary>Sinh viên đã có nhóm ở học kỳ hiện hành chưa (chặn bật "Tôi đang tìm nhóm").</summary>
    [HttpGet("{studentId:guid}/team-status")]
    public Task<StudentTeamStatus> TeamStatus(Guid studentId, CancellationToken ct) => status.GetAsync(studentId, ct);
}
