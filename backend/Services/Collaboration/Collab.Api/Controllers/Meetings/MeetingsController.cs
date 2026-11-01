using BuildingBlocks.Auth;
using Collab.Services.Meetings;
using Collab.Services.Tasks;
using Collab.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Collab.Api.Controllers.Meetings;

/// <summary>Lịch họp C07, chi tiết họp C08 (BE-68, rule 25) – collab.yaml tag Meetings.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class MeetingsController(IMeetingService meetings) : ControllerBase
{
    [HttpGet("workspaces/{workspaceId:guid}/meetings")]
    [Authorize(Policy = CollabPolicies.WorkspaceMember)]
    public async Task<ActionResult<MeetingListResponse>> List(Guid workspaceId, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await meetings.ListAsync(workspaceId, status, HttpContext.GetWorkspaceAccess(), ct));

    /// <summary>Trưởng nhóm, giảng viên hướng dẫn, mentor đề xuất 1–3 khung; một khung = họp đã chốt luôn.</summary>
    [HttpPost("workspaces/{workspaceId:guid}/meetings")]
    [Authorize(Policy = CollabPolicies.WorkspaceWriter)]
    public async Task<ActionResult<MeetingDetailResponse>> Create(Guid workspaceId, MeetingInput input, CancellationToken ct)
    {
        var meeting = await meetings.CreateAsync(workspaceId, input, HttpContext.GetWorkspaceAccess(), ct);
        return Created($"/api/meetings/{meeting.Id}", meeting);
    }

    [HttpGet("meetings/{meetingId:guid}")]
    public async Task<ActionResult<MeetingDetailResponse>> Get(Guid meetingId, CancellationToken ct) =>
        Ok(await meetings.GetAsync(meetingId, User, ct));

    [HttpPut("meetings/{meetingId:guid}/responses")]
    public async Task<ActionResult<MeetingDetailResponse>> Answer(Guid meetingId, MeetingAnswersRequest request, CancellationToken ct) =>
        Ok(await meetings.AnswerAsync(meetingId, request, User, ct));

    [HttpPost("meetings/{meetingId:guid}/slots")]
    public async Task<ActionResult<MeetingDetailResponse>> AddSlot(Guid meetingId, SlotInput input, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await meetings.AddSlotAsync(meetingId, input, User, ct));

    [HttpPost("meetings/{meetingId:guid}/confirm")]
    public async Task<ActionResult<MeetingDetailResponse>> Confirm(Guid meetingId, ConfirmMeetingRequest request, CancellationToken ct) =>
        Ok(await meetings.ConfirmAsync(meetingId, request, User, ct));

    [HttpPost("meetings/{meetingId:guid}/cancel")]
    public async Task<ActionResult<MeetingDetailResponse>> Cancel(Guid meetingId, CancelMeetingRequest request, CancellationToken ct) =>
        Ok(await meetings.CancelAsync(meetingId, request, User, ct));

    [HttpPut("meetings/{meetingId:guid}/minutes")]
    public async Task<ActionResult<MeetingDetailResponse>> Minutes(Guid meetingId, MeetingMinutesRequest request, CancellationToken ct) =>
        Ok(await meetings.SaveMinutesAsync(meetingId, request, User, ct));

    [HttpPost("meetings/{meetingId:guid}/minutes/tasks")]
    public async Task<ActionResult<IReadOnlyList<TaskCardResponse>>> MinuteTasks(Guid meetingId, MinuteTasksRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await meetings.CreateTasksAsync(meetingId, request, User, ct));
}

/// <summary>Endpoint nội bộ cho MeetingReminderJob, chỉ token dịch vụ, không qua Gateway.</summary>
[ApiController]
[Authorize(Policy = Policies.Internal)]
[Route("internal/meetings")]
public sealed class InternalMeetingsController(IMeetingService meetings) : ControllerBase
{
    /// <summary>Họp đã chốt bắt đầu trong <paramref name="offsetMinutes"/> phút tới mà chưa gửi mốc nhắc này.</summary>
    [HttpGet("upcoming")]
    public async Task<ActionResult<IReadOnlyList<UpcomingMeetingResponse>>> Upcoming([FromQuery] int offsetMinutes, CancellationToken ct) =>
        Ok(await meetings.ListUpcomingAsync(offsetMinutes, ct));

    [HttpPost("{meetingId:guid}/reminders")]
    public async Task<IActionResult> ReminderSent(Guid meetingId, MeetingReminderRequest request, CancellationToken ct)
    {
        await meetings.MarkReminderSentAsync(meetingId, request.OffsetMinutes, ct);
        return NoContent();
    }
}
