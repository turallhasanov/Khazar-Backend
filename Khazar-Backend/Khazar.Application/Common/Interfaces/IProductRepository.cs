using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Interfaces;

/// <summary>Product persistence contract. GetAllAsync and GetByIdAsync are inherited from <see cref="IAsyncRepository{T}"/>.</summary>
public interface IProductRepository : IAsyncRepository<Product>
{
    /// <summary>Finds a product by its URL slug. Slugs are stored lower-case, so lookups are case-insensitive.</summary>
    Task<Product?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
}
