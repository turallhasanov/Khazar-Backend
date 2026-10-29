using Khazar.Domain.Common;

namespace Khazar.Domain.Entities;

/// <summary>
/// A catalog category. <see cref="Name"/> is the key the API and storefront use ("Coat"), <see cref="Label"/> the
/// Azerbaijani name shown to shoppers ("Palto"), <see cref="Slug"/> the ?kateqoriya= value in catalog links ("palto").
/// </summary>
public class Category : BaseEntity
{
    public const int MaxNameLength = 100;
    public const int MaxLabelLength = 100;
    public const int MaxSlugLength = 100;

    public string Name { get; private set; } = default!;
    public string Label { get; private set; } = default!;
    public string Slug { get; private set; } = default!;

    /// <summary>Position of the category's tab in the storefront catalog; lower comes first.</summary>
    public int SortOrder { get; private set; }

    // Required by ORMs
    private Category() { }

    public Category(string name, string label, string slug, int sortOrder)
    {
        SetDetails(name, label, slug, sortOrder);
    }

    public void UpdateDetails(string name, string label, string slug, int sortOrder)
    {
        SetDetails(name, label, slug, sortOrder);
        MarkUpdated();
    }

    private void SetDetails(string name, string label, string slug, int sortOrder)
    {
        Name = Guard.MaxLength(Guard.NotEmpty(name, nameof(Name)), MaxNameLength, nameof(Name));
        Label = Guard.MaxLength(Guard.NotEmpty(label, nameof(Label)), MaxLabelLength, nameof(Label));
        Slug = Guard.MaxLength(Guard.NotEmpty(slug, nameof(Slug)), MaxSlugLength, nameof(Slug)).ToLowerInvariant();
        SortOrder = Guard.NotNegative(sortOrder, nameof(SortOrder));
    }
}
