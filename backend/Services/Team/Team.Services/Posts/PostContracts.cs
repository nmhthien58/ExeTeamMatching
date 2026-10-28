using FluentValidation;
using Team.Services.JoinRequests;
using Team.Services.Teams;

namespace Team.Services.Posts;

// Schema của team.yaml (tag Posts). RecruitmentPostResponse dùng chung với TeamDetail (Teams/TeamContracts.cs).

public sealed record RecruitmentPostInput(string Title, string? Description, IReadOnlyList<int>? SpecializationIds);

public sealed record SeekingPostInput(string Title, string? Description, IReadOnlyList<int>? DomainIds);

/// <summary>Schema <c>SeekingPost</c> (S10, S11). <c>skills</c> lấy từ hồ sơ của Academic – lookup nội bộ chưa trả nên hiện rỗng.</summary>
public sealed record SeekingPostResponse(
    Guid Id, string Title, string? Description, IReadOnlyList<int> DomainIds, StudentRef Student, IReadOnlyList<SkillRating> Skills,
    string Status, DateTimeOffset UpdatedAt);

public sealed record SeekingPostQuery
{
    public int? SpecializationId { get; init; }
    public string? Domain { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class RecruitmentPostInputValidator : AbstractValidator<RecruitmentPostInput>
{
    public RecruitmentPostInputValidator()
    {
        RuleFor(x => x.Title)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Nhập tiêu đề bài tuyển.")
            .MaximumLength(150).WithMessage("Tiêu đề tối đa 150 ký tự.");
        RuleFor(x => x.Description).MaximumLength(2000).WithMessage("Mô tả tối đa 2.000 ký tự.");
    }
}

public sealed class SeekingPostInputValidator : AbstractValidator<SeekingPostInput>
{
    public SeekingPostInputValidator()
    {
        RuleFor(x => x.Title)
            .Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Nhập vai trò mong muốn.")
            .MaximumLength(150).WithMessage("Vai trò tối đa 150 ký tự.");
        RuleFor(x => x.Description).MaximumLength(2000).WithMessage("Mô tả tối đa 2.000 ký tự.");
    }
}

public sealed class SeekingPostQueryValidator : AbstractValidator<SeekingPostQuery>
{
    public SeekingPostQueryValidator()
    {
        RuleFor(x => x.Domain).Matches("^D(0[1-9]|1[0-8])$").When(x => x.Domain is not null).WithMessage("domain phải có dạng D01–D18.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
