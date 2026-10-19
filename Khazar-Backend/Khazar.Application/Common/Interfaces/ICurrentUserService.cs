namespace Khazar.Application.Common.Interfaces;

/// <summary>The caller of the current request, as identified by its access token.</summary>
public interface ICurrentUserService
{
    /// <summary>Id of the signed-in user, or null for anonymous requests.</summary>
    Guid? UserId { get; }
}
