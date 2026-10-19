using System.Net;
using BuildingBlocks.Errors;
using Identity.Repositories.Accounts;
using Identity.Repositories.Tokens;
using Identity.Services.Google;
using Identity.Services.Tokens;
using Microsoft.Extensions.Logging;

namespace Identity.Services.Auth;

public interface IAuthService
{
    /// <summary>A03, W01 – đổi Google ID token lấy JWT hệ thống (BE-11, rule 5).</summary>
    Task<TokenResponse> LoginWithGoogleAsync(GoogleLoginRequest request, IPAddress? ip, CancellationToken ct);

    /// <summary>Rotation: token cũ bị thu hồi và trỏ sang token mới; dùng lại token cũ → 401 (BE-12).</summary>
    Task<TokenResponse> RefreshAsync(RefreshRequest request, IPAddress? ip, CancellationToken ct);

    /// <summary>A07 – thu hồi refresh token của chính người dùng. Token lạ hoặc đã thu hồi: bỏ qua.</summary>
    Task LogoutAsync(Guid userId, RefreshRequest request, CancellationToken ct);

    Task<UserInfo> GetMeAsync(Guid userId, CancellationToken ct);
}

public sealed class AuthService(
    IGoogleTokenValidator google,
    IUserAccountRepository accounts,
    IRefreshTokenRepository refreshTokens,
    TokenIssuer issuer,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<TokenResponse> LoginWithGoogleAsync(GoogleLoginRequest request, IPAddress? ip, CancellationToken ct)
    {
        var identity = await google.ValidateAsync(request.IdToken, ct);
        if (!identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
        {
            throw AuthErrors.InvalidGoogleToken();
        }

        var now = clock.GetUtcNow();
        var account = await accounts.FindByEmailAsync(identity.Email.Trim(), ct);
        if (account is null || account.Roles.Count == 0)
        {
            logger.LogInformation("Đăng nhập bị từ chối: email chưa có tài khoản");
            throw AuthErrors.NotInList();
        }
        if (account.Status == AccountStatuses.Locked)
        {
            throw AuthErrors.AccountLocked();
        }
        if (account.AllowedUntil is { } until && until <= now)
        {
            throw AuthErrors.NotInList();
        }
        if (request.Client == LoginClients.Mobile && account.Roles.All(r => r.RoleId == RoleIds.Admin))
        {
            throw AuthErrors.WrongClient();
        }
        if (account.GoogleSubject is not null && account.GoogleSubject != identity.Subject)
        {
            // Email đã gắn với một tài khoản Google khác (email bị cấp lại) – không cho đăng nhập thay.
            logger.LogWarning("Google subject không khớp với tài khoản {AccountId}", account.Id);
            throw AuthErrors.InvalidGoogleToken();
        }

        var isFirstLogin = account.Status == AccountStatuses.Pending;
        account.Status = AccountStatuses.Active;
        account.GoogleSubject ??= identity.Subject;
        account.LastLoginAt = now;

        var (response, _) = issuer.Issue(account, ip, isFirstLogin);
        await accounts.SaveChangesAsync(ct);
        logger.LogInformation("Tài khoản {AccountId} đăng nhập ({Client}, lần đầu: {IsFirstLogin})", account.Id, request.Client, isFirstLogin);
        return response;
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest request, IPAddress? ip, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var current = await refreshTokens.FindByHashAsync(TokenIssuer.Hash(request.RefreshToken), ct)
            ?? throw AuthErrors.InvalidRefreshToken();

        if (current.RevokedAt is not null)
        {
            if (current.ReplacedByTokenId is not null)
            {
                // Token đã xoay vòng bị dùng lại: có thể bị lộ → thu hồi cả chuỗi token còn hiệu lực của tài khoản.
                logger.LogWarning("Refresh token đã dùng bị dùng lại, thu hồi mọi phiên của tài khoản {AccountId}", current.UserAccountId);
                await RevokeAllAsync(current.UserAccountId, now, ct);
            }
            throw AuthErrors.InvalidRefreshToken();
        }
        if (current.ExpiresAt <= now)
        {
            throw AuthErrors.InvalidRefreshToken();
        }

        var account = await accounts.GetByIdAsync(current.UserAccountId, ct) ?? throw AuthErrors.InvalidRefreshToken();
        if (account.Status == AccountStatuses.Locked)
        {
            await RevokeAllAsync(account.Id, now, ct);
            throw AuthErrors.AccountLocked(401);
        }
        if (account.Roles.Count == 0 || account.AllowedUntil is { } until && until <= now)
        {
            await RevokeAllAsync(account.Id, now, ct);
            throw AuthErrors.InvalidRefreshToken();
        }

        var (response, next) = issuer.Issue(account, ip, isFirstLogin: false);
        current.RevokedAt = now;
        current.ReplacedByTokenId = next.Id;
        if (!await refreshTokens.TrySaveChangesAsync(ct))
        {
            // Request khác vừa xoay vòng cùng token này.
            throw AuthErrors.InvalidRefreshToken();
        }
        return response;
    }

    public async Task LogoutAsync(Guid userId, RefreshRequest request, CancellationToken ct)
    {
        var token = await refreshTokens.FindByHashAsync(TokenIssuer.Hash(request.RefreshToken), ct);
        if (token is null || token.UserAccountId != userId || token.RevokedAt is not null)
        {
            return;
        }
        token.RevokedAt = clock.GetUtcNow();
        await refreshTokens.TrySaveChangesAsync(ct);
    }

    public async Task<UserInfo> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var account = await accounts.GetByIdAsync(userId, ct) ?? throw new UnauthorizedAccessException();
        if (account.Status == AccountStatuses.Locked)
        {
            throw AuthErrors.AccountLocked();
        }
        return TokenIssuer.ToUserInfo(account, isFirstLogin: false);
    }

    private async Task RevokeAllAsync(Guid userAccountId, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var token in await refreshTokens.ListActiveAsync(userAccountId, ct))
        {
            token.RevokedAt = now;
        }
        await refreshTokens.TrySaveChangesAsync(ct);
    }
}
