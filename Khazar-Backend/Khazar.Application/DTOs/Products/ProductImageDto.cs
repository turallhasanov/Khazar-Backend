namespace Khazar.Application.DTOs.Products;

/// <summary>A gallery photo. The first photo is the cover, the second the catalog card's hover photo.</summary>
public sealed record ProductImageDto
{
    /// <summary>Site path of an uploaded file, e.g. "/images/abc.jpg" (POST /api/admin/images).</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>The variant color the photo shows ("Antrasit"); null when it shows no particular color.</summary>
    public string? Color { get; init; }
}
