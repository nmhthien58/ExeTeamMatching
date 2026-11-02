using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.AdvisorAssignments;
using Team.Services.AdvisorMatching;

namespace Team.Api.Controllers.AdvisorMatching;

/// <summary>Ghép cố vấn – Admin: W19 (giảng viên), W28 (mentor), Ghép sớm, đổi giảng viên (BE-80) – team.yaml tag AdvisorMatching.</summary>
[ApiController]
[Authorize(Policy = Policies.Admin)]
public sealed class AdvisorMatchingController(IAdvisorMatchingService matching) : ControllerBase
{
    /// <summary>Bảng W19 / W28: nhóm với cố vấn hiện tại, lý do, nguyện vọng, quan tâm; tải từng cố vấn.</summary>
    [HttpGet("api/advisor-matching/{kind}/overview")]
    public Task<AdvisorMatchingOverviewResponse> Overview(string kind, [FromQuery] AdvisorMatchingOverviewQuery query, CancellationToken ct) =>
        matching.GetOverviewAsync(kind, query, ct);

    /// <summary>Ghép sớm – bước 1: tính phương án cho nhóm chưa có cố vấn, lưu lần chạy PREVIEW (không đổi dữ liệu ghép).</summary>
    [HttpPost("api/advisor-matching/{kind}/preview")]
    public Task<AdvisorMatchingRunResponse> Preview(string kind, AdvisorMatchingPreviewInput input, CancellationToken ct) =>
        matching.PreviewAsync(kind, input, User, ct);

    /// <summary>Ghép sớm – bước 2: áp dụng phương án, bỏ qua nhóm đã có cố vấn trong lúc chờ. 409 run-not-preview nếu đã áp dụng / bỏ.</summary>
    [HttpPost("api/advisor-matching/runs/{id:guid}/confirm")]
    public Task<AdvisorMatchingRunResponse> Confirm(Guid id, CancellationToken ct) => matching.ConfirmAsync(id, User, ct);

    /// <summary>Bỏ phương án xem trước (DISCARDED).</summary>
    [HttpDelete("api/advisor-matching/runs/{id:guid}")]
    public async Task<IActionResult> Discard(Guid id, CancellationToken ct)
    {
        await matching.DiscardAsync(id, ct);
        return NoContent();
    }

    /// <summary>Đổi giảng viên hướng dẫn (reason MANUAL, ghi chú bắt buộc, ghi nhật ký). 409 lecturer-full khi vượt số nhóm tối đa.</summary>
    [HttpPut("api/teams/{teamId:guid}/instructor")]
    public Task<LecturerAssignmentResponse> ChangeInstructor(Guid teamId, ChangeInstructorInput input, CancellationToken ct) =>
        matching.ChangeInstructorAsync(teamId, input, User, ct);
}

/// <summary>Endpoint nội bộ cho Job Service (token dịch vụ, không qua Gateway).</summary>
[ApiController]
[Authorize(Policy = Policies.Internal)]
[Route("internal/advisor-matching")]
public sealed class InternalAdvisorMatchingController(IAdvisorMatchingService matching) : ControllerBase
{
    /// <summary>LecturerMatchingJob / MentorMatchingJob: ghép và áp dụng ngay (SCHEDULED). Gọi lại trả lần đã áp dụng, không tạo trùng.</summary>
    [HttpPost("{kind}")]
    public Task<AdvisorMatchingRunResponse> Run(string kind, InternalAdvisorMatchingInput input, CancellationToken ct) =>
        matching.RunScheduledAsync(kind, input, ct);
}
