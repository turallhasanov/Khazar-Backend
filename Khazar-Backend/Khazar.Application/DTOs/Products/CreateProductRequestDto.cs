namespace Khazar.Application.DTOs.Products;

public sealed record CreateProductRequestDto : SaveProductRequestDto
{
    /// <summary>Optional initial variants; more can be added later.</summary>
    public IReadOnlyList<CreateVariantRequestDto> Variants { get; init; } = [];
}
