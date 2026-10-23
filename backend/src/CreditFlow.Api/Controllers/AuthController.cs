using CreditFlow.Api.Configuration;
using CreditFlow.Api.Http;
using CreditFlow.Application.Auth;
using CreditFlow.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreditFlow.Api.Controllers;

/// <summary>Authentification : JWT d'accès en réponse, refresh token rotatif en cookie httpOnly.</summary>
[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting(Policies.AuthRateLimit)]
public sealed class AuthController(IAuthService auth, RefreshTokenCookie cookie) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await auth.RegisterAsync(request, cancellationToken);
        return Created("/api/v1/auth/me", user);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await auth.LoginAsync(request, cancellationToken);
        cookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt);
        return Ok(session.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            var session = await auth.RefreshAsync(cookie.Read(Request), cancellationToken);
            cookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt);
            return Ok(session.Response);
        }
        catch (AuthenticationFailedException)
        {
            cookie.Delete(Response);
            throw;
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(cookie.Read(Request), cancellationToken);
        cookie.Delete(Response);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public Task<UserDto> Me(CancellationToken cancellationToken) => auth.GetUserAsync(User.UserId(), cancellationToken);
}
