using BuildingBlocks.Auth;
using Collab.Services.Advisor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Collab.Api.Controllers.Advisor;

/// <summary>Tổng quan cố vấn L01, M01 (BE-86) – collab.yaml tag Workspaces.</summary>
[ApiController]
[Authorize(Roles = $"{Roles.Lecturer},{Roles.Mentor}")]
[Route("api/advisor")]
public sealed class AdvisorController(IAdvisorOverviewService overview) : ControllerBase
{
    /// <summary>Cần trả lời, Trễ hạn nộp (chỉ giảng viên hướng dẫn), Họp sắp tới, Chờ chốt lịch – gom mọi nhóm mình phụ trách.</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<AdvisorOverviewResponse>> Get(CancellationToken ct) =>
        Ok(await overview.GetAsync(User, ct));
}
