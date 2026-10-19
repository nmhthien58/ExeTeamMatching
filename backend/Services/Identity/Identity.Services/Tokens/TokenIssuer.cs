using System.Net;
using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Auth;
using Identity.Repositories.Accounts;
using Identity.Repositories.Tokens;
using Identity.Services.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Services.Tokens;

/// <summary>
/// Cấp cặp access + refresh token. Refresh token là 32 byte ngẫu nhiên (base64url) trả cho client;
/// DB chỉ giữ SHA-256 hex. Bản ghi được thêm vào DbContext, người gọi tự <c>SaveChanges</c>.
/// </summary>
public sealed class TokenIssuer(
    AccessTokenFactory accessTokens,
    IRefreshTokenRepository refreshTokens,
    IOptions<JwtOptions> options,
    TimeProvider clock)
{
    public (TokenResponse Response, RefreshToken Stored) Issue(UserAccount account, IPAddress? ip, bool isFirstLogin)
    {
        var (accessToken, accessExpiresAt) = accessTokens.Create(account);

        var now = clock.GetUtcNow();
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var stored = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserAccountId = account.Id,
            TokenHash = Hash(raw),
            ExpiresAt = now.AddDays(options.Value.RefreshTokenDays),
            CreatedByIp = ip,
            CreatedAt = now,
        };
        refreshTokens.Add(stored);

        var response = new TokenResponse(accessToken, accessExpiresAt, raw, stored.ExpiresAt, ToUserInfo(account, isFirstLogin));
        return (response, stored);
    }

    public static string Hash(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))).ToLowerInvariant();

    /// <summary>
    /// <c>profileCompleted</c> tạm luôn true: Identity chưa có dữ liệu kỹ năng của Academic (rule 16).
    /// Client kiểm tra hồ sơ qua Academic cho tới khi có nguồn dữ liệu (xem đề xuất ở docs/setup/google-sign-in.md).
    /// </summary>
    public static UserInfo ToUserInfo(UserAccount account, bool isFirstLogin) => new(
        account.Id,
        account.Email,
        account.FullName,
        AccessTokenFactory.RoleCodes(account).ToList(),
        AccessTokenFactory.ExternalRef(account, RoleIds.Student),
        AccessTokenFactory.ExternalRef(account, RoleIds.Lecturer),
        AccessTokenFactory.ExternalRef(account, RoleIds.Mentor),
        isFirstLogin,
        ProfileCompleted: true);
}
