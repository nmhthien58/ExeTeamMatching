using BuildingBlocks.Auth;
using Identity.Services;
using Identity.Services.ClientErrors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.Controllers.ClientErrors;

/// <summary>Báo cáo lỗi từ app và màn theo dõi của Bộ môn (BE-87, W30; identity.yaml tag ClientErrors).</summary>
[ApiController]
[Route("api")]
public sealed class ClientErrorsController(IClientErrorService errors) : ControllerBase
{
    /// <summary>Stack đã cắt 8 KB phía app; để dư cho các trường còn lại và ký tự nhiều byte.</summary>
    private const long MaxBodyBytes = 64 * 1024;

    /// <summary>App gửi một lỗi. Cho phép khi chưa đăng nhập vì crash có thể xảy ra trước đó.</summary>
    [HttpPost("client-errors")]
    [AllowAnonymous]
    [EnableRateLimiting(ClientErrorRateLimit.PolicyName)]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Report(ClientErrorReportRequest request, CancellationToken ct)
    {
        var userId = Guid.TryParse(User.FindFirst(ClaimNames.Subject)?.Value, out var id) ? id : (Guid?)null;
        await errors.ReportAsync(request, userId, ct);
        return Accepted();
    }

    [HttpGet("admin/client-errors")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<PagedResponse<ClientErrorResponse>>> Search([FromQuery] ClientErrorQuery query, CancellationToken ct) =>
        Ok(await errors.SearchAsync(query, ct));

    [HttpGet("admin/client-errors/summary")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<ClientErrorSummaryResponse>> Summary(CancellationToken ct) =>
        Ok(await errors.GetSummaryAsync(ct));
}
