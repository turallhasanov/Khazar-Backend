namespace Khazar.Application.Common.Exceptions;

/// <summary>Thrown when an external system (e.g. NexusERP) is unreachable or rejects a request; surfaces as HTTP 502.</summary>
public sealed class ExternalServiceException(string message, string errorCode = ExternalServiceException.DefaultErrorCode)
    : Exception(message)
{
    public const string DefaultErrorCode = "external_service_failed";

    public string ErrorCode { get; } = errorCode;
}
