using Khazar.Application.DTOs.Orders;

namespace Khazar.Application.Services;

/// <summary>Checkout and order lookup use cases.</summary>
public interface IOrderAppService
{
    /// <summary>
    /// Places an order from a checkout request. Prices come from the catalog, never from the request.
    /// </summary>
    /// <exception cref="Common.Exceptions.ValidationException">The request has no items.</exception>
    /// <exception cref="Common.Exceptions.NotFoundException">A product or variant does not exist.</exception>
    /// <exception cref="Domain.Exceptions.DomainException">A business rule is violated (e.g. insufficient stock).</exception>
    Task<OrderResponseDto> CreateOrderAsync(CreateOrderDto dto, CancellationToken cancellationToken = default);

    Task<OrderResponseDto?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
}
