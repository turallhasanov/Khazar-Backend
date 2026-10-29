using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.Common.Mappings;
using Khazar.Application.DTOs.Orders;
using Khazar.Domain.Entities;
using Khazar.Domain.Enums;
using Khazar.Domain.Exceptions;

namespace Khazar.Application.Services;

public sealed class AdminOrderAppService(
    IOrderRepository orderRepository,
    IProductRepository productRepository,
    IOrderErpSyncQueue erpSyncQueue) : IAdminOrderAppService
{
    private const string InvalidStatusErrorCode = "invalid_order_status";

    public async Task<IReadOnlyList<OrderResponseDto>> GetAllAsync(string? status, CancellationToken cancellationToken = default)
    {
        OrderStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            filter = Enum.TryParse<OrderStatus>(status.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new ValidationException(
                    $"Unknown order status \"{status}\". Use one of: {string.Join(", ", Enum.GetNames<OrderStatus>())}.",
                    "invalid_status");
        }

        var orders = await orderRepository.GetAllAsync(cancellationToken);
        return orders
            .Where(order => filter is null || order.Status == filter)
            .Select(order => order.ToResponseDto())
            .ToList();
    }

    public async Task<OrderResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(id, cancellationToken)).ToResponseDto();

    public async Task<OrderResponseDto> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken);
        Transition(order, order.Complete);
        await orderRepository.UpdateAsync(order, cancellationToken);
        return order.ToResponseDto();
    }

    public async Task<OrderResponseDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken);
        Transition(order, order.Cancel);

        // The checkout reserved stock for every line; give it back. Products and the order share the request's
        // DbContext, so the save below commits the stock and the status change together.
        foreach (var line in order.Items.GroupBy(item => item.ProductId))
        {
            var product = await productRepository.GetByIdAsync(line.Key, cancellationToken);
            foreach (var item in line)
                product?.Variants.FirstOrDefault(variant => variant.Id == item.VariantId)?.IncreaseStock(item.Quantity);
        }

        await orderRepository.UpdateAsync(order, cancellationToken);
        return order.ToResponseDto();
    }

    public async Task<OrderResponseDto> RetryErpSyncAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken);
        if (order.Status != OrderStatus.Pending)
            throw new ConflictException(
                $"Only Pending orders are pushed to NexusERP; this order is {order.Status}.", InvalidStatusErrorCode);

        erpSyncQueue.Enqueue(order.Id);
        return order.ToResponseDto();
    }

    private async Task<Order> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await orderRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Order), id);

    private static void Transition(Order order, Action transition)
    {
        try
        {
            transition();
        }
        catch (DomainException ex)
        {
            throw new ConflictException($"Order {order.OrderNumber}: {ex.Message}", InvalidStatusErrorCode);
        }
    }
}
