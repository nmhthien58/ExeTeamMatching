using System.Net.Http.Headers;
using System.Text;
using BuildingBlocks.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Team.Services.Academic;

// shortcut: chép từ Jobs.Host/Auth/ServiceTokenProvider.cs (chỉ đổi sub/name), chuyển vào BuildingBlocks khi Thiên đồng ý để các service dùng chung.

/// <summary>Token dịch vụ (<c>roles = Service</c>) cho lời gọi <c>/internal/*</c>, ký bằng cùng <c>Jwt:SigningKey</c>.</summary>
public sealed class ServiceTokenProvider(IOptions<JwtOptions> options, TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(1);

    private readonly JsonWebTokenHandler _handler = new();
    private readonly object _lock = new();
    private (string Token, DateTimeOffset ExpiresAt)? _current;

    public string GetToken()
    {
        var now = clock.GetUtcNow();
        lock (_lock)
        {
            if (_current is { } cached && cached.ExpiresAt - now > RenewBefore)
            {
                return cached.Token;
            }

            var jwt = options.Value;
            var expiresAt = now.Add(Lifetime);
            var token = _handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = jwt.Issuer,
                Audience = jwt.Audience,
                Claims = new Dictionary<string, object>
                {
                    [ClaimNames.Subject] = "team",
                    [ClaimNames.Name] = "Team Service",
                    [ClaimNames.Roles] = new[] { Roles.Service },
                },
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256),
            });
            _current = (token, expiresAt);
            return token;
        }
    }
}

/// <summary>Gắn token dịch vụ vào mọi lời gọi HTTP nội bộ.</summary>
public sealed class ServiceTokenHandler(ServiceTokenProvider tokens) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetToken());
        return base.SendAsync(request, cancellationToken);
    }
}
