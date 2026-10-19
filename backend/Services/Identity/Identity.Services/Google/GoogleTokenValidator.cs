using BuildingBlocks.Errors;
using Google.Apis.Auth;
using Identity.Services.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Services.Google;

/// <summary>Section <c>Google</c>: Web client ID dùng chung cho web và mobile (docs/setup/google-sign-in.md).</summary>
public sealed class GoogleAuthOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;
}

/// <summary>Thông tin lấy từ Google ID token đã kiểm tra chữ ký, issuer, audience, hạn.</summary>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name);

/// <summary>Tách riêng để test giả lập được, không phải gọi Google.</summary>
public interface IGoogleTokenValidator
{
    /// <summary>Token không hợp lệ → <see cref="HttpStatusException"/> 401 <c>invalid-token</c>.</summary>
    Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct);
}

public sealed class GoogleTokenValidator(IOptions<GoogleAuthOptions> options, ILogger<GoogleTokenValidator> logger)
    : IGoogleTokenValidator
{
    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct)
    {
        var clientId = options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Chưa cấu hình Google:ClientId (biến môi trường Google__ClientId).");
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId],
                IssuedAtClockTolerance = TimeSpan.FromSeconds(30),
                ExpirationTimeClockTolerance = TimeSpan.FromSeconds(30),
            });
            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
        }
        // Token sai định dạng ném lỗi parse (Newtonsoft, base64) chứ không phải InvalidJwtException.
        catch (Exception ex) when (ex is InvalidJwtException or FormatException or ArgumentException or Newtonsoft.Json.JsonException)
        {
            logger.LogInformation("Google ID token không hợp lệ: {Reason}", ex.Message);
            throw AuthErrors.InvalidGoogleToken();
        }
    }
}
