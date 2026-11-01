namespace Khazar.Domain.Exceptions;

/// <summary>Thrown when an order is created or modified with invalid details.</summary>
public sealed class InvalidOrderException() : DomainException("Invalid order details provided");
