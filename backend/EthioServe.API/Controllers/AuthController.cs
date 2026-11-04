using EthioServe.API.Configuration;
using EthioServe.API.Contracts;
using EthioServe.API.Security;
using EthioServe.Application.Abstractions;
using EthioServe.Application.Auth;
using EthioServe.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EthioServe.API.Controllers;

// NOTE: no class-level [AllowAnonymous]. It would override [Authorize] on /me. Each public action opts out itself.
[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
[RequestSizeLimit(16 * 1024)]
public sealed class AuthController(IAuthService auth, ICurrentUser currentUser, IEmailService email) : ControllerBase
{
    private const string CsrfHeader = "X-Requested-With";

    [HttpPost("register/customer")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthResponse>> RegisterCustomer(RegisterCustomerRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterCustomerAsync(request, GetClient(), ct);
        RefreshTokenCookie.Set(Response, result.RefreshToken, result.RefreshTokenExpiresAt);

        return Created("/api/auth/me", ToResponse(result));
    }

    [HttpPost("register/provider")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthResponse>> RegisterProvider(RegisterProviderRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterProviderAsync(request, GetClient(), ct);
        RefreshTokenCookie.Set(Response, result.RefreshToken, result.RefreshTokenExpiresAt);

        return Created("/api/auth/me", ToResponse(result));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, GetClient(), ct);
        RefreshTokenCookie.Set(Response, result.RefreshToken, result.RefreshTokenExpiresAt);

        return Ok(ToResponse(result));
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        if (!Request.Headers.ContainsKey(CsrfHeader))
        {
            return Problem(
                detail: $"The {CsrfHeader} header is required.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Access denied");
        }

        var token = Request.Cookies[RefreshTokenCookie.Name] ?? string.Empty;

        try
        {
            var result = await auth.RefreshAsync(token, GetClient(), ct);
            RefreshTokenCookie.Set(Response, result.RefreshToken, result.RefreshTokenExpiresAt);

            return Ok(ToResponse(result));
        }
        catch (UnauthorizedException ex)
        {
            // Returned (not thrown) so the Set-Cookie header that clears the bad cookie survives.
            RefreshTokenCookie.Clear(Response);

            return Problem(detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized, title: "Authentication failed");
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (!Request.Headers.ContainsKey(CsrfHeader))
        {
            return Problem(
                detail: $"The {CsrfHeader} header is required.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Access denied");
        }

        await auth.LogoutAsync(Request.Cookies[RefreshTokenCookie.Name], ct);
        RefreshTokenCookie.Clear(Response);

        return NoContent();
    }

    [HttpGet("capabilities")]
    [AllowAnonymous]
    [ProducesResponseType<AuthCapabilitiesResponse>(StatusCodes.Status200OK)]
    public ActionResult<AuthCapabilitiesResponse> Capabilities() =>
        Ok(new AuthCapabilitiesResponse(email.IsAvailable));

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        if (!email.IsAvailable)
        {
            return Problem(
                detail: "Password recovery is unavailable because email delivery is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Password recovery unavailable");
        }

        await auth.RequestPasswordResetAsync(request, GetClient(), ct);

        // Always the same answer, whether or not the email is registered.
        return Accepted(new { message = "If an account with that email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        if (!email.IsAvailable)
        {
            return Problem(
                detail: "Password recovery is unavailable because email delivery is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Password recovery unavailable");
        }

        await auth.ResetPasswordAsync(request, ct);
        RefreshTokenCookie.Clear(Response);

        return NoContent();
    }
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Invalid access token.");

        return Ok(await auth.GetCurrentUserAsync(userId, ct));
    }

    private static AuthResponse ToResponse(AuthTokens tokens) =>
        new(tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.User);

    private ClientInfo GetClient()
    {
        var userAgent = Request.Headers.UserAgent.ToString();

        return new ClientInfo(
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            userAgent.Length > 300 ? userAgent[..300] : userAgent);
    }
}