using Khazar.Application.DTOs.Products;

namespace Khazar.Application.Services;

/// <summary>Read-side use cases for the storefront catalog.</summary>
public interface IProductAppService
{
    Task<IReadOnlyList<ProductListDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ProductDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
