using Khazar.Application.Common.Interfaces;
using Khazar.Application.DTOs.Categories;
using Khazar.Domain.Entities;

namespace Khazar.Application.Common.Mappings;

/// <summary>Explicit Category to DTO mappings.</summary>
public static class CategoryMappings
{
    public static CategoryDto ToDto(this Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Label = category.Label,
        Slug = category.Slug,
        SortOrder = category.SortOrder,
    };

    public static AdminCategoryDto ToAdminDto(this ManagedCategory managed)
    {
        var category = managed.Category;
        return new AdminCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Label = category.Label,
            Slug = category.Slug,
            SortOrder = category.SortOrder,
            ProductCount = managed.ProductCount,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt,
        };
    }
}
