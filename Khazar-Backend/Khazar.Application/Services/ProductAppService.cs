using Khazar.Application.Common.Interfaces;
using Khazar.Application.Common.Mappings;
using Khazar.Application.DTOs.Products;

namespace Khazar.Application.Services;

public sealed class ProductAppService(IProductRepository productRepository) : IProductAppService
{
    public async Task<IReadOnlyList<ProductListDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await productRepository.GetAllAsync(cancellationToken);
        return products.ToListDtos();
    }

    public async Task<ProductDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // GetByIdAsync also returns archived products; the storefront must not show them.
        var product = await productRepository.GetByIdAsync(id, cancellationToken);
        return product is { IsActive: true } ? product.ToDetailDto() : null;
    }

    public async Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        // Slugs are stored lower-case (see Product.SetDetails).
        var product = await productRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken);
        return product?.ToDetailDto();
    }

    public async Task<IReadOnlyList<ProductListDto>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
            return [];

        var products = await productRepository.GetByCategoryAsync(category.Trim(), cancellationToken);
        return products.ToListDtos();
    }
}
