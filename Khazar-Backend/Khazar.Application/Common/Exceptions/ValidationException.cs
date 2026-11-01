namespace Khazar.Application.Common.Exceptions;

/// <summary>
/// Thrown when a request is structurally invalid before any business rule is applied.
/// <see cref="ErrorCode"/> lets clients tell specific failures apart, e.g. a wrong versus an expired verification code.
/// </summary>
public sealed class ValidationException(string message, string errorCode = ValidationException.DefaultErrorCode)
    : Exception(message)
{
    public const string DefaultErrorCode = "validation_failed";

    public string ErrorCode { get; } = errorCode;
}
