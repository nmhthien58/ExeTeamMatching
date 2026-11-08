using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Academic.Repositories.Imports;
using Academic.Repositories.Lecturers;
using Academic.Repositories.Semesters;
using Academic.Repositories.Students;
using Academic.Services.Caching;
using Academic.Services.Lecturers;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Academic.Services.Imports;

public interface IImportService
{
    byte[] GetTemplate();

    byte[] GetLecturerTemplate();

    /// <summary>Đọc và kiểm tra file, giữ bản xem trước 30 phút. Chưa ghi dữ liệu.</summary>
    Task<ImportPreviewResponse> PreviewAsync(ImportPreviewInput input, ClaimsPrincipal user, CancellationToken ct);

    Task<ImportBatchResponse> ConfirmAsync(Guid previewId, ClaimsPrincipal user, CancellationToken ct);

    Task<PagedResponse<ImportBatchResponse>> ListAsync(ImportQuery query, CancellationToken ct);

    /// <summary>Đọc và kiểm tra file Excel giảng viên (BE-93).</summary>
    Task<LecturerImportPreviewResponse> PreviewLecturersAsync(LecturerImportPreviewInput input, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Xác nhận import giảng viên, tạo tài khoản và phát LecturerRegistered (BE-93).</summary>
    Task<LecturerImportConfirmResult> ConfirmLecturersAsync(Guid previewId, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>
/// Import danh sách sinh viên của học kỳ (BE-17, W06–W08, rule 5, 11). Danh sách học kỳ là nguồn whitelist đăng nhập:
/// mỗi sinh viên import thành công phát <c>StudentImported</c> qua outbox (Identity tạo sẵn tài khoản, Notification lưu liên lạc).
/// </summary>
/// <remarks>
/// Confirm kiểm tra lại các dòng với DB lúc xác nhận (dữ liệu có thể đổi trong 30 phút), rồi ghi lô import, sinh viên,
/// danh sách học kỳ và outbox trong một lần <c>SaveChanges</c>. MSSV đã có hồ sơ thì cập nhật họ tên, email, chuyên ngành
/// và SĐT (khi file có) theo file; sinh viên đã có trong học kỳ cùng môn thì cập nhật mã lớp.
/// </remarks>
public sealed partial class ImportService(
    IImportRepository imports,
    ILecturerRepository lecturers,
    ImportPreviewStore previews,
    IValidator<ImportPreviewInput> validator,
    TimeProvider clock,
    AcademicCache? cache = null) : IImportService
{
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^\+?[0-9]{9,14}$", RegexOptions.Compiled);

    public byte[] GetTemplate() => StudentImportFile.CreateTemplate();

    public byte[] GetLecturerTemplate() => LecturerImportFile.CreateTemplate();

    public async Task<ImportPreviewResponse> PreviewAsync(ImportPreviewInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var semester = await imports.FindSemesterAsync(input.SemesterId!.Value, ct)
            ?? throw Invalid("semesterId", "Không tìm thấy học kỳ.");

        var rows = StudentImportFile.Read(input.File!);
        var checks = await CheckAsync(rows, semester.Id, input.CourseCode!, ct);

        var previewId = Guid.NewGuid();
        var fileName = Path.GetFileName(input.FileName!);
        await previews.SaveAsync(previewId, new StoredPreview(semester.Id, input.CourseCode!, fileName, user.GetUserId(), rows), ct);
        return new ImportPreviewResponse(previewId, fileName, rows.Count,
            checks.Where(c => c.IsValid).Select(c => c.Row).ToList(),
            checks.Where(c => !c.IsValid).Select(c => ImportErrorRow.From(c.Row, c.Errors)).ToList());
    }

    public async Task<ImportBatchResponse> ConfirmAsync(Guid previewId, ClaimsPrincipal user, CancellationToken ct)
    {
        var preview = await previews.GetAsync(previewId, ct)
            ?? throw new NotFoundException("bản xem trước (đã quá 30 phút hoặc đã xác nhận)", previewId);
        var semester = await imports.FindSemesterAsync(preview.SemesterId, ct) ?? throw new NotFoundException("học kỳ", preview.SemesterId);

        var (checks, students, enrollments) = await LoadAndCheckAsync(preview.Rows, semester.Id, preview.CourseCode, ct);
        var now = ToDbTime(clock.GetUtcNow());
        var valid = checks.Where(c => c.IsValid).ToList();
        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            SemesterId = semester.Id,
            FileName = preview.FileName,
            TotalRows = checks.Count,
            SuccessRows = valid.Count,
            FailedRows = checks.Count - valid.Count,
            Errors = ErrorsJson(checks),
            Status = valid.Count > 0 ? ImportStatuses.Completed : ImportStatuses.Failed,
            ImportedBy = user.GetUserId(),
            ImportedByName = user.FindFirstValue(ClaimNames.Name),
            CreatedAt = now,
            CompletedAt = now,
        };
        imports.Add(batch);

        var newClassCodes = valid
            .Where(c => c.ClassCodeId == 0)
            .Select(c => c.Row.ClassCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var newClassMap = newClassCodes.Count > 0
            ? await imports.EnsureClassCodesAsync(newClassCodes, ct)
            : new Dictionary<string, int>();

        if (newClassCodes.Count > 0 && cache != null)
        {
            await cache.InvalidateAsync(AcademicCacheKeys.Catalog("class-codes"), ct);
        }

        var studentsById = students.ToDictionary(s => s.Id);
        var enrollmentsByStudent = enrollments.ToDictionary(e => e.StudentId);
        foreach (var check in valid)
        {
            var row = check.Row;
            var classCodeId = check.ClassCodeId != 0 ? check.ClassCodeId : newClassMap[row.ClassCode];
            if (check.ExistingStudentId is not { } studentId || !studentsById.TryGetValue(studentId, out var student))
            {
                student = new Student { Id = Guid.NewGuid(), StudentCode = row.StudentCode };
                imports.Add(student);
            }
            student.FullName = row.FullName;
            student.Email = row.Email;
            student.MajorId = check.MajorId;
            if (check.SpecializationId is { } specId)
            {
                student.SpecializationId = specId;
            }
            student.Phone = row.Phone ?? student.Phone;

            if (enrollmentsByStudent.TryGetValue(student.Id, out var enrollment))
            {
                enrollment.ClassCodeId = classCodeId;
                enrollment.ImportBatchId = batch.Id;
            }
            else
            {
                imports.Add(new SemesterEnrollment
                {
                    Id = Guid.NewGuid(), SemesterId = semester.Id, StudentId = student.Id, CourseCode = preview.CourseCode,
                    ClassCodeId = classCodeId, ImportBatchId = batch.Id,
                });
            }

            imports.AddStudentEvent(StudentEventTypes.StudentImported, student.Id, new StudentImportedEvent(
                student.Id, student.StudentCode, student.FullName, student.Email, student.Phone, student.SpecializationId,
                semester.Id, semester.EndDate, preview.CourseCode, row.ClassCode, student.MajorId));
        }

        try
        {
            await imports.SaveAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ImportErrors.Conflict();
        }
        await previews.RemoveAsync(previewId, ct);
        return ImportBatchResponse.From(batch);
    }

    public async Task<PagedResponse<ImportBatchResponse>> ListAsync(ImportQuery query, CancellationToken ct)
    {
        var (items, total) = await imports.ListBatchesAsync(query.SemesterId, query.Page, query.PageSize, ct);
        return PagedResponse<ImportBatchResponse>.Create(
            items.Select(ImportBatchResponse.From).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<LecturerImportPreviewResponse> PreviewLecturersAsync(LecturerImportPreviewInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        if (input.File is null) throw Invalid("file", "Chọn file Excel.");
        if (string.IsNullOrWhiteSpace(input.FileName) || !input.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw Invalid("file", "Chỉ nhận file .xlsx.");
        if (input.FileLength <= 0) throw Invalid("file", "File rỗng.");
        if (input.FileLength > ImportPreviewInputValidator.MaxFileBytes)
            throw Invalid("file", "File tối đa 5 MB.");

        var rows = LecturerImportFile.Read(input.File);
        var existingEmails = await lecturers.FindExistingEmailsAsync(rows.Select(r => r.Email).ToList(), ct);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var validRows = new List<LecturerImportRow>();
        var errorRows = new List<LecturerImportErrorRow>();

        foreach (var row in rows)
        {
            var errors = new List<LecturerImportRowError>();
            var fullName = row.FullName?.Trim() ?? "";
            var email = row.Email?.Trim() ?? "";
            var phone = row.Phone?.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                errors.Add(new LecturerImportRowError("fullName", "Nhập họ tên."));
            }
            else if (fullName.Length > 150)
            {
                errors.Add(new LecturerImportRowError("fullName", "Họ tên tối đa 150 ký tự."));
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                errors.Add(new LecturerImportRowError("email", "Nhập email."));
            }
            else if (email.Length > 254 || !EmailPattern.IsMatch(email))
            {
                errors.Add(new LecturerImportRowError("email", "Email không đúng định dạng."));
            }
            else if (!email.EndsWith("@fpt.edu.vn", StringComparison.OrdinalIgnoreCase) &&
                     !email.EndsWith("@fe.edu.vn", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new LecturerImportRowError("email", "Email giảng viên phải có đuôi @fpt.edu.vn hoặc @fe.edu.vn."));
            }
            else if (!seenEmails.Add(email))
            {
                errors.Add(new LecturerImportRowError("email", "Email bị trùng lặp trong file."));
            }
            else if (existingEmails.Contains(email.ToLowerInvariant()))
            {
                errors.Add(new LecturerImportRowError("email", "Email đã tồn tại trong hệ thống."));
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var normPhone = LecturerRules.NormalizePhone(phone);
                if (normPhone is null || !PhonePattern.IsMatch(normPhone))
                {
                    errors.Add(new LecturerImportRowError("phone", "SĐT phải gồm 9–14 chữ số, có thể bắt đầu bằng +."));
                }
            }

            if (errors.Count == 0)
            {
                validRows.Add(row);
            }
            else
            {
                errorRows.Add(new LecturerImportErrorRow(row.RowNumber, email, fullName, phone, errors));
            }
        }

        var previewId = Guid.NewGuid();
        var fileName = Path.GetFileName(input.FileName!);
        await previews.SaveLecturerAsync(previewId, new StoredLecturerPreview(fileName, user.GetUserId(), rows), ct);

        return new LecturerImportPreviewResponse(previewId, fileName, rows.Count, validRows, errorRows);
    }

    public async Task<LecturerImportConfirmResult> ConfirmLecturersAsync(Guid previewId, ClaimsPrincipal user, CancellationToken ct)
    {
        var preview = await previews.GetLecturerAsync(previewId, ct)
            ?? throw new NotFoundException("bản xem trước import giảng viên", previewId);

        var existingEmails = await lecturers.FindExistingEmailsAsync(preview.Rows.Select(r => r.Email).ToList(), ct);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var successCount = 0;
        var failedCount = 0;
        var errors = new List<string>();

        var now = clock.GetUtcNow();

        foreach (var row in preview.Rows)
        {
            var email = row.Email.Trim();
            var fullName = row.FullName.Trim();
            var phone = row.Phone?.Trim();

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) ||
                !EmailPattern.IsMatch(email) ||
                (!email.EndsWith("@fpt.edu.vn", StringComparison.OrdinalIgnoreCase) && !email.EndsWith("@fe.edu.vn", StringComparison.OrdinalIgnoreCase)) ||
                !seenEmails.Add(email) ||
                existingEmails.Contains(email.ToLowerInvariant()))
            {
                failedCount++;
                errors.Add($"Dòng {row.RowNumber}: Email '{email}' không hợp lệ hoặc đã tồn tại.");
                continue;
            }

            var lecturerId = Guid.NewGuid();
            var lecturer = new Lecturer
            {
                Id = lecturerId,
                FullName = fullName,
                Email = email,
                Phone = LecturerRules.NormalizePhone(phone),
                MaxTeamsExe101 = 4,
                MaxTeamsExe201 = 4,
                DesiredTeamsExe101 = null,
                DesiredTeamsExe201 = null,
                ProfileCompletedAt = null,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };

            lecturers.Add(lecturer);
            lecturers.AddEvent(LecturerEventTypes.LecturerRegistered, lecturerId,
                new LecturerRegisteredEvent(lecturerId, fullName, email, [], 4, 4, true));
            successCount++;
        }

        if (successCount > 0)
        {
            await lecturers.SaveAsync(ct);
        }

        await previews.RemoveLecturerAsync(previewId, ct);
        return new LecturerImportConfirmResult(preview.Rows.Count, successCount, failedCount, errors);
    }

    private async Task<IReadOnlyList<ImportRowCheck>> CheckAsync(IReadOnlyList<ImportRow> rows, Guid semesterId, string courseCode, CancellationToken ct) =>
        (await LoadAndCheckAsync(rows, semesterId, courseCode, ct)).Checks;

    /// <summary>Đọc danh mục, sinh viên trùng MSSV / email, email của mentor và danh sách học kỳ của họ, rồi chạy <see cref="StudentImportRules"/>.</summary>
    private async Task<(IReadOnlyList<ImportRowCheck> Checks, IReadOnlyList<Student> Students, IReadOnlyList<SemesterEnrollment> Enrollments)> LoadAndCheckAsync(
        IReadOnlyList<ImportRow> rows, Guid semesterId, string courseCode, CancellationToken ct)
    {
        var codes = rows.Select(r => r.StudentCode.ToUpperInvariant()).Where(c => c.Length > 0).Distinct().ToList();
        var emails = rows.Select(r => r.Email).Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var classCodes = rows.Select(r => r.ClassCode.ToUpperInvariant()).Where(c => c.Length > 0).Distinct().ToList();

        var majors = await imports.ListMajorsAsync(ct);
        var classes = await imports.ListClassCodesAsync(classCodes, ct);
        var students = await imports.FindStudentsAsync(codes, emails, ct);
        var enrollments = await imports.FindEnrollmentsAsync(semesterId, students.Select(s => s.Id).ToList(), ct);
        var mentorEmails = await imports.FindMentorEmailsAsync(emails, ct);

        var reference = new ImportReference(
            majors.Select(m => new CatalogEntry(m.Id, m.Code, m.Name, m.IsActive)).ToList(),
            classes.Select(c => new CatalogEntry(c.Id, c.Code, null, c.IsActive)).ToList(),
            students.Select(s => new ExistingStudent(s.Id, s.StudentCode, s.Email)).ToList(),
            enrollments.ToDictionary(e => e.StudentId, e => e.CourseCode),
            mentorEmails);
        return (StudentImportRules.Check(rows, courseCode, reference), students, enrollments);
    }

    /// <summary>Cột <c>errors</c> (jsonb): <c>[{row, field, message}]</c>; null khi không có dòng lỗi.</summary>
    private static string? ErrorsJson(IEnumerable<ImportRowCheck> checks)
    {
        var errors = checks.SelectMany(c => c.Errors.Select(e => new { row = c.Row.RowNumber, field = e.Field, message = e.Message })).ToList();
        return errors.Count == 0 ? null : JsonSerializer.Serialize(errors);
    }

    /// <summary>UTC, làm tròn xuống micro giây như <c>timestamptz</c> (giống <c>RoundService</c>).</summary>
    private static DateTimeOffset ToDbTime(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }

    private static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>Lỗi của module Imports.</summary>
public static class ImportErrors
{
    public static ConflictException Conflict() =>
        new("import-conflict", "Dữ liệu sinh viên vừa thay đổi",
            "Có thao tác khác vừa ghi sinh viên trùng MSSV hoặc email trong file. Tải lại file để xem trước rồi xác nhận lại.");
}

public static class ImportsModule
{
    /// <summary>Module Imports (BE-17): import danh sách sinh viên học kỳ – W06, W07, W08.</summary>
    public static IServiceCollection AddImportsModule(this IServiceCollection services)
    {
        services.AddScoped<IImportRepository, ImportRepository>();
        services.AddScoped<IImportService, ImportService>();
        services.AddSingleton<ImportPreviewStore>();
        return services;
    }
}
