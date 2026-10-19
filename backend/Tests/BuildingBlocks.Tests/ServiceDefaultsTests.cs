using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using BuildingBlocks.Observability;
using BuildingBlocks.Web;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Tests;

public sealed class ServiceDefaultsTests : IAsyncLifetime
{
    private const string SigningKey = "test-signing-key-0123456789abcdef-0123456789";
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = SigningKey,
        });
        builder.AddServiceDefaults("test");
        builder.Services.AddApiControllers(typeof(ServiceDefaultsTests).Assembly)
            .AddApplicationPart(typeof(ServiceDefaultsTests).Assembly);

        _app = builder.Build();
        _app.UseServiceDefaults();
        _app.MapControllers();
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Business_error_becomes_problem_details_with_code_and_trace_id()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/test/conflict");
        request.Headers.Add(CorrelationContext.HeaderName, "abc123");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("abc123", response.Headers.GetValues(CorrelationContext.HeaderName).Single());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("https://exe-team-matching/errors/round-closed", body.GetProperty("type").GetString());
        Assert.Equal("abc123", body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Missing_correlation_id_is_generated()
    {
        var response = await _client.GetAsync("/test/not-found");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var id = response.Headers.GetValues(CorrelationContext.HeaderName).Single();
        Assert.False(string.IsNullOrWhiteSpace(id));
    }

    [Fact]
    public async Task Fluent_validation_returns_400_with_camel_case_field_errors()
    {
        var response = await _client.PostAsJsonAsync("/test/validate", new { name = "", age = 3 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("https://exe-team-matching/errors/validation", body.GetProperty("type").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
        Assert.True(body.GetProperty("errors").TryGetProperty("age", out _));
    }

    [Fact]
    public async Task Unhandled_exception_returns_500_without_details()
    {
        var response = await _client.GetAsync("/test/crash");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("boom", body);
    }

    [Fact]
    public async Task Secured_endpoint_requires_valid_token_and_role()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/test/admin")).StatusCode);

        var student = new HttpRequestMessage(HttpMethod.Get, "/test/admin");
        student.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(Roles.Student));
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(student)).StatusCode);

        var admin = new HttpRequestMessage(HttpMethod.Get, "/test/admin");
        admin.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(Roles.Admin, Roles.Lecturer));
        var ok = await _client.SendAsync(admin);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("Lecturer", await ok.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("advisor", new[] { "Lecturer" }, HttpStatusCode.OK)]
    [InlineData("advisor", new[] { "Mentor" }, HttpStatusCode.OK)]
    [InlineData("advisor", new[] { "Student" }, HttpStatusCode.Forbidden)]
    [InlineData("advisor", new[] { "Admin" }, HttpStatusCode.Forbidden)]
    [InlineData("mentor", new[] { "Lecturer", "Mentor" }, HttpStatusCode.OK)]
    [InlineData("mentor", new[] { "Lecturer" }, HttpStatusCode.Forbidden)]
    [InlineData("internal", new[] { "Admin" }, HttpStatusCode.Forbidden)]
    [InlineData("internal", new[] { "Service" }, HttpStatusCode.OK)]
    public async Task Role_policies_follow_the_roles_claim(string path, string[] roles, HttpStatusCode expected)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/test/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(roles));

        Assert.Equal(expected, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Health_check_is_anonymous()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);
    }

    private static string CreateToken(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, Guid.NewGuid().ToString()),
            new(ClaimNames.Email, "a@example.com"),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimNames.Roles, r)));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "exe-identity",
            Audience = "exe-team-matching",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
        });
    }
}

public sealed record CreateThing(string Name, int Age);

public sealed class CreateThingValidator : AbstractValidator<CreateThing>
{
    public CreateThingValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
    }
}

[ApiController]
[Route("test")]
public sealed class TestController : ControllerBase
{
    [HttpGet("conflict")]
    public IActionResult ThrowConflict() => throw new ConflictException("round-closed", "Đợt đăng ký đã khóa");

    [HttpGet("not-found")]
    public IActionResult Missing() => throw new NotFoundException("nhóm", 42);

    [HttpGet("crash")]
    public IActionResult Crash() => throw new InvalidOperationException("boom");

    [HttpPost("validate")]
    public IActionResult Validate(CreateThing body) => Ok(body);

    [Authorize(Policy = Policies.Admin)]
    [HttpGet("admin")]
    public IActionResult Admin() => Ok(User.GetRoles());

    [Authorize(Policy = Policies.Advisor)]
    [HttpGet("advisor")]
    public IActionResult Advisor() => Ok();

    [Authorize(Policy = Policies.Mentor)]
    [HttpGet("mentor")]
    public IActionResult Mentor() => Ok();

    [Authorize(Policy = Policies.Internal)]
    [HttpGet("internal")]
    public IActionResult Internal() => Ok();
}
