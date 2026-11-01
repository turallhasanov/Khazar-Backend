namespace Khazar.Domain.Exceptions;

/// <summary>Thrown when a variant does not have enough stock to fulfil a request.</summary>
public sealed class InsufficientStockException(string sku)
    : DomainException($"Insufficient stock for SKU: {sku}")
{
    public string Sku { get; } = sku;
}
