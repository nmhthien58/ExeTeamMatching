using FluentValidation;
using Identity.Repositories.ClientErrors;

namespace Identity.Services.ClientErrors;

// Schema của identity.yaml (tag ClientErrors). JSON camelCase.

/// <summary>Body của <c>POST /api/client-errors</c>. <c>stackTrace</c> dài hơn giới hạn thì server cắt, không từ chối.</summary>
public sealed record ClientErrorReportRequest
{
    public string Kind { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ExceptionType { get; init; }
    public string? StackTrace { get; init; }
    public string Platform { get; init; } = string.Empty;
    public string AppVersion { get; init; } = string.Empty;
    public string? DeviceModel { get; init; }
    public string? OsVersion { get; init; }
    public Guid? UserId { get; init; }
    public string? Role { get; init; }
    public string? Screen { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }
}

/// <summary>W30 – lọc theo loại, khoảng thời gian (<c>to</c> không tính mốc đó), phiên bản app.</summary>
public sealed record ClientErrorQuery
{
    public string? Kind { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? AppVersion { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>Schema <c>ClientErrorReport</c>.</summary>
public sealed record ClientErrorResponse(
    Guid Id,
    string Kind,
    string Message,
    string ExceptionType,
    string? StackTrace,
    string Platform,
    string AppVersion,
    string? DeviceModel,
    string? OsVersion,
    Guid? UserId,
    string? Role,
    string? Screen,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt);

/// <summary>Schema <c>ClientErrorSummary</c>: 7 ngày gần nhất tính cả hôm nay, theo giờ Việt Nam.</summary>
public sealed record ClientErrorSummaryResponse(
    int FatalCount,
    int NonFatalCount,
    int AffectedUsers,
    IReadOnlyList<ClientErrorDailyCount> Daily,
    IReadOnlyList<ClientErrorTopItem> TopErrors);

public sealed record ClientErrorDailyCount(DateOnly Date, int FatalCount, int NonFatalCount);

public sealed record ClientErrorTopItem(string ExceptionType, string FirstStackLine, int Count, int FatalCount, DateTimeOffset LastOccurredAt);

public sealed class ClientErrorReportRequestValidator : AbstractValidator<ClientErrorReportRequest>
{
    public ClientErrorReportRequestValidator()
    {
        RuleFor(x => x.Kind).Must(ClientErrorRules.IsKind).WithMessage(ClientErrorRules.KindMessage);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ExceptionType).MaximumLength(200);
        RuleFor(x => x.Platform).NotEmpty().MaximumLength(20);
        RuleFor(x => x.AppVersion).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DeviceModel).MaximumLength(100);
        RuleFor(x => x.OsVersion).MaximumLength(100);
        RuleFor(x => x.Role).MaximumLength(20);
        RuleFor(x => x.Screen).MaximumLength(200);
        RuleFor(x => x.OccurredAt).NotNull();
    }
}

public sealed class ClientErrorQueryValidator : AbstractValidator<ClientErrorQuery>
{
    public ClientErrorQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Kind).Must(ClientErrorRules.IsKind).When(x => x.Kind is not null).WithMessage(ClientErrorRules.KindMessage);
        RuleFor(x => x.AppVersion).MaximumLength(30);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
    }
}

internal static class ClientErrorRules
{
    public const string KindMessage = "kind phải là 'fatal' hoặc 'non_fatal'.";

    public static bool IsKind(string? kind) => kind is ClientErrorKinds.Fatal or ClientErrorKinds.NonFatal;
}
