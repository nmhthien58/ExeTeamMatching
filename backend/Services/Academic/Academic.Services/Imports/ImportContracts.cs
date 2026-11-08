using Academic.Repositories.Imports;
using Academic.Repositories.Rounds;
using FluentValidation;

namespace Academic.Services.Imports;

// Schema của academic.yaml (tag Imports). JSON camelCase, thời gian UTC.

/// <summary>Schema <c>ImportRow</c>. Dòng lỗi giữ nguyên giá trị đọc từ file (đã bỏ khoảng trắng hai đầu).</summary>
public sealed record ImportRow(int RowNumber, string StudentCode, string FullName, string Email, string SpecializationCode, string ClassCode, string? Phone, string? Warning = null);

/// <summary>Một lỗi của dòng: <c>field</c> là tên trường của <see cref="ImportRow"/>.</summary>
public sealed record ImportRowError(string Field, string Message);

/// <summary>Phần tử <c>errorRows</c> của <c>ImportPreview</c>: <see cref="ImportRow"/> kèm danh sách lỗi.</summary>
public sealed record ImportErrorRow(
    int RowNumber, string StudentCode, string FullName, string Email, string SpecializationCode, string ClassCode, string? Phone,
    IReadOnlyList<ImportRowError> Errors, string? Warning = null)
{
    public static ImportErrorRow From(ImportRow r, IReadOnlyList<ImportRowError> errors) =>
        new(r.RowNumber, r.StudentCode, r.FullName, r.Email, r.SpecializationCode, r.ClassCode, r.Phone, errors, r.Warning);
}

/// <summary>Schema <c>ImportPreview</c>.</summary>
public sealed record ImportPreviewResponse(
    Guid PreviewId, string FileName, int TotalRows, IReadOnlyList<ImportRow> ValidRows, IReadOnlyList<ImportErrorRow> ErrorRows);

/// <summary>Schema <c>ImportBatch</c>.</summary>
public sealed record ImportBatchResponse(
    Guid Id, Guid SemesterId, string FileName, int TotalRows, int SuccessRows, int FailedRows, string Status,
    string? ImportedByName, DateTimeOffset CreatedAt)
{
    public static ImportBatchResponse From(ImportBatch b) =>
        new(b.Id, b.SemesterId, b.FileName, b.TotalRows, b.SuccessRows, b.FailedRows, b.Status, b.ImportedByName, b.CreatedAt);
}

/// <summary>Form <c>multipart/form-data</c> của <c>POST /api/imports/students/preview</c>, đã tách khỏi <c>IFormFile</c>.</summary>
public sealed record ImportPreviewInput(Stream? File, string? FileName, long FileLength, Guid? SemesterId, string? CourseCode);

public sealed class LecturerImportForm
{
    public Microsoft.AspNetCore.Http.IFormFile? File { get; init; }
}

/// <summary>Query của <c>GET /api/imports</c>.</summary>
public sealed record ImportQuery(Guid? SemesterId, int Page = 1, int PageSize = 20);

public sealed class ImportQueryValidator : AbstractValidator<ImportQuery>
{
    public ImportQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("page bắt đầu từ 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("pageSize từ 1 đến 100.");
    }
}

/// <summary>Kiểm tra form xem trước (gọi trong service vì <c>IFormFile</c> không đi qua ValidationFilter thành record này).</summary>
public sealed class ImportPreviewInputValidator : AbstractValidator<ImportPreviewInput>
{
    /// <summary>Giới hạn file import; danh sách vài nghìn sinh viên chỉ khoảng vài trăm KB.</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    public ImportPreviewInputValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("Chọn file Excel.");
        RuleFor(x => x.FileName).Must(n => n!.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            .When(x => x.File is not null).WithMessage("Chỉ nhận file .xlsx.").OverridePropertyName("file");
        RuleFor(x => x.FileLength).GreaterThan(0).When(x => x.File is not null).WithMessage("File rỗng.")
            .LessThanOrEqualTo(MaxFileBytes).WithMessage("File tối đa 5 MB.").OverridePropertyName("file");
        RuleFor(x => x.SemesterId).NotEmpty().WithMessage("Chọn học kỳ.");
        RuleFor(x => x.CourseCode).Must(c => c is CourseCodes.Exe101 or CourseCodes.Exe201).WithMessage("Môn là EXE101 hoặc EXE201.");
    }
}

// Payload sự kiện exe.student (docs/contracts/events.md), key = studentId.

public static class StudentEventTypes
{
    public const string StudentImported = "StudentImported";
}

public sealed record StudentImportedEvent(
    Guid StudentId, string StudentCode, string FullName, string Email, string? Phone, int? SpecializationId,
    Guid SemesterId, DateOnly SemesterEndDate, string CourseCode, string ClassCode, int MajorId);
