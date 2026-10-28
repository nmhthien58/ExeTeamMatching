using FluentValidation;

namespace Academic.Services.Students;

// Schema của academic.yaml (tag Internal) cho Team Service. JSON camelCase.

/// <summary>Schema <c>StudentSnapshot</c>. Sinh viên không có trong danh sách học kỳ: <c>isEnrolled = false</c>, <c>courseCode = null</c>.</summary>
public sealed record StudentSnapshot(Guid Id, string StudentCode, string FullName, int SpecializationId, string? CourseCode, bool IsEnrolled);

/// <summary>Body của <c>POST /internal/students/lookup</c>.</summary>
public sealed record StudentLookupRequest(Guid? SemesterId, IReadOnlyList<Guid>? StudentIds);

/// <summary>Body của <c>POST /internal/students/by-code</c>. Bỏ trống học kỳ thì mọi sinh viên có <c>isEnrolled = false</c>.</summary>
public sealed record StudentByCodeRequest(Guid? SemesterId, IReadOnlyList<string>? StudentCodes);

public sealed class StudentLookupRequestValidator : AbstractValidator<StudentLookupRequest>
{
    public const int MaxIds = 500;

    public StudentLookupRequestValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty().WithMessage("Thiếu semesterId.");
        RuleFor(x => x.StudentIds).NotNull().WithMessage("Thiếu studentIds.")
            .Must(ids => ids!.Count <= MaxIds).When(x => x.StudentIds is not null).WithMessage($"Tối đa {MaxIds} sinh viên mỗi lần.");
    }
}

public sealed class StudentByCodeRequestValidator : AbstractValidator<StudentByCodeRequest>
{
    public StudentByCodeRequestValidator()
    {
        RuleFor(x => x.StudentCodes).NotNull().WithMessage("Thiếu studentCodes.");
    }
}
