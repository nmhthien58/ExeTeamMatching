using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Api.Controllers.ClientErrors;
using Identity.Repositories.Accounts;
using Identity.Services;
using Identity.Services.Auth;
using Identity.Services.ClientErrors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Identity.Tests;

/// <summary>Quyền, validate và giới hạn tần suất của API báo cáo lỗi qua HTTP (BE-87).</summary>
public sealed class ClientErrorEndpointTests : IDisposable
{
    private const string ListUrl = "/api/admin/client-errors";
    private const string ReportUrl = "/api/client-errors";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IdentityApiFactory _factory = new();
    private readonly HttpClient _client;

    public ClientErrorEndpointTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private static object Body(string kind = "fatal", string message = "Bad state") => new
    {
        kind,
        message,
        exceptionType = "StateError",
        stackTrace = "#0 main",
        platform = "android",
        appVersion = "1.0.0+1",
        occurredAt = DateTimeOffset.UtcNow,
    };

    private async Task<string> LoginAsync(string email, params (short, Guid?)[] roles)
    {
        await _factory.SeedAccountAsync(email, AccountStatuses.Active, roles);
        var response = await IdentityApiFactory.LoginAsync(_client, email);
        return (await response.Content.ReadFromJsonAsync<TokenResponse>(Json))!.AccessToken;
    }

    [Fact]
    public async Task Report_works_without_login()
    {
        var response = await _client.PostAsJsonAsync(ReportUrl, Body());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var saved = await _factory.WithDbAsync(db => db.ClientErrorReports.SingleAsync());
        Assert.Null(saved.UserId);
    }

    [Fact]
    public async Task Report_with_token_stores_account_id()
    {
        var token = await LoginAsync("sv.b@example.com", (RoleIds.Student, Guid.NewGuid()));

        var response = await _client.SendAsync(IdentityApiFactory.WithBearer(HttpMethod.Post, ReportUrl, token, Body()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var saved = await _factory.WithDbAsync(db => db.ClientErrorReports.SingleAsync());
        Assert.NotNull(saved.UserId);
    }

    [Theory]
    [InlineData("crash", "Bad state", "kind")]
    [InlineData("fatal", "", "message")]
    public async Task Invalid_report_returns_validation_problem(string kind, string message, string field)
    {
        var response = await _client.PostAsJsonAsync(ReportUrl, Body(kind, message));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Too_long_message_is_rejected()
    {
        var response = await _client.PostAsJsonAsync(ReportUrl, Body(message: new string('x', 2001)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Too_many_reports_from_one_ip_return_429_problem()
    {
        using var limited = _factory.WithWebHostBuilder(b => b.UseSetting("ClientErrors:RateLimit:PermitLimit", "2"));
        var client = limited.CreateClient();
        Task<HttpResponseMessage> PostAsync(string ip)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, ReportUrl) { Content = JsonContent.Create(Body()) };
            request.Headers.Add("X-Forwarded-For", ip);
            return client.SendAsync(request);
        }

        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync("203.0.113.1")).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync("203.0.113.1")).StatusCode);
        var rejected = await PostAsync("203.0.113.1");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(rejected.Headers.RetryAfter);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.EndsWith("/rate-limited", problem.GetProperty("type").GetString());

        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync("203.0.113.2")).StatusCode);
    }

    // CR-05 (MB-59): phần đầu header do client tự gửi; khóa là địa chỉ do proxy tin cậy nối vào cuối.
    [Theory]
    [InlineData("203.0.113.9", 1, "203.0.113.9")]
    [InlineData("1.2.3.4, 203.0.113.9", 1, "203.0.113.9")]
    [InlineData("1.2.3.4, 203.0.113.9, 172.18.0.5", 2, "203.0.113.9")]
    [InlineData("203.0.113.9, 172.18.0.5", 2, "203.0.113.9")]
    [InlineData("172.18.0.5", 2, "172.18.0.5")]
    [InlineData("not-an-ip", 1, "10.0.0.1")]
    [InlineData("", 1, "10.0.0.1")]
    public void Client_key_uses_address_appended_by_trusted_proxy(string header, int hops, string expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-For"] = header;

        Assert.Equal(expected, ClientErrorRateLimit.ClientKey(context, hops));
    }

    [Fact]
    public void Spoofed_forwarded_prefix_does_not_change_client_key()
    {
        string Key(string spoofed)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = $"{spoofed}, 203.0.113.9";
            return ClientErrorRateLimit.ClientKey(context);
        }

        Assert.Equal(Key("1.1.1.1"), Key("8.8.8.8"));
    }

    [Fact]
    public async Task Admin_endpoints_need_admin_role()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(ListUrl)).StatusCode);

        var student = await LoginAsync("sv.c@example.com", (RoleIds.Student, Guid.NewGuid()));
        var list = await _client.SendAsync(IdentityApiFactory.WithBearer(HttpMethod.Get, ListUrl, student));
        var summary = await _client.SendAsync(IdentityApiFactory.WithBearer(HttpMethod.Get, ListUrl + "/summary", student));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, summary.StatusCode);
    }

    [Fact]
    public async Task Admin_reads_list_and_summary()
    {
        await _client.PostAsJsonAsync(ReportUrl, Body());
        var admin = await LoginAsync("admin@example.com", (RoleIds.Admin, null));

        var list = await _client.SendAsync(IdentityApiFactory.WithBearer(HttpMethod.Get, ListUrl + "?kind=fatal", admin));
        var summary = await _client.SendAsync(IdentityApiFactory.WithBearer(HttpMethod.Get, ListUrl + "/summary", admin));

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content.ReadFromJsonAsync<PagedResponse<ClientErrorResponse>>(Json))!;
        Assert.Equal("#0 main", Assert.Single(page.Items).StackTrace);
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        Assert.Equal(1, (await summary.Content.ReadFromJsonAsync<ClientErrorSummaryResponse>(Json))!.FatalCount);
    }

    [Theory]
    [InlineData("?kind=crash")]
    [InlineData("?pageSize=101")]
    [InlineData("?from=2026-11-05T00:00:00Z&to=2026-11-04T00:00:00Z")]
    public async Task Invalid_filter_returns_400(string query)
    {
        var admin = await LoginAsync("admin2@example.com", (RoleIds.Admin, null));

        var response = await _client.SendAsync(IdentityApiFactory.WithBearer(HttpMethod.Get, ListUrl + query, admin));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
