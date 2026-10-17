namespace Khazar.Application.DTOs.Orders;

/// <summary>Bound from the "shipping" object of the checkout payload.</summary>
public sealed record ShippingAddressDto
{
    public string City { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;

    /// <summary>Courier option id: "standard" or "express".</summary>
    public string Courier { get; init; } = string.Empty;
}
