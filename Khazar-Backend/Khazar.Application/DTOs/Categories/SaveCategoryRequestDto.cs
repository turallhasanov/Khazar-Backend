namespace Khazar.Application.DTOs.Categories;

/// <summary>Editable category fields, for both create and update.</summary>
public sealed record SaveCategoryRequestDto
{
    /// <summary>Key the API and storefront use, e.g. "Coat". Renaming it changes every product's "category" value.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Azerbaijani display name, e.g. "Palto".</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>URL value: lower-case letters, digits and single hyphens, e.g. "palto".</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Position in the storefront catalog tabs; lower comes first.</summary>
    public int SortOrder { get; init; }
}
