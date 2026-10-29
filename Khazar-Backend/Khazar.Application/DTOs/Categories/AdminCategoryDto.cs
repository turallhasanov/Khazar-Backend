namespace Khazar.Application.DTOs.Categories;

/// <summary>A category as the admin panel manages it.</summary>
public sealed record AdminCategoryDto : CategoryDto
{
    /// <summary>Products in this category, archived ones included. A category with products cannot be deleted.</summary>
    public int ProductCount { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
