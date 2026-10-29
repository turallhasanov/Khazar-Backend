using Khazar.Application.DTOs.Categories;

namespace Khazar.Application.Services;

/// <summary>
/// Category management for platform admins. Failures carry an errorCode naming the field or rule: invalid_name,
/// invalid_label, invalid_slug, invalid_sort_order, category_name_taken, category_slug_taken, category_has_products.
/// </summary>
public interface IAdminCategoryAppService
{
    /// <summary>Every category in storefront order, with its product count.</summary>
    Task<IReadOnlyList<AdminCategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<AdminCategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdminCategoryDto> CreateAsync(SaveCategoryRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Replaces name, label, slug and sort order. Products follow the category, so a rename applies to all of them.</summary>
    Task<AdminCategoryDto> UpdateAsync(Guid id, SaveCategoryRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes a category without products ("category_has_products" otherwise).</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
