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
    IOrderErpSyncQueue erpSyncQueue,
    INexusErpClient erpClient) : IAdminOrderAppService
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
            // Cancelled orders (what "deleting" an order does) are listed only when asked for with status=Cancelled.
            .Where(order => filter is null ? order.Status != OrderStatus.Cancelled : order.Status == filter)
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

    public async Task<OrderResponseDto> CancelAsync(Guid id, CancellationToken cancellationToken = default) =>
        await CancelAsync(await LoadAsync(id, cancellationToken), notifyErp: true, cancellationToken);

    public async Task<OrderResponseDto> CancelFromErpAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        var number = orderNumber?.Trim();
        var found = string.IsNullOrEmpty(number) ? null : await orderRepository.GetByNumberAsync(number, cancellationToken);
        var order = found is null
            ? throw new NotFoundException(nameof(Order), orderNumber ?? string.Empty)
            : await LoadAsync(found.Id, cancellationToken);

        // NexusERP may repeat the request after a lost response; the stock was already returned the first time.
        if (order.Status == OrderStatus.Cancelled)
            return order.ToResponseDto();

        // NexusERP is the one cancelling, so it is not told again.
        return await CancelAsync(order, notifyErp: false, cancellationToken);
    }

    private async Task<OrderResponseDto> CancelAsync(Order order, bool notifyErp, CancellationToken cancellationToken)
    {
        // Pending orders never reached NexusERP (a failed push leaves them Pending), so only these have a sale to cancel.
        var inErp = notifyErp && order.Status is OrderStatus.Processing or OrderStatus.SyncedToErp;
        Transition(order, order.Cancel);

        // NexusERP first: if it cannot cancel, nothing changes here either, so the two systems never disagree.
        // NexusERP returns its own stock; Khazar's stock is restored below.
        if (inErp)
        {
            var cancelled = await erpClient.CancelOrderInErpAsync(order.OrderNumber, cancellationToken);
            if (!cancelled.Success)
                throw new ExternalServiceException(
                    $"Order {order.OrderNumber} was not cancelled in NexusERP, so it was not cancelled here: {cancelled.Error}",
                    "nexus_cancel_failed");
        }

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

    public async Task<OrderResponseDto> SyncToNexusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken);
        if (order.Status != OrderStatus.Pending)
            throw new ConflictException(
                $"Only Pending orders are pushed to NexusERP; this order is {order.Status}.", InvalidStatusErrorCode);

        // The client logs the reason (missing StoreId, unknown SKU, HTTP error) itself.
        var push = await erpClient.PushOrderToErpAsync(order, cancellationToken);
        if (!push.Success)
            throw new ExternalServiceException(
                $"Order {order.OrderNumber} was not accepted by NexusERP: {push.Error}", "nexus_sync_failed");

        order.StartProcessing();
        order.MarkSyncedToErp();
        await orderRepository.UpdateAsync(order, cancellationToken);
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
