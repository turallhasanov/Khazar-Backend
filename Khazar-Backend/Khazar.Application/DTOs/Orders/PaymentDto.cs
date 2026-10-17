namespace Khazar.Application.DTOs.Orders;

public sealed record PaymentDto
{
    /// <summary>Payment option id: "card", "cash" or "terminal". Card details are never sent to the API.</summary>
    public string Method { get; init; } = string.Empty;
}
