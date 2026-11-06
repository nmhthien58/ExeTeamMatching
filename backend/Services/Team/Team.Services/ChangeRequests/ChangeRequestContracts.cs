using FluentValidation;
using Team.Services.Teams;

namespace Team.Services.ChangeRequests;

public sealed record ChangeRequestInput(IReadOnlyList<Guid> ItemIds, string Reason);
public sealed record ChangeRequestDecisionInput(string? Note);
public sealed record ProjectChangeInput(string Name, string? Description);
public sealed record ChangeRequestQuery
{
    public string? Status { get; init; }
    public Guid? RoundId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record ChangeItemResponse(
    Guid Id, string Action, string Source, StudentRef? Student, string? NewProjectName,
    string? NewProjectDescription, DateTimeOffset CreatedAt);

public sealed record PendingChangesResponse(
    IReadOnlyList<MemberResponse> BaselineMembers, IReadOnlyList<ChangeItemResponse> PendingItems,
    ProjectSummary? CurrentProject, int WouldHaveMemberCount, int WouldHaveSpecializationCount);

public sealed record ChangeRequestResponse(
    Guid Id, TeamCardResponse Team, Guid RoundId, string? RoundName, string Reason, short ChangedMemberCount,
    decimal ChangedPercent, string Status, string? ReviewedByName, DateTimeOffset? ReviewedAt,
    string? ReviewNote, DateTimeOffset CreatedAt);

public sealed record ChangeRequestDetailResponse(
    Guid Id, TeamCardResponse Team, Guid RoundId, string? RoundName, string Reason, short ChangedMemberCount,
    decimal ChangedPercent, string Status, string? ReviewedByName, DateTimeOffset? ReviewedAt,
    string? ReviewNote, DateTimeOffset CreatedAt, IReadOnlyList<MemberResponse> BaselineMembers,
    IReadOnlyList<MemberResponse> MembersAfterChange, IReadOnlyList<ChangeItemResponse> Items,
    ProjectSummary? ProjectBefore, ProjectSummary? ProjectAfter);

public sealed class ChangeRequestInputValidator : AbstractValidator<ChangeRequestInput>
{
    public ChangeRequestInputValidator()
    {
        RuleFor(x => x.ItemIds).NotNull().Must(ids => ids is { Count: > 0 }).WithMessage("Chọn ít nhất một thay đổi.");
        RuleFor(x => x.ItemIds).Must(ids => ids is null || ids.Distinct().Count() == ids.Count).WithMessage("Không chọn trùng thay đổi.");
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(1000);
    }
}

public sealed class ChangeRequestDecisionInputValidator : AbstractValidator<ChangeRequestDecisionInput>
{
    public ChangeRequestDecisionInputValidator() => RuleFor(x => x.Note).MaximumLength(1000);
}

public sealed class ChangeRequestQueryValidator : AbstractValidator<ChangeRequestQuery>
{
    public ChangeRequestQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null or "PENDING" or "APPROVED" or "REJECTED")
            .WithMessage("Trạng thái không hợp lệ.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class ProjectChangeInputValidator : AbstractValidator<ProjectChangeInput>
{
    public ProjectChangeInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}
