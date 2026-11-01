using Khazar.Application.Common.Interfaces;
using Khazar.Application.DTOs.Auth;
using Khazar.Application.Services;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Lets the admin panel check, after sign-in, whether the access token belongs to a platform admin (an account listed
/// in "Admin:Emails" or the seeded SuperAdmin). The panel opens only on a 200 from here.
/// </summary>
[Authorize(Policy = AdminAuthorizationExtensions.PlatformAdminPolicy)]
[Route("api/admin/session")]
public sealed class AdminSessionController(IAuthAppService authAppService, ICurrentUserService currentUser) : BaseApiController
{
    /// <summary>Returns the signed-in admin.</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The signed-in user is a platform admin.</response>
    /// <response code="401">No valid access token was sent, or the account no longer exists.</response>
    /// <response code="403">The signed-in user is not a platform admin.</response>
    [HttpGet]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminSessionAsync(CancellationToken cancellationToken)
    {
        var user = currentUser.UserId is { } userId
            ? await authAppService.GetUserAsync(userId, cancellationToken)
            : null;

        return user is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "The account no longer exists.")
            : Ok(user);
    }
}
