namespace Khazar.Application.Common.Exceptions;

/// <summary>
/// Thrown when credentials or tokens are rejected. <see cref="ErrorCode"/> lets clients tell the reasons apart,
/// e.g. "email_not_confirmed" sends the user to the verification screen instead of showing a login error.
/// </summary>
public sealed class UnauthorizedException(string message, string errorCode = UnauthorizedException.DefaultErrorCode)
    : Exception(message)
{
    public const string DefaultErrorCode = "unauthorized";

    public string ErrorCode { get; } = errorCode;
}
