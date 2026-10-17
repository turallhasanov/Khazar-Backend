using Khazar.Domain.Common;
using Khazar.Domain.Enums;

namespace Khazar.Domain.Entities;

public class Order : BaseEntity
{
    public const string OrderNumberPrefix = "KHZ-";
    private const int OrderNumberLength = 5;

    // Ambiguous characters (0/O, 1/I) are excluded.
    private const string OrderNumberAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly List<OrderItem> _items = [];

    public string OrderNumber { get; private set; } = default!;
    public string CustomerName { get; private set; } = default!;
    public string CustomerEmail { get; private set; } = default!;
    public decimal TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    public string ShippingAddress { get; private set; } = default!;

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    // Required by ORMs
    private Order() { }

    public Order(string customerName, string customerEmail, string shippingAddress)
    {
        OrderNumber = GenerateOrderNumber();
        CustomerName = Guard.NotEmpty(customerName, nameof(CustomerName));
        CustomerEmail = NormalizeEmail(customerEmail);
        ShippingAddress = Guard.NotEmpty(shippingAddress, nameof(ShippingAddress));
        Status = OrderStatus.Pending;
    }

    /// <summary>Generates an order number in the KHZ-XXXXX format. Uniqueness must be enforced by a database index.</summary>
    public static string GenerateOrderNumber()
    {
        Span<char> chars = stackalloc char[OrderNumberLength];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = OrderNumberAlphabet[Random.Shared.Next(OrderNumberAlphabet.Length)];

        return OrderNumberPrefix + new string(chars);
    }

    /// <summary>Assigns a new number after a collision (e.g. a unique index violation).</summary>
    public void RegenerateOrderNumber()
    {
        EnsureStatus(OrderStatus.Pending);
        OrderNumber = GenerateOrderNumber();
        MarkUpdated();
    }

    public OrderItem AddItem(ProductVariant variant, string productName, decimal unitPrice, int quantity)
    {
        EnsureStatus(OrderStatus.Pending);

        var existing = _items.FirstOrDefault(i => i.VariantId == variant.Id && i.UnitPrice == unitPrice);
        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
        }
        else
        {
            existing = new OrderItem(
                Id, variant.ProductId, variant.Id, productName,
                variant.Size, variant.Color, unitPrice, quantity);
            _items.Add(existing);
        }

        RecalculateTotal();
        return existing;
    }

    public void RemoveItem(Guid orderItemId)
    {
        EnsureStatus(OrderStatus.Pending);

        var item = _items.FirstOrDefault(i => i.Id == orderItemId)
            ?? throw new DomainException("Order item not found.");

        _items.Remove(item);
        RecalculateTotal();
    }

    public void UpdateShippingAddress(string shippingAddress)
    {
        EnsureStatus(OrderStatus.Pending);
        ShippingAddress = Guard.NotEmpty(shippingAddress, nameof(ShippingAddress));
        MarkUpdated();
    }

    public void StartProcessing()
    {
        EnsureStatus(OrderStatus.Pending);
        if (_items.Count == 0)
            throw new DomainException("An order without items cannot be processed.");

        TransitionTo(OrderStatus.Processing);
    }

    public void MarkSyncedToErp()
    {
        EnsureStatus(OrderStatus.Processing);
        TransitionTo(OrderStatus.SyncedToErp);
    }

    public void Complete()
    {
        EnsureStatus(OrderStatus.SyncedToErp);
        TransitionTo(OrderStatus.Completed);
    }

    public void Cancel()
    {
        if (Status is OrderStatus.Completed or OrderStatus.Cancelled)
            throw new DomainException($"An order in {Status} status cannot be cancelled.");

        TransitionTo(OrderStatus.Cancelled);
    }

    private void RecalculateTotal()
    {
        TotalAmount = _items.Sum(i => i.LineTotal);
        MarkUpdated();
    }

    private void TransitionTo(OrderStatus status)
    {
        Status = status;
        MarkUpdated();
    }

    private void EnsureStatus(OrderStatus expected)
    {
        if (Status != expected)
            throw new DomainException(
                $"This operation requires order status {expected}; current status: {Status}.");
    }

    private static string NormalizeEmail(string email)
    {
        var value = Guard.NotEmpty(email, nameof(CustomerEmail));
        var at = value.IndexOf('@');
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
            throw new DomainException("Invalid email address.");

        return value.ToLowerInvariant();
    }
}
