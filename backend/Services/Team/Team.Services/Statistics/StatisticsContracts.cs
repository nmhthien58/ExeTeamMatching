using FluentValidation;
using Team.Services.AutoAssign;

namespace Team.Services.Statistics;

// Schema của team.yaml (tag Admin, Internal) cho dashboard W02, danh sách W20 và DeadlineReminderJob.

/// <summary>Query của <c>GET /api/statistics/overview</c>; không có <c>semesterId</c> = học kỳ hiện hành, không có môn = cả hai môn.</summary>
public sealed record StatisticsQuery
{
    public Guid? SemesterId { get; init; }
    public string? CourseCode { get; init; }
}

public sealed record CodeCount(string Code, int Count);

/// <summary><c>TeamsBySize</c>: sĩ số → số nhóm, ví dụ <c>{ "4": 8, "5": 52 }</c>.</summary>
public sealed record StatisticsOverviewResponse(
    int StudentsWithTeam, int StudentsWithoutTeam, IReadOnlyDictionary<string, int> TeamsBySize, int PendingChangeRequests,
    int TeamsNeedingReview, IReadOnlyList<CodeCount> BySpecialization, IReadOnlyList<CodeCount> ByDomain);

/// <summary>Query của <c>GET /api/unmatched-students</c> (W20) – học kỳ hiện hành.</summary>
public sealed record UnmatchedStudentsQuery
{
    public string? CourseCode { get; init; }
    public int? SpecializationId { get; init; }

    /// <summary>MSSV hoặc tên (W20 có ô tìm; team.yaml chưa ghi – đề xuất bổ sung).</summary>
    public string? Search { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>Query của <c>GET /internal/deadline-reminder-targets</c>.</summary>
public sealed record DeadlineReminderTargetsQuery
{
    public Guid SemesterId { get; init; }
    public string? CourseCode { get; init; }
}

public sealed record DeadlineReminderTargets(IReadOnlyList<Guid> StudentIdsWithoutTeam, IReadOnlyList<Guid> TeamIdsUnderMinSize);

public sealed class StatisticsQueryValidator : AbstractValidator<StatisticsQuery>
{
    public StatisticsQueryValidator() =>
        RuleFor(x => x.CourseCode).Must(c => c is null || CourseCodes.IsValid(c)).WithMessage(CourseCodes.Message);
}

public sealed class UnmatchedStudentsQueryValidator : AbstractValidator<UnmatchedStudentsQuery>
{
    public UnmatchedStudentsQueryValidator()
    {
        RuleFor(x => x.CourseCode).Must(c => c is null || CourseCodes.IsValid(c)).WithMessage(CourseCodes.Message);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class DeadlineReminderTargetsQueryValidator : AbstractValidator<DeadlineReminderTargetsQuery>
{
    public DeadlineReminderTargetsQueryValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.CourseCode).Must(c => c is null || CourseCodes.IsValid(c)).WithMessage(CourseCodes.Message);
    }
}
