namespace Khazar.Application.Common.Interfaces;

/// <summary>Pulls the NexusERP product catalog, prices and stock into the Khazar catalog.</summary>
public interface INexusProductSyncService
{
    /// <summary>
    /// Upserts every active NexusERP product by SKU (= NexusERP barcode): existing variants get the ERP stock and
    /// the product the ERP price; unknown products are added as active. Throws ExternalServiceException when
    /// NexusERP cannot be read; a single product that fails is reported in the result and does not stop the run.
    /// </summary>
    Task<NexusProductSyncResult> PullProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// A product NexusERP pushes when it is saved there. Matched by SKU (= NexusERP barcode): an unknown, published product
    /// is added as active with a single "Standart" variant; a known one gets the ERP stock and price, the new photo in front
    /// of its gallery, and is shown or hidden as <see cref="NexusProductPush.IsPublished"/> says. An unknown product that
    /// is not published is not added.
    /// </summary>
    Task<NexusProductPushResult> PushProductAsync(NexusProductPush push, CancellationToken cancellationToken = default);
}

/// <summary>A product as NexusERP sends it. <paramref name="ImageUrl"/> is a site path already stored by Khazar.</summary>
public sealed record NexusProductPush(
    string Sku, string Name, string? CategoryName, decimal SalePrice, int Stock, bool IsPublished, string? ImageUrl)
{
    /// <summary>
    /// Further photos NexusERP sent with the product, already stored by Khazar. With <see cref="ImageUrl"/> they follow it
    /// in the gallery; without it they are added after the photos the product already has.
    /// </summary>
    public IReadOnlyList<string> GalleryUrls { get; init; } = [];
}

/// <summary>
/// <paramref name="Outcome"/> is "created", "updated", "unchanged" or "skipped". <paramref name="ReplacedImageUrls"/> are
/// earlier NexusERP photos of the product that the new one superseded; nothing references them any more.
/// </summary>
public sealed record NexusProductPushResult(
    string Outcome, string? Slug, string? ImageUrl, IReadOnlyList<string> ReplacedImageUrls);

public sealed record NexusProductSyncResult(
    int Fetched, int Created, int Updated, int Unchanged, int Skipped, IReadOnlyList<string> Errors);
