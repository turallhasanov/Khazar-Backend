using Khazar.Application.Common.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Khazar.WebAPI.Authentication;

/// <summary>
/// Reads the user id from the "sub" claim of the validated access token. Endpoints without [Authorize] still see the
/// user when a valid token is sent, so checkout can link orders without requiring sign-in.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            return Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;
        }
    }
}
