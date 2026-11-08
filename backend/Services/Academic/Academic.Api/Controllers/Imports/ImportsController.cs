using Academic.Services;
using Academic.Services.Imports;
using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academic.Api.Controllers.Imports;

/// <summary>Import danh sách sinh viên học kỳ: W06, W07, W08 (BE-17) – academic.yaml tag Imports. Chỉ Admin.</summary>
[ApiController]
[Authorize(Policy = Policies.Admin)]
[Route("api/imports")]
public sealed class ImportsController(IImportService imports) : ControllerBase
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("students/template")]
    public FileContentResult Template() => File(imports.GetTemplate(), XlsxType, "mau-import-sinh-vien.xlsx");

    /// <summary>Tải file và xem trước dòng hợp lệ / dòng lỗi. Chưa ghi dữ liệu; bản xem trước giữ 30 phút.</summary>
    [HttpPost("students/preview")]
    [RequestSizeLimit(ImportPreviewInputValidator.MaxFileBytes + 64 * 1024)]
    public async Task<ImportPreviewResponse> Preview([FromForm] StudentImportForm form, CancellationToken ct)
    {
        await using var stream = form.File?.OpenReadStream();
        return await imports.PreviewAsync(
            new ImportPreviewInput(stream, form.File?.FileName, form.File?.Length ?? 0, form.SemesterId, form.CourseCode), User, ct);
    }

    /// <summary>Xác nhận import, bỏ qua dòng lỗi; phát StudentImported cho từng sinh viên.</summary>
    [HttpPost("students/{previewId:guid}/confirm")]
    public Task<ImportBatchResponse> Confirm(Guid previewId, CancellationToken ct) => imports.ConfirmAsync(previewId, User, ct);

    /// <summary>Lịch sử import, mới nhất trước.</summary>
    [HttpGet]
    public Task<PagedResponse<ImportBatchResponse>> List([FromQuery] ImportQuery query, CancellationToken ct) => imports.ListAsync(query, ct);

    [HttpGet("lecturers/template")]
    public FileContentResult LecturerTemplate() => File(imports.GetLecturerTemplate(), XlsxType, "mau-import-giang-vien.xlsx");

    /// <summary>Tải file Excel giảng viên và xem trước dòng hợp lệ / dòng lỗi (BE-93).</summary>
    [HttpPost("lecturers/preview")]
    [RequestSizeLimit(ImportPreviewInputValidator.MaxFileBytes + 64 * 1024)]
    public async Task<LecturerImportPreviewResponse> PreviewLecturers([FromForm] LecturerImportForm form, CancellationToken ct)
    {
        await using var stream = form.File?.OpenReadStream();
        return await imports.PreviewLecturersAsync(
            new LecturerImportPreviewInput(stream, form.File?.FileName, form.File?.Length ?? 0), User, ct);
    }

    /// <summary>Xác nhận import giảng viên, tạo tài khoản và phát LecturerRegistered (BE-93).</summary>
    [HttpPost("lecturers/{previewId:guid}/confirm")]
    public Task<LecturerImportConfirmResult> ConfirmLecturers(Guid previewId, CancellationToken ct) =>
        imports.ConfirmLecturersAsync(previewId, User, ct);
}

/// <summary>Form <c>multipart/form-data</c>: <c>file</c>, <c>semesterId</c>, <c>courseCode</c>.</summary>
public sealed class StudentImportForm
{
    public IFormFile? File { get; init; }
    public Guid? SemesterId { get; init; }
    public string? CourseCode { get; init; }
}
