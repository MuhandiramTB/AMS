using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TAMS.Application.Auth;
using TAMS.Application.Common.Ports;

namespace TAMS.Api.Controllers;

/// <summary>Authentication endpoints. (05 §4.2, FR-AUTH-*.)</summary>
[EnableRateLimiting("auth")]
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Name of the HttpOnly cookie holding the refresh token (06 §6).</summary>
    private const string RefreshCookie = "tams_refresh";

    public sealed record LoginRequest(string UserName, string Password);

    /// <summary>POST /api/v1/auth/login — exchange credentials for tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<ActionResult<LoginResultDto>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new LoginCommand(request.UserName, request.Password), cancellationToken);
        SetRefreshCookie(result.RefreshToken);
        return Ok(result);
    }

    /// <summary>POST /api/v1/auth/refresh — rotate tokens using the refresh cookie.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResultDto>> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookie];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized();
        }

        var result = await Mediator.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
        SetRefreshCookie(result.RefreshToken);
        return Ok(result);
    }

    /// <summary>POST /api/v1/auth/logout — revoke refresh tokens and clear the cookie.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is { } userId)
        {
            await Mediator.Send(new LogoutCommand(userId), cancellationToken);
        }

        Response.Cookies.Delete(RefreshCookie);
        return NoContent();
    }

    /// <summary>GET /api/v1/auth/me — the current authenticated user + permissions.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    public ActionResult<AuthUserDto> Me([FromServices] ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Unauthorized();
        }

        return Ok(new AuthUserDto(
            currentUser.UserId.Value,
            currentUser.UserName,
            User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList(),
            currentUser.Permissions));
    }

    private void SetRefreshCookie(string refreshToken)
    {
        // HttpOnly + Secure + SameSite keeps the long-lived credential out of JS
        // reach (mitigates XSS token theft, 06 §6). SameSite=Strict is safe here
        // because the SPA and API are same-origin (dev proxy / prod reverse proxy).
        Response.Cookies.Append(RefreshCookie, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth"
        });
    }
}
