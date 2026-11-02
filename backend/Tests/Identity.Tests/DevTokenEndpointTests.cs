using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using Identity.Services.Auth;

namespace Identity.Tests;

/// <summary><c>POST /api/dev/token</c> chỉ tồn tại ở Development.</summary>
public sealed class DevTokenEndpointTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Dev_endpoint_is_not_registered_outside_development(string environment)
    {
        using var factory = new IdentityApiFactory(environment);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/dev/token", new { role = "Student" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Development_issues_tokens_usable_for_me_and_refresh()
    {
        using var factory = new IdentityApiFactory("Development");
        using var client = factory.CreateClient();
        var lecturerId = Guid.NewGuid();

        var lecturer = await client.PostAsJsonAsync("/api/dev/token", new { role = "Lecturer", email = "gv.dev@example.com", lecturerId });
        Assert.Equal(HttpStatusCode.OK, lecturer.StatusCode);
        // Gọi lần nữa cùng email với role Mentor → tài khoản có cả hai vai trò.
        var both = (await (await client.PostAsJsonAsync("/api/dev/token", new { role = "mentor", email = "gv.dev@example.com" }))
            .Content.ReadFromJsonAsync<TokenResponse>(Json))!;
        Assert.Equal([Roles.Lecturer, Roles.Mentor], both.User.Roles);
        Assert.Equal(lecturerId, both.User.LecturerId);
        Assert.NotNull(both.User.MentorId);

        var me = await client.SendAsync(IdentityApiFactory.WithBearer(HttpMethod.Get, "/api/auth/me", both.AccessToken));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal("gv.dev@example.com", (await me.Content.ReadFromJsonAsync<UserInfo>(Json))!.Email);

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = both.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task Development_rejects_unknown_role()
    {
        using var factory = new IdentityApiFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/dev/token", new { role = "Leader" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_declares_bearer_scheme_for_the_authorize_button()
    {
        using var factory = new IdentityApiFactory("Development");
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));

        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.True(document.RootElement.GetProperty("security")[0].TryGetProperty("Bearer", out _));
    }
}
