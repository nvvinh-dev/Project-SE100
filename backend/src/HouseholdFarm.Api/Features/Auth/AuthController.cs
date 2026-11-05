using HouseholdFarm.Api.Common.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthController(IAuthService authService, RefreshTokenCookie refreshTokenCookie) : ControllerBase
{
    // Không có địa chỉ để xem lại riêng tài khoản vừa tạo: người dùng đăng nhập rồi đọc /api/auth/me.
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<CurrentUserResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct) =>
        Created((string?)null, await authService.RegisterAsync(request, ct));

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AccessTokenResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var session = await authService.LoginAsync(request, ct);
        refreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt);
        return Ok(new AccessTokenResponse(session.AccessToken));
    }

    // Không cần access token: frontend gọi khi access token đã hết hạn hoặc khi vừa tải lại trang.
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AccessTokenResponse>> RefreshAsync(CancellationToken ct)
    {
        var session = await authService.RefreshAsync(RefreshTokenCookie.Read(Request), ct);
        refreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt);
        return Ok(new AccessTokenResponse(session.AccessToken));
    }

    // Không cần access token: phiên đã hết hạn vẫn đăng xuất được.
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct)
    {
        await authService.LogoutAsync(RefreshTokenCookie.Read(Request), ct);
        refreshTokenCookie.Delete(Response);
        return NoContent();
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        await authService.ChangePasswordAsync(request, ct);
        refreshTokenCookie.Delete(Response);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> GetMeAsync(CancellationToken ct) =>
        Ok(await authService.GetMeAsync(ct));

    [HttpPut("me")]
    public async Task<ActionResult<CurrentUserResponse>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await authService.UpdateProfileAsync(request, ct));
}
