using Khazar.Application.Common.Interfaces;
using Khazar.Application.DTOs.Auth;
using Khazar.Application.Services;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Registration with email verification and JWT sign-in. Error responses carry an "errorCode" extension
/// (e.g. "email_not_confirmed", "verification_code_expired") so clients can react to the specific failure.
/// </summary>
[EnableRateLimiting(AuthenticationExtensions.AuthRateLimitPolicy)]
public sealed class AuthController(IAuthAppService authAppService, ICurrentUserService currentUser) : BaseApiController
{
    /// <summary>Creates an unconfirmed account and emails a 6-digit verification code (valid for 15 minutes).</summary>
    /// <param name="dto">Name, email and password (at least 8 characters with a letter and a digit).</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="201">The account was created. If verificationEmailSent is false, offer a resend.</response>
    /// <response code="400">A field is missing or the password is too weak ("weak_password").</response>
    /// <response code="409">The email address is already registered ("email_already_registered").</response>
    /// <response code="429">Too many auth requests from this client.</response>
    [HttpPost("register")]
    [Consumes("application/json")]
    [ProducesResponseType<RegisterResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await authAppService.RegisterAsync(dto, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Confirms the email address with the emailed code and signs the user in.</summary>
    /// <param name="dto">Email address and the 6-digit code.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The address is confirmed; the response holds the access and refresh tokens.</response>
    /// <response code="400">
    /// The code is wrong ("invalid_verification_code"), expired ("verification_code_expired") or out of attempts ("too_many_attempts").
    /// </response>
    /// <response code="409">The address is already confirmed ("email_already_confirmed").</response>
    /// <response code="429">Too many auth requests from this client.</response>
    [HttpPost("verify-email")]
    [Consumes("application/json")]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyEmailAsync([FromBody] VerifyEmailRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await authAppService.VerifyEmailAsync(dto, cancellationToken));

    /// <summary>Emails a new verification code; the previous code stops working.</summary>
    /// <param name="dto">Email address of the pending account.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="202">Accepted. Returned for unknown and already confirmed addresses too.</response>
    /// <response code="429">Too many auth requests from this client.</response>
    [HttpPost("resend-verification")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendVerificationAsync(
        [FromBody] ResendVerificationRequestDto dto, CancellationToken cancellationToken)
    {
        await authAppService.ResendVerificationCodeAsync(dto, cancellationToken);
        return Accepted();
    }

    /// <summary>Signs in with a confirmed email address and password.</summary>
    /// <param name="dto">Email address and password.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The access and refresh tokens.</response>
    /// <response code="401">Wrong credentials ("invalid_credentials") or unconfirmed email address ("email_not_confirmed").</response>
    /// <response code="429">Too many auth requests from this client.</response>
    [HttpPost("login")]
    [Consumes("application/json")]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await authAppService.LoginAsync(dto, cancellationToken));

    /// <summary>Exchanges a refresh token for a new access and refresh token; the presented refresh token stops working.</summary>
    /// <param name="dto">The refresh token from the last auth response.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The new token pair.</response>
    /// <response code="401">The refresh token is unknown, expired or already used ("invalid_refresh_token").</response>
    /// <response code="429">Too many auth requests from this client.</response>
    [HttpPost("refresh-token")]
    [Consumes("application/json")]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequestDto dto, CancellationToken cancellationToken) =>
        Ok(await authAppService.RefreshTokenAsync(dto, cancellationToken));

    /// <summary>Revokes the refresh token. The access token stays valid until it expires (at most 15 minutes).</summary>
    /// <param name="dto">The refresh token to revoke.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="204">Revoked, or the token was unknown or already revoked.</response>
    [HttpPost("logout")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync([FromBody] RefreshTokenRequestDto dto, CancellationToken cancellationToken)
    {
        await authAppService.LogoutAsync(dto, cancellationToken);
        return NoContent();
    }

    /// <summary>Returns the signed-in user.</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The user the access token belongs to.</response>
    /// <response code="401">No valid access token was sent.</response>
    [Authorize]
    [DisableRateLimiting]
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = currentUser.UserId is { } userId
            ? await authAppService.GetUserAsync(userId, cancellationToken)
            : null;

        // A valid token of a deleted account.
        return user is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "The account no longer exists.")
            : Ok(user);
    }
}
