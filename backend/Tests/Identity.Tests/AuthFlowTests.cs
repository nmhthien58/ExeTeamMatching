using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using Identity.Repositories.Accounts;
using Identity.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace Identity.Tests;

/// <summary>Luồng đăng nhập Google → JWT, refresh rotation, logout, /me qua HTTP (BE-11, BE-12).</summary>
public sealed class AuthFlowTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IdentityApiFactory _factory = new();
    private readonly HttpClient _client;

    public AuthFlowTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task First_google_login_activates_pending_account_and_returns_tokens()
    {
        var studentId = Guid.NewGuid();
        var account = await _factory.SeedAccountAsync("sv.a@example.com", AccountStatuses.Pending, (RoleIds.Student, studentId));

        var response = await IdentityApiFactory.LoginAsync(_client, "sv.a@example.com", LoginClients.Mobile);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>(Json))!;
        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
        Assert.True(tokens.RefreshTokenExpiresAt > DateTimeOffset.UtcNow.AddDays(6));
        Assert.Equal(account.Id, tokens.User.Id);
        Assert.Equal([Roles.Student], tokens.User.Roles);
        Assert.Equal(studentId, tokens.User.StudentId);
        Assert.True(tokens.User.IsFirstLogin);

        var stored = await _factory.WithDbAsync(db => db.UserAccounts.SingleAsync(x => x.Id == account.Id));
        Assert.Equal(AccountStatuses.Active, stored.Status);
        Assert.Equal("sub-sv.a@example.com", stored.GoogleSubject);
        Assert.NotNull(stored.LastLoginAt);
        var hashes = await _factory.WithDbAsync(db => db.RefreshTokens.Select(x => x.TokenHash).ToListAsync());
        Assert.DoesNotContain(tokens.RefreshToken, hashes);
        Assert.All(hashes, h => Assert.Equal(64, h.Length));

        // Lần sau không còn là lần đầu.
        var again = (await (await IdentityApiFactory.LoginAsync(_client, "sv.a@example.com")).Content.ReadFromJsonAsync<TokenResponse>(Json))!;
        Assert.False(again.User.IsFirstLogin);
    }

    [Fact]
    public async Task Email_without_account_is_rejected_with_not_in_list()
    {
        _factory.Google.Register("la@example.com");

        var response = await IdentityApiFactory.LoginAsync(_client, "la@example.com");

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "not-in-list");
    }

    [Fact]
    public async Task Invalid_google_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/google", new { idToken = "khong-hop-le", client = "web" });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "invalid-token");
    }

    [Fact]
    public async Task Unknown_client_is_a_validation_error()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/google", new { idToken = "x", client = "desktop" });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task Locked_account_is_rejected()
    {
        await _factory.SeedAccountAsync("khoa@example.com", AccountStatuses.Locked, (RoleIds.Student, Guid.NewGuid()));

        var response = await IdentityApiFactory.LoginAsync(_client, "khoa@example.com");

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "account-locked");
    }

    [Fact]
    public async Task Admin_only_account_cannot_use_mobile_but_can_use_web()
    {
        await _factory.SeedAccountAsync("bomon@example.com", AccountStatuses.Pending, (RoleIds.Admin, null));

        await AssertProblemAsync(await IdentityApiFactory.LoginAsync(_client, "bomon@example.com", LoginClients.Mobile), HttpStatusCode.Forbidden, "wrong-client");
        Assert.Equal(HttpStatusCode.OK, (await IdentityApiFactory.LoginAsync(_client, "bomon@example.com", LoginClients.Web)).StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_and_reusing_old_token_is_rejected()
    {
        await _factory.SeedAccountAsync("gv@example.com", AccountStatuses.Active, (RoleIds.Lecturer, Guid.NewGuid()), (RoleIds.Mentor, Guid.NewGuid()));
        var login = (await (await IdentityApiFactory.LoginAsync(_client, "gv@example.com")).Content.ReadFromJsonAsync<TokenResponse>(Json))!;

        var refreshed = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var next = (await refreshed.Content.ReadFromJsonAsync<TokenResponse>(Json))!;
        Assert.NotEqual(login.RefreshToken, next.RefreshToken);
        Assert.Equal([Roles.Lecturer, Roles.Mentor], next.User.Roles);

        // Dùng lại token cũ → 401, và cả chuỗi phiên bị thu hồi (token mới cũng hết dùng được).
        await AssertProblemAsync(await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken }), HttpStatusCode.Unauthorized, "invalid-refresh-token");
        await AssertProblemAsync(await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = next.RefreshToken }), HttpStatusCode.Unauthorized, "invalid-refresh-token");
    }

    [Fact]
    public async Task Locked_account_cannot_refresh()
    {
        var account = await _factory.SeedAccountAsync("sv.b@example.com", AccountStatuses.Active, (RoleIds.Student, Guid.NewGuid()));
        var login = (await (await IdentityApiFactory.LoginAsync(_client, "sv.b@example.com")).Content.ReadFromJsonAsync<TokenResponse>(Json))!;
        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.UserAccounts.SingleAsync(x => x.Id == account.Id);
            stored.Status = AccountStatuses.Locked;
            return await db.SaveChangesAsync();
        });

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "account-locked");
    }

    [Fact]
    public async Task Logout_revokes_refresh_token_and_me_returns_user_info()
    {
        var mentorId = Guid.NewGuid();
        await _factory.SeedAccountAsync("mentor@example.com", AccountStatuses.Active, (RoleIds.Mentor, mentorId));
        var login = (await (await IdentityApiFactory.LoginAsync(_client, "mentor@example.com", LoginClients.Mobile)).Content.ReadFromJsonAsync<TokenResponse>(Json))!;

        var me = await _client.SendAsync(IdentityApiFactory.WithBearer(HttpMethod.Get, "/api/auth/me", login.AccessToken));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = (await me.Content.ReadFromJsonAsync<UserInfo>(Json))!;
        Assert.Equal("mentor@example.com", user.Email);
        Assert.Equal(mentorId, user.MentorId);
        Assert.Null(user.StudentId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = login.RefreshToken })).StatusCode);

        var logout = await _client.SendAsync(IdentityApiFactory.WithBearer(HttpMethod.Post, "/api/auth/logout", login.AccessToken, new { refreshToken = login.RefreshToken }));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await AssertProblemAsync(await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken }), HttpStatusCode.Unauthorized, "invalid-refresh-token");
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"{(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal("https://exe-team-matching/errors/" + code, problem.RootElement.GetProperty("type").GetString());
    }
}
