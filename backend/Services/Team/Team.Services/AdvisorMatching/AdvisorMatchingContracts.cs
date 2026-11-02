using BuildingBlocks.Errors;
using FluentValidation;
using Team.Services.LecturerPreferences;
using Team.Services.Teams;
using Team.Services.TopicInterests;

namespace Team.Services.AdvisorMatching;

// Schema của team.yaml (tag AdvisorMatching, Internal). JSON camelCase.

public static class MatchingTriggers
{
    public const string Scheduled = "SCHEDULED";
    public const string Early = "EARLY";
}

public static class MatchingRunStatuses
{
    public const string Preview = "PREVIEW";
    public const string Applied = "APPLIED";
    public const string Discarded = "DISCARDED";
}

public static class AdvisorMatchingEventTypes
{
    public const string LecturerAssigned = "LecturerAssigned";
    public const string AdvisorMatchingCompleted = "AdvisorMatchingCompleted";
}

public static class AdvisorMatchingKinds
{
    /// <summary>
    /// <c>{kind}</c> trên route: <c>LECTURER</c> / <c>MENTOR</c> theo team.yaml (không phân biệt hoa thường);
    /// nhận thêm <c>lecturers</c> / <c>mentors</c> như PROJECT_CONTEXT §5.4 ghi.
    /// </summary>
    public static string Parse(string? kind) => kind?.Trim().ToUpperInvariant() switch
    {
        "LECTURER" or "LECTURERS" => AdvisorKinds.Lecturer,
        "MENTOR" or "MENTORS" => AdvisorKinds.Mentor,
        _ => throw TeamErrors.Invalid("kind", "Loại cố vấn phải là LECTURER hoặc MENTOR."),
    };
}

public static class AdvisorMatchingErrors
{
    // Mã lỗi: docs/contracts/README.md (run-not-preview, mentor-matching-unavailable, academic-unavailable).

    /// <summary>Academic chưa có API danh sách mentor kèm số tối đa / số mong muốn (BE-66) nên chưa ghép mentor được.</summary>
    public static HttpStatusException MentorSourceUnavailable() =>
        new(501, "mentor-matching-unavailable", "Chưa ghép mentor được",
            "Academic Service chưa có API danh sách mentor (số nhóm tối đa, số mong muốn). Ghép mentor sẽ mở khi BE-66 xong.");

    public static ConflictException RunNotPreview(string status) =>
        new("run-not-preview", "Kết quả xem trước không còn dùng được", $"Lần ghép đang ở trạng thái {status}; chỉ áp dụng hoặc bỏ được lần ghép PREVIEW.");

    public static NotFoundException RunNotFound(Guid id) => new("lần ghép cố vấn", id);

    public static NotFoundException NoCurrentSemester() => new("học kỳ hiện hành");

    public static NotFoundException LecturerNotFound(Guid id) => new("giảng viên", id);

    public static ConflictException LecturerFull(string lecturerName, int maxTeams) =>
        new("lecturer-full", "Giảng viên đã đủ số nhóm", $"{lecturerName} đã hướng dẫn {maxTeams} nhóm (tối đa {maxTeams}).");

    public static HttpStatusException AcademicUnavailable() =>
        new(503, "academic-unavailable", "Chưa lấy được danh sách cố vấn", "Academic Service không phản hồi. Thử lại sau ít phút.");
}

/// <summary>Body của <c>POST /internal/advisor-matching/{kind}</c>.</summary>
public sealed record InternalAdvisorMatchingInput(Guid SemesterId, string CourseCode);

/// <summary>Body của <c>POST /api/advisor-matching/{kind}/preview</c>.</summary>
public sealed record AdvisorMatchingPreviewInput(string CourseCode);

/// <summary>Body của <c>PUT /api/teams/{teamId}/instructor</c>.</summary>
public sealed record ChangeInstructorInput(Guid LecturerId, string Note);

public sealed record AdvisorMatchingOverviewQuery(string? CourseCode, bool? WithoutAdvisor, string? Search, int Page = 1, int PageSize = 20);

public sealed class InternalAdvisorMatchingInputValidator : AbstractValidator<InternalAdvisorMatchingInput>
{
    public InternalAdvisorMatchingInputValidator()
    {
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.CourseCode).Must(CourseCodes.IsValid).WithMessage("courseCode phải là EXE101 hoặc EXE201.");
    }
}

public sealed class AdvisorMatchingPreviewInputValidator : AbstractValidator<AdvisorMatchingPreviewInput>
{
    public AdvisorMatchingPreviewInputValidator() =>
        RuleFor(x => x.CourseCode).Must(CourseCodes.IsValid).WithMessage("courseCode phải là EXE101 hoặc EXE201.");
}

public sealed class ChangeInstructorInputValidator : AbstractValidator<ChangeInstructorInput>
{
    public ChangeInstructorInputValidator()
    {
        RuleFor(x => x.LecturerId).NotEmpty();
        RuleFor(x => x.Note).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Cần ghi chú lý do đổi giảng viên.")
            .MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public sealed class AdvisorMatchingOverviewQueryValidator : AbstractValidator<AdvisorMatchingOverviewQuery>
{
    public AdvisorMatchingOverviewQueryValidator()
    {
        RuleFor(x => x.CourseCode).Must(c => c is null || CourseCodes.IsValid(c)).WithMessage("courseCode phải là EXE101 hoặc EXE201.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("pageSize từ 1 đến 100.");
    }
}

public static class CourseCodes
{
    public static bool IsValid(string? code) => code is "EXE101" or "EXE201";
}

/// <summary>Một dòng phương án (cũng là phần tử của cột jsonb <c>advisor_matching_runs.plan</c>).</summary>
public sealed record AdvisorMatchingPlanItem(Guid TeamId, string TeamName, Guid? AdvisorId, string? AdvisorName, string? Reason, int? PreferenceRank);

/// <summary>Schema <c>AdvisorMatchingRun</c>.</summary>
public sealed record AdvisorMatchingRunResponse(
    Guid Id, string AdvisorKind, string CourseCode, string Trigger, string Status, int MatchedCount, int UnmatchedCount,
    DateTimeOffset CreatedAt, DateTimeOffset? AppliedAt, IReadOnlyList<AdvisorMatchingPlanItem> Plan);

/// <summary>Schema <c>AdvisorMatchingRow</c>.</summary>
public sealed record AdvisorMatchingRowResponse(
    TeamCardResponse Team, Guid? AdvisorId, string? AdvisorName, string? Reason, int? PreferenceRank,
    IReadOnlyList<LecturerRef> Preferences, IReadOnlyList<TopicInterestResponse> Interests);

/// <summary>Schema <c>AdvisorLoad</c>.</summary>
public sealed record AdvisorLoadResponse(Guid AdvisorId, string FullName, int AssignedCount, int? PreferredTeams, int? MaxTeams);

/// <summary>Bảng W19 / W28. <c>Page</c>…<c>TotalPages</c> là <c>PagedMeta</c>, tính sau khi lọc <c>withoutAdvisor</c>, <c>search</c>.</summary>
public sealed record AdvisorMatchingOverviewResponse(
    DateTimeOffset? MatchingAt, AdvisorMatchingRunResponse? LastRun, int TeamsWithPreferences, int TeamsWithoutAdvisor,
    IReadOnlyList<AdvisorMatchingRowResponse> Items, IReadOnlyList<AdvisorLoadResponse> AdvisorLoads,
    int Page, int PageSize, int TotalCount, int TotalPages);
