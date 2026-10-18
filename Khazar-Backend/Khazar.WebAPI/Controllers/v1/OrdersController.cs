using Khazar.Application.DTOs.Orders;
using Khazar.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>Checkout and order lookup endpoints.</summary>
public sealed class OrdersController(IOrderAppService orderAppService, ILogger<OrdersController> logger)
    : BaseApiController
{
    // MVC strips the "Async" suffix from action names, so CreatedAtAction(nameof(GetByNumberAsync)) would not
    // resolve; a named route avoids that.
    private const string GetByNumberRouteName = "GetOrderByNumber";

    /// <summary>
    /// Places an order. The server generates the KHZ-XXXXX number and recalculates all prices from the catalog;
    /// client-sent totals are ignored. Validation errors return 400, unknown products or variants 404,
    /// and insufficient stock 409.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOrderAsync([FromBody] CreateOrderDto dto, CancellationToken cancellationToken)
    {
        var order = await orderAppService.CreateOrderAsync(dto, cancellationToken);

        logger.LogInformation(
            "Order {OrderNumber} created with {ItemCount} items, total {TotalAmount} {Currency}.",
            order.OrderNumber, order.ItemCount, order.TotalAmount, order.Currency);

        return CreatedAtRoute(GetByNumberRouteName, new { orderNumber = order.OrderNumber }, order);
    }

    /// <summary>
    /// Returns an order by its number. Both "KHZ-XXXXX" and "#KHZ-XXXXX" are accepted (case-insensitive);
    /// the '#' must be URL-encoded as %23.
    /// </summary>
    [HttpGet("{orderNumber}", Name = GetByNumberRouteName)]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByNumberAsync(string orderNumber, CancellationToken cancellationToken)
    {
        var order = await orderAppService.GetByNumberAsync(orderNumber, cancellationToken);
        return order is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Order '{orderNumber}' was not found.")
            : Ok(order);
    }
}
