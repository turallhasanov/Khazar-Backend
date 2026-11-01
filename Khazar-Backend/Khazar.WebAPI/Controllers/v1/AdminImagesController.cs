using Khazar.WebAPI.Authentication;
using Khazar.WebAPI.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.Controllers.v1;

/// <summary>
/// Image upload for platform admins. Files go to wwwroot/images, which the API serves at /images/{file}; the returned
/// path is what products, looks, articles and sections store. Errors: "invalid_image" (not a JPEG, PNG or WebP),
/// "image_too_large" (over 8 MB).
/// </summary>
[Authorize(Policy = AdminAuthorizationExtensions.PlatformAdminPolicy)]
[Route("api/admin/images")]
public sealed class AdminImagesController(ImageFileStore imageStore) : BaseApiController
{
    /// <summary>Stores an image under a generated name and returns its site path.</summary>
    /// <param name="file">JPEG, PNG or WebP, at most 8 MB. The type is read from the file's content, not its name.</param>
    /// <param name="cancellationToken">Aborts the upload when the client disconnects.</param>
    /// <response code="201">{ "url": "/images/....jpg" }</response>
    /// <response code="400">Not a supported image ("invalid_image"), or too large ("image_too_large").</response>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageFileStore.MaxFileBytes + 64 * 1024)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImageAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        var url = await imageStore.SaveAsync(file, ImageFileStore.ImagesFolder, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { url });
    }
}
