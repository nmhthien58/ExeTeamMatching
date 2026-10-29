using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.Statistics;
using Team.Services.Teams;

namespace Team.Api.Controllers.Statistics;

/// <summary>Dashboard W02, sinh viên chưa có nhóm và nhắc W20 (BE-50) – team.yaml tag Admin.</summary>
[ApiController]
[Authorize(Policy = Policies.Admin)]
[Route("api")]
public sealed class StatisticsController(IStatisticsService statistics) : ControllerBase
{
    [HttpGet("statistics/overview")]
    public async Task<ActionResult<StatisticsOverviewResponse>> Overview([FromQuery] StatisticsQuery query, CancellationToken ct) =>
        Ok(await statistics.GetOverviewAsync(query, ct));

    [HttpGet("unmatched-students")]
    public async Task<ActionResult<PagedResponse<StudentRef>>> Unmatched([FromQuery] UnmatchedStudentsQuery query, CancellationToken ct) =>
        Ok(await statistics.ListUnmatchedAsync(query, ct));

    /// <summary>Phát UnmatchedStudentsReminded; Notification gửi thông báo và email cho từng sinh viên.</summary>
    [HttpPost("unmatched-students/remind")]
    public async Task<IActionResult> RemindUnmatched(RemindUnmatchedRequest request, CancellationToken ct)
    {
        await statistics.RemindUnmatchedAsync(request, User, ct);
        return Accepted();
    }
}

/// <summary>Endpoint nội bộ cho Job Service (token dịch vụ, không qua Gateway).</summary>
[ApiController]
[Authorize(Policy = Policies.Internal)]
[Route("internal")]
public sealed class InternalStatisticsController(IStatisticsService statistics) : ControllerBase
{
    /// <summary>DeadlineReminderJob – sinh viên chưa có nhóm và nhóm dưới 4 người (72 h, 24 h trước hạn).</summary>
    [HttpGet("deadline-reminder-targets")]
    public async Task<ActionResult<DeadlineReminderTargets>> DeadlineReminderTargets([FromQuery] DeadlineReminderTargetsQuery query, CancellationToken ct) =>
        Ok(await statistics.GetDeadlineReminderTargetsAsync(query, ct));
}
