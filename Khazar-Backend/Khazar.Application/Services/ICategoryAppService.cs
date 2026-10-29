using Khazar.Application.DTOs.Categories;

namespace Khazar.Application.Services;

/// <summary>Read-side category use cases for the storefront.</summary>
public interface ICategoryAppService
{
    /// <summary>Every category in storefront order (SortOrder, then Name).</summary>
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
