namespace Khazar.Application.Common.Pricing;

/// <summary>The shipping option of an order and its price.</summary>
/// <param name="Courier">"standard" or "express".</param>
public sealed record ShippingQuote(string Courier, decimal Fee)
{
    public bool IsExpress => Courier == ShippingFees.ExpressCourier;
}

/// <summary>
/// Shipping prices, mirroring Khazar-Frontend/src/pages/CheckoutPage.jsx (FREE_SHIPPING_THRESHOLD,
/// STANDARD_SHIPPING_FEE, EXPRESS_SURCHARGE) so the order email shows the total the customer saw at checkout.
/// Order.TotalAmount covers items only; keep both places in sync until shipping is priced by the server.
/// </summary>
public static class ShippingFees
{
    public const string StandardCourier = "standard";
    public const string ExpressCourier = "express";

    public const decimal FreeShippingThreshold = 150m;
    public const decimal StandardFee = 5m;
    public const decimal ExpressSurcharge = 10m;

    /// <summary>Unknown courier ids are priced as standard shipping, like the checkout's default option.</summary>
    public static ShippingQuote Quote(decimal subtotal, string? courier)
    {
        var express = string.Equals(courier?.Trim(), ExpressCourier, StringComparison.OrdinalIgnoreCase);
        var baseFee = subtotal >= FreeShippingThreshold ? 0m : StandardFee;
        return new ShippingQuote(express ? ExpressCourier : StandardCourier, baseFee + (express ? ExpressSurcharge : 0m));
    }
}
