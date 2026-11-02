using Academic.Repositories.Rounds;
using Academic.Services.Lecturers;
using FluentValidation;

namespace Academic.Services.Students;

// Schema của academic.yaml (tag Students) cho hồ sơ sinh viên (A05, S02, S03). JSON camelCase.

/// <summary>Schema <c>SkillRating</c>.</summary>
public sealed record SkillRating(string Skill, int Level);

/// <summary>
/// Schema <c>StudentProfile</c>. Họ tên, MSSV, email, chuyên ngành, lớp, SĐT đến từ file import và chỉ đọc (rule 11, 16).
/// <see cref="ClassCode"/>, <see cref="CourseCode"/> theo danh sách học kỳ hiện hành (null khi không có trong danh sách).
/// <see cref="HasTeam"/>, <see cref="CurrentTeamId"/> thuộc Team Service – tạm thời false / null (xem báo cáo BE-19).
/// </summary>
public sealed record StudentProfileResponse(
    Guid Id, string StudentCode, string FullName, string Email, CatalogRef Specialization, string? ClassCode, string? CourseCode,
    bool HasTeam, bool IsSeekingTeam, string? Phone, bool ContactEmailVisible, IReadOnlyList<SkillRating> Skills,
    string? Description, bool ProfileCompleted, Guid? CurrentTeamId);

/// <summary>
/// Schema <c>StudentSummary</c>. <see cref="Email"/> rỗng với người không được xem liên lạc (rule 11).
/// <see cref="HasTeam"/> thuộc Team Service – tạm thời false (xem BE-18).
/// </summary>
public sealed record StudentSummaryResponse(
    Guid Id, string StudentCode, string FullName, string Email, CatalogRef Specialization, string ClassCode, string CourseCode,
    bool HasTeam, bool IsSeekingTeam);

/// <summary>
/// Query của <c>GET /api/students</c>. <see cref="SemesterId"/> bỏ trống thì lấy học kỳ hiện hành.
/// <see cref="HasTeam"/> nhận theo contract nhưng chưa có tác dụng cho tới khi Team có API trạng thái nhóm.
/// </summary>
public sealed record StudentQuery(
    Guid? SemesterId, string? CourseCode, string? Search, string? ClassCode, int? SpecializationId, bool? HasTeam,
    string? Sort, int Page = 1, int PageSize = 20);

/// <summary>Body của <c>PUT /api/students/me</c>: chỉ 5 kỹ năng, mô tả, công tắc tìm nhóm (rule 16). <see cref="IsSeekingTeam"/> bỏ trống thì giữ nguyên.</summary>
public sealed record StudentProfileInput(IReadOnlyList<SkillRating>? Skills, string? Description, bool? IsSeekingTeam);

/// <summary>5 kỹ năng cố định (CHECK <c>ck_student_skills_skill</c>), theo thứ tự hiển thị.</summary>
public static class SkillNames
{
    public static readonly IReadOnlyList<string> All = ["TECHNOLOGY", "UI_UX_DESIGN", "MANAGEMENT", "MARKETING", "COMMUNICATION_PLANNING"];
}

public sealed class StudentQueryValidator : AbstractValidator<StudentQuery>
{
    private static readonly string[] Sorts = ["name", "-name", "studentCode", "-studentCode"];

    public StudentQueryValidator()
    {
        RuleFor(x => x.CourseCode).Must(c => c is null or CourseCodes.Exe101 or CourseCodes.Exe201).WithMessage("Môn là EXE101 hoặc EXE201.");
        RuleFor(x => x.Sort).Must(s => s is null || Sorts.Contains(s)).WithMessage("sort là name, -name, studentCode hoặc -studentCode.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("page bắt đầu từ 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("pageSize từ 1 đến 100.");
    }
}

public sealed class StudentProfileInputValidator : AbstractValidator<StudentProfileInput>
{
    public StudentProfileInputValidator()
    {
        RuleFor(x => x.Skills).NotNull().WithMessage("Thiếu skills.");
        RuleForEach(x => x.Skills).ChildRules(skill =>
        {
            skill.RuleFor(s => s.Skill).Must(s => SkillNames.All.Contains(s)).WithMessage("Kỹ năng là TECHNOLOGY, UI_UX_DESIGN, MANAGEMENT, MARKETING hoặc COMMUNICATION_PLANNING.");
            skill.RuleFor(s => s.Level).InclusiveBetween(1, 5).WithMessage("Mức kỹ năng từ 1 đến 5.");
        });
        RuleFor(x => x.Description).MaximumLength(500).WithMessage("Mô tả tối đa 500 ký tự.");
    }
}

// Payload sự kiện exe.student (docs/contracts/events.md), key = studentId.

public static class StudentProfileEventTypes
{
    public const string StudentProfileUpdated = "StudentProfileUpdated";
}

public sealed record StudentProfileUpdatedEvent(
    Guid StudentId, string FullName, string Email, int SpecializationId, IReadOnlyList<SkillRating> Skills, bool IsSeekingTeam, bool ProfileCompleted);
