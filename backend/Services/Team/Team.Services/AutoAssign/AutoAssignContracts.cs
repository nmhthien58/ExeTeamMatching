using FluentValidation;
using Team.Services.Teams;

namespace Team.Services.AutoAssign;

// Schema của team.yaml (tag Admin, Internal) cho ghép tự động (rule 14).

/// <summary>Body của <c>POST /internal/teams/auto-assign/apply</c> – phương án Matching.PlanAutoAssignment do Job chuyển sang.</summary>
public sealed record AutoAssignApplyRequest(Guid SemesterId, string CourseCode, IReadOnlyList<AutoAssignFill>? Fills, IReadOnlyList<AutoAssignNewTeam>? NewTeams);

public sealed record AutoAssignFill(Guid TeamId, IReadOnlyList<Guid> StudentIds);

public sealed record AutoAssignNewTeam(string Name, Guid LeaderStudentId, IReadOnlyList<Guid> StudentIds);

/// <summary>Body của <c>POST /api/auto-assignment/run</c> (W20).</summary>
public sealed record AutoAssignRunRequest(Guid SemesterId, string CourseCode);

/// <summary>Query của <c>GET /api/auto-assignment/result</c> (W21).</summary>
public sealed record AutoAssignResultQuery
{
    public Guid SemesterId { get; init; }
    public string? CourseCode { get; init; }
}

/// <summary>Schema <c>AutoAssignResult</c>.</summary>
public sealed record AutoAssignResultResponse(
    DateTimeOffset RunAt, IReadOnlyList<TeamCardResponse> FilledTeams, IReadOnlyList<TeamCardResponse> NewTeams,
    IReadOnlyList<StudentRef> UnassignedStudents);

public sealed class AutoAssignApplyRequestValidator : AbstractValidator<AutoAssignApplyRequest>
{
    public AutoAssignApplyRequestValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.CourseCode).Must(CourseCodes.IsValid).WithMessage(CourseCodes.Message);
        RuleForEach(x => x.NewTeams).ChildRules(t =>
        {
            t.RuleFor(x => x.Name).Must(n => n?.Trim().Length is >= 2 and <= 100).WithMessage("Tên nhóm 2–100 ký tự.");
            t.RuleFor(x => x.StudentIds).NotEmpty();
        });
    }
}

public sealed class AutoAssignRunRequestValidator : AbstractValidator<AutoAssignRunRequest>
{
    public AutoAssignRunRequestValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.CourseCode).Must(CourseCodes.IsValid).WithMessage(CourseCodes.Message);
    }
}

public sealed class AutoAssignResultQueryValidator : AbstractValidator<AutoAssignResultQuery>
{
    public AutoAssignResultQueryValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.CourseCode).Must(c => c is null || CourseCodes.IsValid(c)).WithMessage(CourseCodes.Message);
    }
}

public static class CourseCodes
{
    public static readonly IReadOnlyList<string> All = ["EXE101", "EXE201"];

    public const string Message = "courseCode phải là EXE101 hoặc EXE201.";

    public static bool IsValid(string? courseCode) => courseCode is not null && All.Contains(courseCode);
}
