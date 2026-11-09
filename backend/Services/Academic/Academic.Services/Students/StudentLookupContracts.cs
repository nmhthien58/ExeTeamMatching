using Academic.Repositories.Rounds;
using FluentValidation;

namespace Academic.Services.Students;

// Schema của academic.yaml (tag Internal) cho Team Service. JSON camelCase.

/// <summary>
/// Schema <c>StudentSnapshot</c>. Sinh viên không có trong danh sách học kỳ: <c>isEnrolled = false</c>, <c>courseCode</c> và <c>classCode</c> = null.
/// </summary>
public sealed record StudentSnapshot(Guid Id, string StudentCode, string FullName, int? SpecializationId, string? ClassCode, string? CourseCode, bool IsEnrolled, int MajorId = 0);

/// <summary>Schema <c>SeekingStudent</c> của <c>GET /internal/students/seeking</c>.</summary>
public sealed record SeekingStudent(
    Guid Id, string StudentCode, string FullName, int? SpecializationId, int MajorId, string? Description, IReadOnlyList<SkillRating> Skills);

/// <summary>Body của <c>POST /internal/students/lookup</c>.</summary>
public sealed record StudentLookupRequest(Guid? SemesterId, IReadOnlyList<Guid>? StudentIds);

/// <summary>Query của <c>GET /internal/students</c>: toàn bộ sinh viên của học kỳ, lọc theo môn nếu có.</summary>
public sealed record SemesterStudentsQuery(Guid? SemesterId, string? CourseCode);

/// <summary>Body của <c>POST /internal/students/by-code</c>. Bỏ trống học kỳ thì mọi sinh viên có <c>isEnrolled = false</c>.</summary>
public sealed record StudentByCodeRequest(Guid? SemesterId, IReadOnlyList<string>? StudentCodes);

public sealed class StudentLookupRequestValidator : AbstractValidator<StudentLookupRequest>
{
    public const int MaxIds = 500;

    public StudentLookupRequestValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty().WithMessage("Thiếu semesterId.");
        RuleFor(x => x.StudentIds).NotNull().WithMessage("Thiếu studentIds.")
            .Must(ids => ids!.Count <= MaxIds).When(x => x.StudentIds is not null, ApplyConditionTo.CurrentValidator).WithMessage($"Tối đa {MaxIds} sinh viên mỗi lần.");
    }
}

public sealed class SemesterStudentsQueryValidator : AbstractValidator<SemesterStudentsQuery>
{
    public SemesterStudentsQueryValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty().WithMessage("Thiếu semesterId.");
        RuleFor(x => x.CourseCode).Must(c => c is null or CourseCodes.Exe101 or CourseCodes.Exe201).WithMessage("Môn là EXE101 hoặc EXE201.");
    }
}

public sealed class StudentByCodeRequestValidator : AbstractValidator<StudentByCodeRequest>
{
    public StudentByCodeRequestValidator()
    {
        RuleFor(x => x.StudentCodes).NotNull().WithMessage("Thiếu studentCodes.");
    }
}
