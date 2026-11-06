using BuildingBlocks.Auth;
using Collab.Services.Files;
using Collab.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Collab.Api.Controllers.Reports;

/// <summary>Báo cáo dự án PDF của từng người (MB-53) – collab.yaml tag Reports. Chỉ chủ báo cáo đọc, ghi được.</summary>
[ApiController]
[Authorize]
[Route("api/reports/{ownerId:guid}")]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    /// <summary>Lớn hơn 25 MB một chút để Services trả 413 <c>file-too-large</c> có mã lỗi.</summary>
    private const long UploadRequestLimit = 30 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportResponse>>> List(Guid ownerId, CancellationToken ct) =>
        Ok(await reports.ListAsync(ownerId, User.GetUserId(), ct));

    [HttpPost]
    [RequestSizeLimit(UploadRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<ActionResult<UploadedReportResponse>> Upload(Guid ownerId, [FromForm] UploadReportForm form, CancellationToken ct)
    {
        await using var content = form.File.OpenReadStream();
        var result = await reports.UploadAsync(ownerId, User.GetUserId(), new UploadReportCommand(form.File.FileName, form.File.Length, content), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("{fileName}/download-url")]
    public async Task<ActionResult<DownloadUrlResponse>> DownloadUrl(Guid ownerId, string fileName, CancellationToken ct) =>
        Ok(await reports.GetDownloadUrlAsync(ownerId, fileName, User.GetUserId(), ct));
}

/// <summary>Form multipart của <c>POST /api/reports/{ownerId}</c>.</summary>
public sealed class UploadReportForm
{
    public required IFormFile File { get; init; }
}
