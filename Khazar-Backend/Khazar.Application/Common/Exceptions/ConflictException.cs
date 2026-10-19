namespace Khazar.Application.Common.Exceptions;

/// <summary>Thrown when a request conflicts with existing data, e.g. an email address that is already registered.</summary>
public sealed class ConflictException(string message, string errorCode = ConflictException.DefaultErrorCode)
    : Exception(message)
{
    public const string DefaultErrorCode = "conflict";

    public string ErrorCode { get; } = errorCode;
}
