using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Base class for version 1 endpoints. Routes resolve to /api/v1/{controller-name}, e.g. /api/v1/products.
/// [ApiController] turns model binding failures into 400 ValidationProblemDetails responses automatically.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
}
