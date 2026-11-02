using BuildingBlocks.Auth;
using Collab.Services.Meetings;
using Collab.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Collab.Api.Controllers.Meetings;

/// <summary>Lịch họp hằng tuần EXE201 C09 (BE-82, rule 25) – collab.yaml tag MeetingSchedules.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class MeetingSchedulesController(IMeetingScheduleService schedules) : ControllerBase
{
    /// <remarks>Chưa có lịch trả 200 kèm <c>null</c> theo contract, không phải 204.</remarks>
    [HttpGet("meeting-schedules/me")]
    [Authorize(Policy = Policies.Lecturer)]
    public async Task<IActionResult> Mine(CancellationToken ct) => new JsonResult(await schedules.GetMineAsync(User, ct));

    [HttpPut("meeting-schedules/me")]
    [Authorize(Policy = Policies.Lecturer)]
    public Task<MeetingScheduleResponse> Save(MeetingScheduleInput input, CancellationToken ct) => schedules.SaveAsync(input, User, ct);

    /// <summary>Đăng lịch cho các nhóm; ít slot hơn số nhóm vẫn đăng được, response có cảnh báo.</summary>
    [HttpPost("meeting-schedules/me/publish")]
    [Authorize(Policy = Policies.Lecturer)]
    public Task<MeetingScheduleResponse> Publish(CancellationToken ct) => schedules.PublishAsync(User, ct);

    [HttpPut("meeting-schedules/me/bookings/{workspaceId:guid}")]
    [Authorize(Policy = Policies.Lecturer)]
    public Task<MeetingScheduleResponse> Move(Guid workspaceId, SlotBookingRequest request, CancellationToken ct) =>
        schedules.MoveAsync(workspaceId, request, User, ct);

    /// <remarks>Chưa có lịch trả 200 kèm <c>null</c> theo contract, không phải 204.</remarks>
    [HttpGet("workspaces/{workspaceId:guid}/meeting-schedule")]
    [Authorize(Policy = CollabPolicies.WorkspaceMember)]
    public async Task<IActionResult> ForTeam(Guid workspaceId, CancellationToken ct) =>
        new JsonResult(await schedules.GetForTeamAsync(workspaceId, HttpContext.GetWorkspaceAccess(), ct));

    [HttpPut("workspaces/{workspaceId:guid}/slot-booking")]
    [Authorize(Policy = CollabPolicies.WorkspaceWriter)]
    public Task<TeamMeetingScheduleResponse> Book(Guid workspaceId, SlotBookingRequest request, CancellationToken ct) =>
        schedules.BookAsync(workspaceId, request, HttpContext.GetWorkspaceAccess(), ct);
}

/// <summary>Endpoint nội bộ cho SlotBookingCloseJob, chỉ token dịch vụ, không qua Gateway.</summary>
[ApiController]
[Authorize(Policy = Policies.Internal)]
[Route("internal/meeting-schedules")]
public sealed class InternalMeetingSchedulesController(IMeetingScheduleService schedules) : ControllerBase
{
    [HttpPost("close-booking")]
    public Task<CloseBookingResponse> CloseBooking(CancellationToken ct) => schedules.CloseBookingAsync(ct);
}
