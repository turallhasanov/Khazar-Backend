using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Base class for storefront endpoints. Routes resolve to /api/{controller-name}, e.g. /api/products.
/// [ApiController] turns model binding failures into 400 ValidationProblemDetails responses automatically.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
}
