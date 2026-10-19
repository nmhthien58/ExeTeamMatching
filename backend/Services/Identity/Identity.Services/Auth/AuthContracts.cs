using FluentValidation;

namespace Identity.Services.Auth;

/// <summary>Body của <c>POST /api/auth/google</c>.</summary>
public sealed record GoogleLoginRequest(string IdToken, string Client);

/// <summary>Body của <c>POST /api/auth/refresh</c> và <c>/api/auth/logout</c>.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Schema <c>TokenResponse</c> trong identity.yaml.</summary>
public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserInfo User);

/// <summary>Schema <c>UserInfo</c> trong identity.yaml.</summary>
public sealed record UserInfo(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    Guid? StudentId,
    Guid? LecturerId,
    Guid? MentorId,
    bool IsFirstLogin,
    bool ProfileCompleted);

/// <summary>Giá trị trường <c>client</c>.</summary>
public static class LoginClients
{
    public const string Web = "web";
    public const string Mobile = "mobile";
}

public sealed class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty().WithMessage("Thiếu Google ID token.");
        RuleFor(x => x.Client)
            .Must(c => c is LoginClients.Web or LoginClients.Mobile)
            .WithMessage("client phải là 'web' hoặc 'mobile'.");
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Thiếu refresh token.");
    }
}
