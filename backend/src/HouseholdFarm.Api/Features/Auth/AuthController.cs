using HouseholdFarm.Api.Common.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthController(IAuthService authService, RefreshTokenCookie refreshTokenCookie) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AccessTokenResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var session = await authService.LoginAsync(request, ct);
        refreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt);
        return Ok(new AccessTokenResponse(session.AccessToken));
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> GetMeAsync(CancellationToken ct) =>
        Ok(await authService.GetMeAsync(ct));
}
