using Khazar.Application.Common.Interfaces;
using Khazar.WebAPI.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>NexusERP actions for platform admins.</summary>
[Authorize(Policy = AdminAuthorizationExtensions.PlatformAdminPolicy)]
[Route("api/admin/nexus")]
public sealed class AdminNexusController(INexusProductSyncService productSyncService) : BaseApiController
{
    /// <summary>
    /// Pulls NexusERP products, prices and stock into the catalog: known SKUs get the ERP stock and price, unknown
    /// ones are added as active products with a placeholder photo. Nothing is archived.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    /// <response code="200">Counts of created, updated, unchanged and skipped products, with per-product errors.</response>
    /// <response code="502">NexusERP could not be read ("nexus_unreachable").</response>
    [HttpPost("pull-products")]
    [ProducesResponseType<NexusProductSyncResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> PullProductsAsync(CancellationToken cancellationToken) =>
        Ok(await productSyncService.PullProductsAsync(cancellationToken));
}
