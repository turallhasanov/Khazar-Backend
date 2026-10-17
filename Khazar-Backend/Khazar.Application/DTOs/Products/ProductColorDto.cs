namespace Khazar.Application.DTOs.Products;

public sealed record ProductColorDto
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Swatch color such as "#9a9a94"; null when the domain has no value for it.</summary>
    public string? Hex { get; init; }
}
