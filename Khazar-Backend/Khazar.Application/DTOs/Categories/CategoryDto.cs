namespace Khazar.Application.DTOs.Categories;

/// <summary>A storefront category. Products carry <see cref="Name"/> in their "category" field.</summary>
public record CategoryDto
{
    public Guid Id { get; init; }

    /// <summary>Key the API and storefront use, e.g. "Coat".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Azerbaijani display name, e.g. "Palto".</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>The ?kateqoriya= value in catalog links, e.g. "palto".</summary>
    public string Slug { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}
