namespace Khazar.Application.DTOs.Products;

/// <summary>Product page payload: the catalog card fields plus description and per-variant stock.</summary>
public sealed record ProductDetailDto : ProductListDto
{
    public ProductDetailDto() { }

    /// <summary>Copies the catalog card fields so detail mapping does not repeat them.</summary>
    public ProductDetailDto(ProductListDto listing) : base(listing) { }

    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Not present in the frontend mock. Exposes real variant ids and per-color stock,
    /// which the size-only <see cref="ProductListDto.Stock"/> matrix cannot express.
    /// </summary>
    public IReadOnlyList<ProductVariantDto> Variants { get; init; } = [];
}
