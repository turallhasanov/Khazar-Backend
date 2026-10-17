using Khazar.Application.DTOs.Orders;
using Khazar.Domain.Entities;
using Khazar.Domain.Enums;
using Khazar.Domain.ValueObjects;

namespace Khazar.Application.Common.Mappings;

/// <summary>Explicit Order to DTO mappings and checkout DTO to domain value conversions.</summary>
public static class OrderMappings
{
    // The storefront ships within Azerbaijan only.
    private const string DefaultCountry = "Azerbaijan";

    private static readonly string DefaultCurrency = Currency.AZN.ToString();

    public static OrderResponseDto ToResponseDto(this Order order) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        Status = order.Status.ToString(),
        CreatedAt = order.CreatedAt,
        CustomerName = order.CustomerName,
        CustomerEmail = order.CustomerEmail,
        ShippingAddress = order.ShippingAddress,
        Currency = DefaultCurrency,
        TotalAmount = order.TotalAmount,
        ItemCount = order.Items.Sum(i => i.Quantity),
        Items = order.Items.Select(i => i.ToItemDto()).ToList(),
    };

    public static OrderItemDto ToItemDto(this OrderItem item) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        VariantId = item.VariantId,
        Name = item.ProductName,
        Color = item.Color,
        Size = item.Size,
        UnitPrice = item.UnitPrice,
        Quantity = item.Quantity,
        LineTotal = item.LineTotal,
    };

    /// <summary>Builds the full name stored on <see cref="Order.CustomerName"/>.</summary>
    public static string ToFullName(this CustomerDto customer) =>
        $"{customer.FirstName.Trim()} {customer.LastName.Trim()}".Trim();

    /// <summary>Converts checkout shipping data to the domain value object; throws a DomainException on empty parts.</summary>
    public static Address ToAddress(this ShippingAddressDto shipping) =>
        new(shipping.Address, shipping.City, shipping.PostalCode, DefaultCountry);
}
