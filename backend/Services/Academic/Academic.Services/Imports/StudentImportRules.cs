using System.Text.RegularExpressions;

namespace Academic.Services.Imports;

/// <summary>Dữ liệu trong DB cần để kiểm tra một file: danh mục ngành, mã lớp, sinh viên đã có (theo MSSV hoặc email), danh sách học kỳ.</summary>
/// <param name="Enrollments">Môn của sinh viên đã có trong danh sách học kỳ, theo id sinh viên.</param>
/// <param name="MentorEmails">Email trong file đã thuộc một mentor.</param>
public sealed record ImportReference(
    IReadOnlyList<CatalogEntry> Majors,
    IReadOnlyList<CatalogEntry> ClassCodes,
    IReadOnlyList<ExistingStudent> Students,
    IReadOnlyDictionary<Guid, string> Enrollments,
    IReadOnlyCollection<string> MentorEmails,
    IReadOnlyList<CatalogEntry>? Specializations = null);

/// <summary>Mục danh mục; <see cref="Name"/> null với mã lớp.</summary>
public sealed record CatalogEntry(int Id, string Code, string? Name, bool IsActive);

public sealed record ExistingStudent(Guid Id, string StudentCode, string Email);

/// <summary>
/// Kết quả kiểm tra một dòng. <see cref="Row"/> đã chuẩn hóa (MSSV, mã chuyên ngành, mã lớp viết hoa; SĐT bỏ khoảng trắng, dấu chấm, gạch).
/// Dòng hợp lệ có <see cref="MajorId"/>, <see cref="ClassCodeId"/>; <see cref="ExistingStudentId"/> khi MSSV đã có hồ sơ.
/// </summary>
public sealed record ImportRowCheck(ImportRow Row, IReadOnlyList<ImportRowError> Errors, int MajorId, int? SpecializationId, int ClassCodeId, Guid? ExistingStudentId)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Quy tắc từng dòng của file import (BE-17, rule 5, 11). Hàm thuần, không đọc DB: dữ liệu DB truyền qua <see cref="ImportReference"/>.
/// Một dòng có thể có nhiều lỗi; dòng trùng MSSV / email với dòng trước trong file bị báo lỗi, dòng đầu tiên vẫn hợp lệ.
/// </summary>
public static partial class StudentImportRules
{
    public static IReadOnlyList<ImportRowCheck> Check(IReadOnlyList<ImportRow> rows, string courseCode, ImportReference reference)
    {
        var majors = reference.Majors;
        var classCodes = reference.ClassCodes.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var byCode = reference.Students.ToDictionary(s => s.StudentCode, StringComparer.OrdinalIgnoreCase);
        var byEmail = reference.Students.ToDictionary(s => s.Email, StringComparer.OrdinalIgnoreCase);
        var mentorEmails = reference.MentorEmails.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenCodes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var result = new List<ImportRowCheck>(rows.Count);
        foreach (var raw in rows)
        {
            var row = Normalize(raw);
            var errors = new List<ImportRowError>();
            void Error(string field, string message) => errors.Add(new ImportRowError(field, message));

            var majorId = 0;
            // MSSV: 2–3 chữ + 6 số (rule 11, BE-91), không trùng trong file. Tiền tố là mã ngành.
            if (row.StudentCode.Length == 0)
            {
                Error(ImportFields.StudentCode, "Thiếu MSSV.");
            }
            else if (!StudentCodePattern().IsMatch(row.StudentCode))
            {
                Error(ImportFields.StudentCode, "MSSV phải gồm 2–3 chữ cái và 6 chữ số (ví dụ SE184118, MKT1906).");
            }
            else if (!seenCodes.TryAdd(row.StudentCode, row.RowNumber))
            {
                Error(ImportFields.StudentCode, $"Trùng MSSV với dòng {seenCodes[row.StudentCode]}.");
            }
            else
            {
                var prefix = StudentCodePrefix().Match(row.StudentCode).Groups[1].Value;
                if (majors.FirstOrDefault(m => string.Equals(m.Code, prefix, StringComparison.OrdinalIgnoreCase)) is not { } major)
                {
                    Error(ImportFields.StudentCode, $"Tiền tố ngành '{prefix}' không có trong danh mục ngành.");
                }
                else if (!major.IsActive)
                {
                    Error(ImportFields.StudentCode, $"Ngành '{major.Code}' đã ngừng dùng.");
                }
                else
                {
                    majorId = major.Id;
                }
            }

            if (row.FullName.Length == 0)
            {
                Error(ImportFields.FullName, "Thiếu họ tên.");
            }
            else if (row.FullName.Length > 150)
            {
                Error(ImportFields.FullName, "Họ tên tối đa 150 ký tự.");
            }

            // Email: cùng mẫu với CHECK ck_students_email; không trùng trong file, không thuộc mentor hay sinh viên có MSSV khác.
            if (row.Email.Length == 0)
            {
                Error(ImportFields.Email, "Thiếu email.");
            }
            else if (row.Email.Length > 254 || !EmailPattern().IsMatch(row.Email))
            {
                Error(ImportFields.Email, "Email không đúng định dạng.");
            }
            else if (!seenEmails.TryAdd(row.Email, row.RowNumber))
            {
                Error(ImportFields.Email, $"Trùng email với dòng {seenEmails[row.Email]}.");
            }
            else if (mentorEmails.Contains(row.Email))
            {
                Error(ImportFields.Email, "Email đã thuộc một mentor.");
            }
            else if (byEmail.TryGetValue(row.Email, out var owner) && !string.Equals(owner.StudentCode, row.StudentCode, StringComparison.OrdinalIgnoreCase))
            {
                Error(ImportFields.Email, $"Email đã thuộc sinh viên {owner.StudentCode}.");
            }

            // Chuyên ngành KHÔNG kiểm tra lúc import (rule 16, BE-91). Sinh viên tự chọn chuyên ngành ở onboarding/hồ sơ.
            int? specializationId = null;

            var classCodeId = 0;
            if (row.ClassCode.Length == 0)
            {
                Error(ImportFields.ClassCode, "Thiếu mã lớp.");
            }
            else if (!classCodes.TryGetValue(row.ClassCode, out var classCode))
            {
                Error(ImportFields.ClassCode, $"Mã lớp '{row.ClassCode}' không có trong danh mục.");
            }
            else if (!classCode.IsActive)
            {
                Error(ImportFields.ClassCode, $"Mã lớp '{row.ClassCode}' đã ngừng dùng.");
            }
            else
            {
                classCodeId = classCode.Id;
            }

            // SĐT tùy chọn, lưu dạng chuỗi (rule 11); cùng mẫu với CHECK ck_students_phone.
            if (row.Phone is not null && !PhonePattern().IsMatch(row.Phone))
            {
                Error(ImportFields.Phone, "SĐT phải gồm 9–14 chữ số, có thể bắt đầu bằng +.");
            }

            // Sinh viên đã có trong danh sách học kỳ ở môn khác: mỗi học kỳ một dòng (uq_semester_enrollments_semester_student).
            Guid? existingId = byCode.TryGetValue(row.StudentCode, out var existing) ? existing.Id : null;
            if (existingId is { } id && reference.Enrollments.TryGetValue(id, out var enrolledCourse) && enrolledCourse != courseCode)
            {
                Error(ImportFields.StudentCode, $"Sinh viên đã có trong danh sách học kỳ ở môn {enrolledCourse}.");
            }

            result.Add(new ImportRowCheck(row, errors, majorId, specializationId, classCodeId, existingId));
        }
        return result;
    }

    private static ImportRow Normalize(ImportRow row) => row with
    {
        StudentCode = row.StudentCode.ToUpperInvariant(),
        SpecializationCode = row.SpecializationCode.ToUpperInvariant(),
        ClassCode = row.ClassCode.ToUpperInvariant(),
        Phone = row.Phone is null ? null : PhoneSeparators().Replace(row.Phone, string.Empty),
    };

    [GeneratedRegex("^[A-Z]{2,3}[0-9]{6}$")]
    private static partial Regex StudentCodePattern();

    [GeneratedRegex("^([A-Z]{2,3})[0-9]{6}$")]
    private static partial Regex StudentCodePrefix();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+?[0-9]{9,14}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"[\s.\-()]")]
    private static partial Regex PhoneSeparators();
}
