using System.Diagnostics.CodeAnalysis;
using FluentValidation;

namespace Team.Services.Teams;

// Schema của team.yaml (tag Teams). JSON camelCase.

public sealed record CatalogRef(int Id, string? Code, string? Name);

public record StudentRef(Guid Id, string StudentCode, string FullName, CatalogRef Specialization);

/// <summary>Schema <c>Member</c>. Phone / email chưa có nguồn trong Team (snapshot của Academic không có) nên luôn null.</summary>
public sealed record MemberResponse(
    Guid Id, string StudentCode, string FullName, CatalogRef Specialization, string Role, string? Phone, string? Email,
    DateTimeOffset JoinedAt, string? PendingChange) : StudentRef(Id, StudentCode, FullName, Specialization);

public sealed record TeamRequirement(
    int MemberCount, int MinSize, int MaxSize, int SpecializationCount, int MinSpecializations, bool IsValid,
    IReadOnlyList<CatalogRef> MissingSpecializations);

public record TeamCardResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public required bool IsProjectContinued { get; init; }
    public required string CourseCode { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<CatalogRef> Domains { get; init; }
    public required int MemberCount { get; init; }
    public required int OpenSlots { get; init; }
    public StudentRef? Leader { get; init; }
    public string? LecturerName { get; init; }
    public string? MentorName { get; init; }
    public required TeamRequirement Requirement { get; init; }
}

public sealed record ProjectSummary(Guid Id, string Name, string? Description, int CurrentVersion, string OriginCourseCode, string? OriginSemesterCode);

public sealed record RecruitmentPostResponse(Guid Id, string Title, string? Description, IReadOnlyList<int> SpecializationIds, string Status, DateTimeOffset UpdatedAt);

/// <summary>Schema <c>TeamDetail</c>. <c>mentor</c> (MentorAssignment) do BE-74 bổ sung; hiện chỉ có <c>mentorName</c>.</summary>
public sealed record TeamDetailResponse : TeamCardResponse
{
    public TeamDetailResponse()
    {
    }

    /// <summary>Chép các trường của thẻ; phần chi tiết gán bằng object initializer.</summary>
    [SetsRequiredMembers]
    public TeamDetailResponse(TeamCardResponse card) : base(card)
    {
        Members = [];
        MyRelation = "NONE";
    }

    public string? IdeaDescription { get; init; }
    public ProjectSummary? Project { get; init; }
    public required IReadOnlyList<MemberResponse> Members { get; init; }
    public required bool IsInherited { get; init; }
    public required bool IsAutoAssigned { get; init; }
    public RecruitmentPostResponse? RecruitmentPost { get; init; }
    public required bool HasPendingChanges { get; init; }

    /// <summary>LEADER, MEMBER, INVITED, REQUESTED, NONE – chọn nút ở S08.</summary>
    public required string MyRelation { get; init; }

    public DateTimeOffset? FinalizedAt { get; init; }
}

/// <summary>Trả về của <c>GET /api/teams/me/home</c> (S01).</summary>
public sealed record TeamHomeResponse(
    TeamCardResponse? Team, bool IsLeader, int PendingInvitationCount, int PendingJoinRequestCount, string NextAction,
    string Phase, int InterestedLecturerCount, bool IsAutoAssigned);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

/// <summary>Schema <c>TeamInput</c> (S13, S16, W16).</summary>
public sealed record TeamInput(string Name, string? ProjectName, string? IdeaDescription, IReadOnlyList<int> DomainIds, int? PrimaryDomainId);

/// <summary>Query của <c>GET /api/teams</c> (S05, W15).</summary>
public sealed record TeamQuery
{
    public Guid? SemesterId { get; init; }
    public string? CourseCode { get; init; }
    public string? Search { get; init; }
    public string? Domain { get; init; }
    public int? MissingSpecializationId { get; init; }
    public int? MinOpenSlots { get; init; }
    public string? ClassCode { get; init; }
    public string? Status { get; init; }
    public bool InvalidOnly { get; init; }
    public string Sort { get; init; } = "-createdAt";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class TeamInputValidator : AbstractValidator<TeamInput>
{
    public TeamInputValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => n?.Trim().Length >= 2).WithMessage("Tên nhóm cần ít nhất 2 ký tự.")
            .MaximumLength(100).WithMessage("Tên nhóm tối đa 100 ký tự.");
        RuleFor(x => x.ProjectName)
            .Must(n => n is null || n.Trim().Length >= 2).WithMessage("Tên đề tài cần ít nhất 2 ký tự.")
            .MaximumLength(200).WithMessage("Tên đề tài tối đa 200 ký tự.");
        RuleFor(x => x.IdeaDescription).MaximumLength(4000).WithMessage("Mô tả ý tưởng tối đa 4.000 ký tự.");
        RuleFor(x => x.DomainIds)
            .Must(ids => ids is { Count: > 0 }).WithMessage("Chọn ít nhất một domain.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count).WithMessage("Domain bị trùng.");
        RuleFor(x => x.PrimaryDomainId)
            .Must((input, primary) => primary is null || input.DomainIds?.Contains(primary.Value) == true)
            .WithMessage("Domain chính phải nằm trong danh sách domain đã chọn.");
    }
}

public sealed class TeamQueryValidator : AbstractValidator<TeamQuery>
{
    private static readonly string[] Sorts = ["name", "-createdAt", "openSlots", "-openSlots", "relevance"];

    public TeamQueryValidator()
    {
        RuleFor(x => x.CourseCode).Must(c => c is null or "EXE101" or "EXE201").WithMessage("courseCode phải là EXE101 hoặc EXE201.");
        RuleFor(x => x.Status).Must(s => s is null || TeamStatuses.All.Contains(s)).WithMessage("status không hợp lệ.");
        RuleFor(x => x.Domain).Matches("^D(0[1-9]|1[0-8])$").When(x => x.Domain is not null).WithMessage("domain phải có dạng D01–D18.");
        // shortcut: Team không lưu mã lớp của thành viên; cần dữ liệu lớp từ Academic để lọc.
        RuleFor(x => x.ClassCode).Null().WithMessage("Chưa hỗ trợ lọc theo mã lớp.");
        RuleFor(x => x.MinOpenSlots).InclusiveBetween(0, TeamRules.MaxSize).When(x => x.MinOpenSlots is not null);
        RuleFor(x => x.Sort).Must(s => Sorts.Contains(s)).WithMessage("sort không hợp lệ.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
