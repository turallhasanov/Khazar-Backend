namespace Khazar.Domain.Common;

/// <summary>Thrown when a business rule is violated.</summary>
public class DomainException(string message) : Exception(message);
