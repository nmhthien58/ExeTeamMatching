using Identity.Repositories.ClientErrors;

namespace Identity.Services.ClientErrors;

public interface IClientErrorService
{
    /// <param name="request">Báo cáo app gửi.</param>
    /// <param name="authenticatedUserId">Id trong JWT nếu có; được ưu tiên hơn <c>userId</c> trong body.</param>
    /// <param name="ct">Hủy khi client ngắt.</param>
    Task ReportAsync(ClientErrorReportRequest request, Guid? authenticatedUserId, CancellationToken ct);

    Task<PagedResponse<ClientErrorResponse>> SearchAsync(ClientErrorQuery query, CancellationToken ct);

    Task<ClientErrorSummaryResponse> GetSummaryAsync(CancellationToken ct);
}

/// <summary>Nhận và thống kê báo cáo lỗi từ app (BE-87, W30).</summary>
public sealed class ClientErrorService(IClientErrorReportRepository reports, TimeProvider time) : IClientErrorService
{
    public const int MaxStackTraceLength = 8192;
    public const int SummaryDays = 7;
    public const int TopErrorCount = 5;

    private const int MaxFirstStackLineLength = 300;
    private const string UnknownExceptionType = "Unknown";

    /// <summary>Bộ môn đọc số liệu theo ngày Việt Nam, không phải ngày UTC.</summary>
    private static readonly TimeSpan ReportingOffset = TimeSpan.FromHours(7);

    public Task ReportAsync(ClientErrorReportRequest request, Guid? authenticatedUserId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var occurredAt = request.OccurredAt!.Value.ToUniversalTime();
        var stackTrace = Clean(request.StackTrace);
        if (stackTrace?.Length > MaxStackTraceLength)
        {
            stackTrace = stackTrace[..MaxStackTraceLength];
        }

        return reports.AddAsync(new ClientErrorReport
        {
            Id = Guid.NewGuid(),
            Kind = request.Kind,
            Message = Clean(request.Message) ?? UnknownExceptionType,
            ExceptionType = Clean(request.ExceptionType) ?? UnknownExceptionType,
            StackTrace = stackTrace,
            FirstStackLine = FirstLine(stackTrace),
            Platform = Clean(request.Platform) ?? string.Empty,
            AppVersion = Clean(request.AppVersion) ?? string.Empty,
            DeviceModel = Clean(request.DeviceModel),
            OsVersion = Clean(request.OsVersion),
            UserId = authenticatedUserId ?? request.UserId,
            Role = Clean(request.Role),
            Screen = Clean(request.Screen),
            // Đồng hồ máy chạy nhanh không được đẩy lỗi sang tương lai, ngoài khoảng thống kê.
            OccurredAt = occurredAt > now ? now : occurredAt,
            ReceivedAt = now,
        }, ct);
    }

    public async Task<PagedResponse<ClientErrorResponse>> SearchAsync(ClientErrorQuery query, CancellationToken ct)
    {
        var filter = new ClientErrorFilter(query.Kind, query.From?.ToUniversalTime(), query.To?.ToUniversalTime(), query.AppVersion);
        var (items, total) = await reports.SearchAsync(filter, query.Page, query.PageSize, ct);
        return PagedResponse<ClientErrorResponse>.Create(items.Select(ToResponse).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<ClientErrorSummaryResponse> GetSummaryAsync(CancellationToken ct)
    {
        var today = ReportingDay(time.GetUtcNow());
        var firstDay = today.AddDays(1 - SummaryDays);
        var from = new DateTimeOffset(firstDay.ToDateTime(TimeOnly.MinValue), ReportingOffset).ToUniversalTime();

        var occurrences = await reports.ListOccurrencesAsync(from, ct);
        var affectedUsers = await reports.CountAffectedUsersAsync(from, ct);
        var groups = await reports.ListTopGroupsAsync(from, TopErrorCount, ct);

        var byDay = occurrences.ToLookup(x => ReportingDay(x.OccurredAt));
        var daily = Enumerable.Range(0, SummaryDays)
            .Select(i => firstDay.AddDays(i))
            .Select(day => new ClientErrorDailyCount(
                day,
                byDay[day].Count(x => x.Kind == ClientErrorKinds.Fatal),
                byDay[day].Count(x => x.Kind == ClientErrorKinds.NonFatal)))
            .ToList();

        return new ClientErrorSummaryResponse(
            daily.Sum(x => x.FatalCount),
            daily.Sum(x => x.NonFatalCount),
            affectedUsers,
            daily,
            groups.Select(g => new ClientErrorTopItem(g.ExceptionType, g.FirstStackLine, g.Count, g.FatalCount, g.LastOccurredAt)).ToList());
    }

    private static DateOnly ReportingDay(DateTimeOffset moment) => DateOnly.FromDateTime(moment.ToOffset(ReportingOffset).DateTime);

    /// <summary>PostgreSQL không lưu được ký tự NUL, mà endpoint này nhận dữ liệu từ client chưa đăng nhập.</summary>
    private static string? Clean(string? value)
    {
        var cleaned = value?.Replace("\0", string.Empty).Trim();
        return string.IsNullOrEmpty(cleaned) ? null : cleaned;
    }

    private static string FirstLine(string? stackTrace)
    {
        if (stackTrace is null)
        {
            return string.Empty;
        }
        var line = stackTrace.Split('\n', 2)[0].Trim();
        return line.Length > MaxFirstStackLineLength ? line[..MaxFirstStackLineLength] : line;
    }

    private static ClientErrorResponse ToResponse(ClientErrorReport x) => new(
        x.Id, x.Kind, x.Message, x.ExceptionType, x.StackTrace, x.Platform, x.AppVersion, x.DeviceModel, x.OsVersion,
        x.UserId, x.Role, x.Screen, x.OccurredAt, x.ReceivedAt);
}
