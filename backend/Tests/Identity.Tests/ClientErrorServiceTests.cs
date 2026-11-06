using Identity.Repositories.ClientErrors;
using Identity.Repositories.Context;
using Identity.Services.ClientErrors;
using Microsoft.EntityFrameworkCore;

namespace Identity.Tests;

/// <summary>Service báo cáo lỗi với repository thật trên DB InMemory (BE-87).</summary>
public sealed class ClientErrorServiceTests : IDisposable
{
    // 16:30 UTC = 23:30 ngày 05/11 ở Việt Nam.
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 16, 30, 0, TimeSpan.Zero);

    private readonly IdentityDbContext _db = new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseInMemoryDatabase("client-errors-" + Guid.NewGuid()).Options);
    private readonly ClientErrorService _service;

    public ClientErrorServiceTests() => _service = new ClientErrorService(new ClientErrorReportRepository(_db), new FixedClock());

    public void Dispose() => _db.Dispose();

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static ClientErrorReportRequest Request(string kind = ClientErrorKinds.NonFatal, DateTimeOffset? occurredAt = null) => new()
    {
        Kind = kind,
        Message = "Bad state",
        ExceptionType = "StateError",
        StackTrace = "#0 main (package:app/main.dart:1:1)\n#1 run (dart:async)",
        Platform = "android",
        AppVersion = "1.0.0+1",
        OccurredAt = occurredAt ?? Now.AddMinutes(-1),
    };

    [Fact]
    public async Task Report_saves_utc_time_and_first_stack_line()
    {
        var localTime = new DateTimeOffset(2026, 11, 5, 22, 0, 0, TimeSpan.FromHours(7));

        await _service.ReportAsync(Request(occurredAt: localTime), null, default);

        var saved = await _db.ClientErrorReports.SingleAsync();
        Assert.Equal(TimeSpan.Zero, saved.OccurredAt.Offset);
        Assert.Equal(localTime, saved.OccurredAt);
        Assert.Equal(Now, saved.ReceivedAt);
        Assert.Equal("#0 main (package:app/main.dart:1:1)", saved.FirstStackLine);
    }

    [Fact]
    public async Task Report_cuts_stack_trace_and_removes_nul_characters()
    {
        var request = Request() with { Message = "a\0b", StackTrace = new string('x', ClientErrorService.MaxStackTraceLength + 500) };

        await _service.ReportAsync(request, null, default);

        var saved = await _db.ClientErrorReports.SingleAsync();
        Assert.Equal("ab", saved.Message);
        Assert.Equal(ClientErrorService.MaxStackTraceLength, saved.StackTrace!.Length);
        Assert.Equal(300, saved.FirstStackLine.Length);
    }

    [Fact]
    public async Task Report_without_exception_type_or_stack_uses_defaults()
    {
        await _service.ReportAsync(Request() with { ExceptionType = "  ", StackTrace = null }, null, default);

        var saved = await _db.ClientErrorReports.SingleAsync();
        Assert.Equal("Unknown", saved.ExceptionType);
        Assert.Equal(string.Empty, saved.FirstStackLine);
    }

    [Fact]
    public async Task Report_prefers_user_id_from_token_over_body()
    {
        var fromToken = Guid.NewGuid();
        var fromBody = Guid.NewGuid();

        await _service.ReportAsync(Request() with { UserId = fromBody }, fromToken, default);
        await _service.ReportAsync(Request() with { UserId = fromBody }, null, default);

        var ids = await _db.ClientErrorReports.Select(x => x.UserId).ToListAsync();
        Assert.Equal(2, ids.Count);
        Assert.Contains(fromToken, ids);
        Assert.Contains(fromBody, ids);
    }

    [Fact]
    public async Task Report_from_device_with_fast_clock_is_not_in_the_future()
    {
        await _service.ReportAsync(Request(occurredAt: Now.AddDays(2)), null, default);

        Assert.Equal(Now, (await _db.ClientErrorReports.SingleAsync()).OccurredAt);
    }

    [Fact]
    public async Task Search_filters_and_pages_newest_first()
    {
        await _service.ReportAsync(Request(ClientErrorKinds.Fatal, Now.AddHours(-3)), null, default);
        await _service.ReportAsync(Request(ClientErrorKinds.Fatal, Now.AddHours(-2)), null, default);
        await _service.ReportAsync(Request(ClientErrorKinds.Fatal, Now.AddHours(-1)) with { AppVersion = "2.0.0" }, null, default);
        await _service.ReportAsync(Request(ClientErrorKinds.NonFatal, Now.AddHours(-1)), null, default);

        var page = await _service.SearchAsync(new ClientErrorQuery { Kind = ClientErrorKinds.Fatal, PageSize = 2 }, default);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(Now.AddHours(-1), page.Items[0].OccurredAt);

        var second = await _service.SearchAsync(new ClientErrorQuery { Kind = ClientErrorKinds.Fatal, Page = 2, PageSize = 2 }, default);
        Assert.Equal(Now.AddHours(-3), Assert.Single(second.Items).OccurredAt);

        var byVersion = await _service.SearchAsync(new ClientErrorQuery { AppVersion = "2.0.0" }, default);
        Assert.Equal(1, byVersion.TotalCount);

        var byRange = await _service.SearchAsync(new ClientErrorQuery { From = Now.AddHours(-3), To = Now.AddHours(-2) }, default);
        Assert.Equal(Now.AddHours(-3), Assert.Single(byRange.Items).OccurredAt);
    }

    [Fact]
    public async Task Summary_counts_seven_days_on_vietnam_calendar()
    {
        var user = Guid.NewGuid();
        // 18:00 UTC ngày 04/11 là 01:00 ngày 05/11 ở Việt Nam; 16:00 UTC vẫn là 23:00 ngày 04/11.
        await _service.ReportAsync(Request(ClientErrorKinds.Fatal, new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero)) with { UserId = user }, null, default);
        await _service.ReportAsync(Request(ClientErrorKinds.NonFatal, new DateTimeOffset(2026, 11, 4, 16, 0, 0, TimeSpan.Zero)) with { UserId = user }, null, default);
        await _service.ReportAsync(Request(ClientErrorKinds.NonFatal, new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.FromHours(7))) with { UserId = Guid.NewGuid() }, null, default);
        await _service.ReportAsync(Request(ClientErrorKinds.NonFatal, Now.AddMinutes(-5)), null, default);
        // Ngoài cửa sổ: 23:59 ngày 29/10 ở Việt Nam.
        await _service.ReportAsync(Request(ClientErrorKinds.Fatal, new DateTimeOffset(2026, 10, 29, 23, 59, 0, TimeSpan.FromHours(7))), null, default);

        var summary = await _service.GetSummaryAsync(default);

        Assert.Equal(1, summary.FatalCount);
        Assert.Equal(3, summary.NonFatalCount);
        Assert.Equal(2, summary.AffectedUsers);
        Assert.Equal(ClientErrorService.SummaryDays, summary.Daily.Count);
        Assert.Equal(new DateOnly(2026, 10, 30), summary.Daily[0].Date);
        Assert.Equal(new DateOnly(2026, 11, 5), summary.Daily[^1].Date);
        Assert.Equal(1, summary.Daily[0].NonFatalCount);
        Assert.Equal(1, summary.Daily[^1].FatalCount);
        Assert.Equal(1, summary.Daily[^1].NonFatalCount);
        Assert.Equal(1, summary.Daily[^2].NonFatalCount);
    }

    [Fact]
    public async Task Summary_groups_repeated_errors_and_keeps_top_five()
    {
        for (var i = 0; i < 3; i++)
        {
            await _service.ReportAsync(Request(i == 0 ? ClientErrorKinds.Fatal : ClientErrorKinds.NonFatal), null, default);
        }
        for (var i = 0; i < 6; i++)
        {
            await _service.ReportAsync(Request() with { ExceptionType = "Error" + i }, null, default);
        }

        var summary = await _service.GetSummaryAsync(default);

        Assert.Equal(ClientErrorService.TopErrorCount, summary.TopErrors.Count);
        var top = summary.TopErrors[0];
        Assert.Equal("StateError", top.ExceptionType);
        Assert.Equal("#0 main (package:app/main.dart:1:1)", top.FirstStackLine);
        Assert.Equal(3, top.Count);
        Assert.Equal(1, top.FatalCount);
    }
}
