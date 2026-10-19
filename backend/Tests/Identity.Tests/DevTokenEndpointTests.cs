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
}
