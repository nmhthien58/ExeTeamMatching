using System.Net.Http.Headers;
using System.Text;
using Academic.Repositories.Context;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Academic.Tests;

/// <summary>
/// Academic.Api trong bộ nhớ với academic_db và Redis thật (xem <see cref="AcademicDbIntegrationTests"/>).
/// Chuỗi kết nối đổi được qua <c>EXE_ACADEMIC_DB</c>, <c>EXE_REDIS</c>. Không chạy Outbox dispatcher.
/// </summary>
public sealed class AcademicApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-only-signing-key-0123456789abcdef-xyz";

    public static string ConnectionString => Environment.GetEnvironmentVariable("EXE_ACADEMIC_DB")
        ?? "Host=localhost;Port=5432;Database=academic_db;Username=academic_svc;Password=academic_dev_pw";

    public static string Redis => Environment.GetEnvironmentVariable("EXE_REDIS") ?? "localhost:6379";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("ConnectionStrings:AcademicDb", ConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", Redis);
        builder.ConfigureTestServices(services =>
        {
            foreach (var hosted in services.Where(d => d.ImplementationType == typeof(OutboxDispatcher<AcademicDbContext>)).ToList())
            {
                services.Remove(hosted);
            }
        });
    }

    public HttpClient ClientAs(params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(roles));
        return client;
    }

    public async Task<T> QueryDbAsync<T>(Func<AcademicDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AcademicDbContext>());
    }

    private static string Token(string[] roles) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "exe-identity",
        Audience = "exe-team-matching",
        Claims = new Dictionary<string, object>
        {
            [ClaimNames.Subject] = Guid.NewGuid().ToString(),
            [ClaimNames.Name] = "Người thử",
            [ClaimNames.Roles] = roles,
        },
        Expires = DateTime.UtcNow.AddMinutes(10),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
    });
}
