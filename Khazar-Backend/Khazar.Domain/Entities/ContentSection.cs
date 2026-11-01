using Khazar.Domain.Common;

namespace Khazar.Domain.Entities;

/// <summary>
/// One editable block of storefront copy (the home page hero, the about page, the contact details, ...), stored as
/// JSON. <see cref="Key"/> names the block ("hero"); the application layer owns the schema of each key and validates
/// <see cref="Data"/> before it is saved.
/// </summary>
public class ContentSection : BaseEntity
{
    public const int MaxKeyLength = 50;

    public string Key { get; private set; } = default!;
    public string Data { get; private set; } = default!;

    // Required by ORMs
    private ContentSection() { }

    public ContentSection(string key, string data)
    {
        Key = Guard.MaxLength(Guard.NotEmpty(key, nameof(Key)), MaxKeyLength, nameof(Key));
        Data = Guard.NotEmpty(data, nameof(Data));
    }

    public void Replace(string data)
    {
        Data = Guard.NotEmpty(data, nameof(Data));
        MarkUpdated();
    }
}
