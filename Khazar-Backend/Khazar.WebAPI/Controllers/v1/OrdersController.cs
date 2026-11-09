using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.DTOs.Orders;
using Khazar.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>Checkout and order lookup endpoints.</summary>
public sealed class OrdersController(
    IOrderAppService orderAppService, ICurrentUserService currentUser, ILogger<OrdersController> logger)
    : BaseApiController
{
    // MVC strips the "Async" suffix from action names, so CreatedAtAction(nameof(GetOrderByNumberAsync)) would not
    // resolve; a named route avoids that.
    private const string GetOrderByNumberRouteName = "GetOrderByNumber";

    /// <summary>
    /// Places an order. The server generates the KHZ-XXXXX number and recalculates all prices from the catalog;
    /// client-sent totals are ignored.
    /// </summary>
    /// <param name="dto">Customer, shipping, payment and line items of the checkout.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="201">The order was placed; the Location header points to GET /api/orders/{orderNumber}.</response>
    /// <response code="400">The request is invalid, e.g. it has no items.</response>
    /// <response code="404">A product or variant in the request does not exist.</response>
    /// <response code="409">A business rule is violated, e.g. insufficient stock.</response>
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

        return CreatedAtRoute(GetOrderByNumberRouteName, new { orderNumber = order.OrderNumber }, order);
    }

    /// <summary>The signed-in customer's own orders, newest first, with status, total, address and lines (name, size, color, quantity, price, photo).</summary>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The orders; empty when the customer has none.</response>
    /// <response code="401">No valid access token was sent.</response>
    [Authorize]
    [HttpGet("my-orders")]
    [ProducesResponseType<IReadOnlyList<MyOrderDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyOrdersAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Sign in to see your orders.");
        return Ok(await orderAppService.GetMyOrdersAsync(userId, cancellationToken));
    }

    /// <summary>One of the signed-in customer's own orders. Another customer's order answers 404.</summary>
    /// <param name="orderNumber">Order number, e.g. KHZ-12345.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The order.</response>
    /// <response code="401">No valid access token was sent.</response>
    /// <response code="404">The customer has no order with this number.</response>
    [Authorize]
    [HttpGet("my-orders/{orderNumber}")]
    [ProducesResponseType<MyOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyOrderAsync([FromRoute] string orderNumber, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Sign in to see your orders.");
        var order = await orderAppService.GetMyOrderAsync(userId, orderNumber, cancellationToken);
        return order is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Order '{orderNumber}' was not found.")
            : Ok(order);
    }

    /// <summary>Returns an order by its number.</summary>
    /// <param name="orderNumber">
    /// Order number, e.g. KHZ-12345. "#KHZ-12345" is accepted too (case-insensitive); the '#' must be URL-encoded as %23.
    /// </param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The order.</response>
    /// <response code="404">No order has this number.</response>
    [HttpGet("{orderNumber}", Name = GetOrderByNumberRouteName)]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderByNumberAsync([FromRoute] string orderNumber, CancellationToken cancellationToken)
    {
        var order = await orderAppService.GetByNumberAsync(orderNumber, cancellationToken);
        return order is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Order '{orderNumber}' was not found.")
            : Ok(order);
    }
}
