using Khazar.Application.DTOs.Orders;
using Khazar.Application.Services;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Order management for platform admins. Orders are not deleted: they are completed or cancelled (cancelling returns
/// the items to stock). A transition the order's status does not allow answers 409 "invalid_order_status".
/// </summary>
[Authorize(Policy = AdminAuthorizationExtensions.PlatformAdminPolicy)]
[Route("api/admin/orders")]
public sealed class AdminOrdersController(IAdminOrderAppService orderAppService) : BaseApiController
{
    /// <summary>Returns the active orders, newest first. Cancelled (deleted) orders are listed only with status=Cancelled.</summary>
    /// <param name="status">Optional filter: Pending, Processing, SyncedToErp, Completed or Cancelled.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="200">The orders.</response>
    /// <response code="400">Unknown status ("invalid_status").</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrderResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrdersAsync([FromQuery] string? status, CancellationToken cancellationToken) =>
        Ok(await orderAppService.GetAllAsync(status, cancellationToken));

    /// <summary>Returns one order with its items.</summary>
    /// <param name="id">Order identifier.</param>
    /// <param name="cancellationToken">Aborts the query when the client disconnects.</param>
    /// <response code="404">No order has this id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await orderAppService.GetByIdAsync(id, cancellationToken));

    /// <summary>Marks an order NexusERP accepted (SyncedToErp) as completed.</summary>
    /// <param name="id">Order identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The completed order.</response>
    /// <response code="404">No order has this id.</response>
    /// <response code="409">The order is not SyncedToErp ("invalid_order_status").</response>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteOrderAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await orderAppService.CompleteAsync(id, cancellationToken));

    /// <summary>Cancels an order and returns its items to stock; an order NexusERP accepted is cancelled there too.</summary>
    /// <param name="id">Order identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The cancelled order.</response>
    /// <response code="404">No order has this id.</response>
    /// <response code="409">The order is already completed or cancelled ("invalid_order_status").</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelOrderAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await orderAppService.CancelAsync(id, cancellationToken));

    /// <summary>
    /// Deletes an order the way orders are removed here: it is cancelled and kept as a record (status "Cancelled").
    /// Its items return to Khazar stock, and an order NexusERP accepted is cancelled there too (its stock is returned
    /// there). If NexusERP cannot cancel it, the order is left unchanged.
    /// </summary>
    /// <param name="id">Order identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">The cancelled order.</response>
    /// <response code="404">No order has this id.</response>
    /// <response code="409">The order is already completed or cancelled ("invalid_order_status").</response>
    /// <response code="502">NexusERP did not cancel the order ("nexus_cancel_failed"); nothing changed.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> DeleteOrderAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await orderAppService.CancelAsync(id, cancellationToken));

    /// <summary>Queues a Pending order for another NexusERP push; the result shows on the next load.</summary>
    /// <param name="id">Order identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="202">The order was queued.</response>
    /// <response code="404">No order has this id.</response>
    /// <response code="409">The order is not Pending ("invalid_order_status").</response>
    [HttpPost("{id:guid}/erp-sync")]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RetryErpSyncAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Accepted(await orderAppService.RetryErpSyncAsync(id, cancellationToken));

    /// <summary>Pushes a Pending order to NexusERP immediately and returns it with its new status.</summary>
    /// <param name="id">Order identifier.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">NexusERP accepted the order; it is now SyncedToErp.</response>
    /// <response code="404">No order has this id.</response>
    /// <response code="409">The order is not Pending ("invalid_order_status").</response>
    /// <response code="502">NexusERP did not accept the order ("nexus_sync_failed"); it stays Pending.</response>
    [HttpPost("{id:guid}/sync-nexus")]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SyncToNexusAsync([FromRoute] Guid id, CancellationToken cancellationToken) =>
        Ok(await orderAppService.SyncToNexusAsync(id, cancellationToken));
}
