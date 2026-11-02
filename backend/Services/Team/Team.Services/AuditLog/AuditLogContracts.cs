using FluentValidation;

namespace Team.Services.AuditLog;

// Schema của team.yaml (tag AuditLog). JSON camelCase.

/// <summary>Schema <c>AuditLog</c> (S22, L05, W24). <c>action</c> là mã lưu trong DB (MEMBER_JOINED…).</summary>
public sealed record AuditLogResponse(
    long Id, Guid? TeamId, string Action, string ActorName, string ActorRole, string Summary, string? Reason, DateTimeOffset OccurredAt);

public record AuditLogPageQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>W24 – lọc theo nhóm, người thực hiện, khoảng thời gian.</summary>
public sealed record AuditLogQuery : AuditLogPageQuery
{
    public Guid? TeamId { get; init; }
    public Guid? ActorAccountId { get; init; }
    public string? ActorName { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed class AuditLogPageQueryValidator : AbstractValidator<AuditLogPageQuery>
{
    public AuditLogPageQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class AuditLogQueryValidator : AbstractValidator<AuditLogQuery>
{
    public AuditLogQueryValidator()
    {
        Include(new AuditLogPageQueryValidator());
        RuleFor(x => x.ActorName).MaximumLength(150);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
    }
}
