using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Repositories.Accounts;
using Identity.Repositories.Context;
using Identity.Services.Auth;
using Identity.Services.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Tests;

/// <summary>
/// Identity.Api chạy trong bộ nhớ: DB InMemory, Google validator giả. <paramref name="environment"/> mặc định
/// Production để kiểm tra endpoint dev không được đăng ký.
/// </summary>
public sealed class IdentityApiFactory(string environment = "Production") : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-only-signing-key-0123456789abcdef-xyz";

    private readonly string _databaseName = "identity-" + Guid.NewGuid();

    public FakeGoogleTokenValidator Google { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("ConnectionStrings:IdentityDb", "Host=unused");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<IdentityDbContext>>();
            services.AddDbContext<IdentityDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.RemoveAll<IGoogleTokenValidator>();
            services.AddSingleton<IGoogleTokenValidator>(Google);
        });
    }

    /// <summary>Thêm tài khoản (như được tạo sẵn từ danh sách import) và đăng ký Google ID token giả cho email đó.</summary>
    public async Task<UserAccount> SeedAccountAsync(string email, string status, params (short RoleId, Guid? Ref)[] roles)
    {
        var account = new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = "Người dùng " + email.Split('@')[0],
            Status = status,
            Roles = roles.Select(r => new UserAccountRole { RoleId = r.RoleId, ExternalRefId = r.Ref }).ToList(),
        };
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.UserAccounts.Add(account);
        await db.SaveChangesAsync();
        Google.Register(email);
        return account;
    }

    public async Task<T> WithDbAsync<T>(Func<IdentityDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IdentityDbContext>());
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string app = LoginClients.Web) =>
        client.PostAsJsonAsync("/api/auth/google", new { idToken = FakeGoogleTokenValidator.TokenFor(email), client = app });

    public static HttpRequestMessage WithBearer(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return request;
    }
}

/// <summary>Google giả: token "google:{email}" hợp lệ với email đã đăng ký; còn lại 401 invalid-token.</summary>
public sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    private readonly Dictionary<string, GoogleIdentity> _tokens = new(StringComparer.Ordinal);

    public static string TokenFor(string email) => "google:" + email;

    public void Register(string email, bool emailVerified = true) =>
        _tokens[TokenFor(email)] = new GoogleIdentity("sub-" + email, email, emailVerified, "Google " + email);

    public Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct) =>
        _tokens.TryGetValue(idToken, out var identity)
            ? Task.FromResult(identity)
            : throw AuthErrors.InvalidGoogleToken();
}
