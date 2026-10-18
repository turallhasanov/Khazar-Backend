namespace Khazar.Application.Common.Exceptions;

/// <summary>Thrown when a request is structurally invalid before any business rule is applied.</summary>
public sealed class ValidationException(string message) : Exception(message);
