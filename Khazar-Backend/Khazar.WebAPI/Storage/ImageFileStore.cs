using Khazar.Application.Common.Exceptions;

namespace Khazar.WebAPI.Storage;

/// <summary>
/// Saves uploaded photos under wwwroot, which the API serves as static files. The type is read from the file's own
/// content (JPEG, PNG or WebP), never from the client's file name or declared content type, and the stored name is
/// generated, so an upload cannot choose where it lands or what it is called.
/// Errors: "invalid_image" (missing, or not a JPEG, PNG or WebP), "image_too_large" (over 8 MB).
/// </summary>
public sealed class ImageFileStore(IWebHostEnvironment environment)
{
    public const long MaxFileBytes = 8 * 1024 * 1024;
    private const int HeaderBytes = 12;

    /// <summary>Storage folder of the admin panel's photos, served at /images/{file}.</summary>
    public const string ImagesFolder = "images";

    /// <summary>Storage folder of the photos NexusERP sends with its products, served at /uploads/products/{file}.</summary>
    public const string ProductUploadsFolder = "uploads/products";

    /// <summary>Stores the image in <paramref name="folder"/> (relative to wwwroot) and returns its site path.</summary>
    public async Task<string> SaveAsync(IFormFile? file, string folder, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("Choose an image file.", "invalid_image");
        if (file.Length > MaxFileBytes)
            throw new ValidationException("The image can be at most 8 MB.", "image_too_large");

        await using var input = file.OpenReadStream();
        var header = new byte[HeaderBytes];
        var read = await input.ReadAtLeastAsync(header, HeaderBytes, throwOnEndOfStream: false, cancellationToken);
        var extension = DetectExtension(header.AsSpan(0, read))
            ?? throw new ValidationException("Only JPEG, PNG and WebP images are accepted.", "invalid_image");
        input.Position = 0;

        var directory = Path.Combine(environment.WebRootPath, folder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{extension}";

        await using (var output = System.IO.File.Create(Path.Combine(directory, fileName)))
            await input.CopyToAsync(output, cancellationToken);

        return $"/{folder}/{fileName}";
    }

    /// <summary>Removes a file this store saved (e.g. when the request that uploaded it then failed). Never throws.</summary>
    public void TryDelete(string sitePath)
    {
        try
        {
            var relative = sitePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(environment.WebRootPath, relative));
            // Only inside wwwroot, whatever the caller passed.
            if (full.StartsWith(Path.GetFullPath(environment.WebRootPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                System.IO.File.Delete(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover file is harmless.
        }
    }

    private static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith<byte>([0xFF, 0xD8, 0xFF]))
            return ".jpg";
        if (header.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return ".png";
        if (header.Length >= HeaderBytes && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
            return ".webp";
        return null;
    }
}
