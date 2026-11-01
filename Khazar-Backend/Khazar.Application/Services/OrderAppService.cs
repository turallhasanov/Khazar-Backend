using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.Common.Mappings;
using Khazar.Application.Common.Pricing;
using Khazar.Application.DTOs.Orders;
using Khazar.Domain.Entities;

namespace Khazar.Application.Services;

public sealed class OrderAppService(
    IOrderRepository orderRepository,
    IProductRepository productRepository,
    ICurrentUserService currentUser,
    IOrderErpSyncQueue erpSyncQueue,
    IBackgroundEmailQueue emailQueue) : IOrderAppService
{
    public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Items.Count == 0)
            throw new ValidationException("An order must contain at least one item.");

        // Name, email and address rules are enforced by the Order constructor and the Address value object.
        // A signed-in customer's order is linked to the account; guest checkout stays possible.
        var order = new Order(
            dto.Customer.ToFullName(),
            dto.Customer.Email,
            dto.Shipping.ToAddress(),
            currentUser.UserId);

        // Several lines may reference the same product; load each product once.
        var products = new Dictionary<Guid, Product>();

        foreach (var item in dto.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                // An archived product left in a cart is no longer for sale.
                product = await productRepository.GetByIdAsync(item.ProductId, cancellationToken) is { IsActive: true } active
                    ? active
                    : throw new NotFoundException(nameof(Product), item.ProductId);
                products[item.ProductId] = product;
            }

            var variant = product.Variants.FirstOrDefault(v =>
                    v.Color.Equals(item.StockRef.Color.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    v.Size.Equals(item.StockRef.Size.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new NotFoundException(
                    $"Product '{product.Slug}' has no variant with color '{item.StockRef.Color}' and size '{item.StockRef.Size}'.");

            // Reserve stock first: it validates the quantity and throws InsufficientStockException on shortage.
            variant.DecreaseStock(item.Quantity);

            // Unit price is always taken from the catalog; client-sent prices are never trusted.
            order.AddItem(variant, product.Name, product.Price, item.Quantity);
        }

        // The repository persists the order together with the stock changes above in one transaction.
        var created = await orderRepository.CreateAsync(order, cancellationToken);

        // Only after the order is committed: the ERP push runs in the background and never delays checkout.
        erpSyncQueue.Enqueue(created.Id);

        // The receipt is built from the committed order; a mail server failure never fails the checkout.
        var response = created.ToResponseDto();
        var shipping = ShippingFees.Quote(response.TotalAmount, dto.Shipping.Courier);
        emailQueue.Enqueue($"order confirmation {response.OrderNumber}", (sender, ct) =>
            sender.SendOrderConfirmationAsync(response, shipping, ct));

        return response;
    }

    public async Task<OrderResponseDto?> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            return null;

        // The checkout screen displays numbers as "#KHZ-XXXXX"; stored numbers have no '#'.
        var normalized = orderNumber.Trim().TrimStart('#').ToUpperInvariant();

        var order = await orderRepository.GetByNumberAsync(normalized, cancellationToken);
        return order?.ToResponseDto();
    }
}
