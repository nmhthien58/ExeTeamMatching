using BuildingBlocks.Auth;
using Identity.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers.Auth;

/// <summary>Đăng nhập Google → JWT hệ thống, refresh, logout, thông tin tài khoản (BE-11, BE-12; identity.yaml).</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Đăng nhập bằng Google ID token (A03, W01).</summary>
    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Google(GoogleLoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginWithGoogleAsync(request, HttpContext.Connection.RemoteIpAddress, ct));

    /// <summary>Đổi refresh token lấy cặp token mới (rotation).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(await auth.RefreshAsync(request, HttpContext.Connection.RemoteIpAddress, ct));

    /// <summary>Đăng xuất, thu hồi refresh token (A07).</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(User.GetUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Thông tin tài khoản đang đăng nhập.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfo>> Me(CancellationToken ct) =>
        Ok(await auth.GetMeAsync(User.GetUserId(), ct));
}
