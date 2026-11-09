using System.Text.RegularExpressions;
using Khazar.Domain.Entities;

namespace Khazar.Application.Common;

/// <summary>A product name split into the storefront name and the variant details NexusERP appended to it.</summary>
public sealed record ParsedProductName(string BaseName, string? Size, string? Color)
{
    public bool HasVariantDetails => Size is not null || Color is not null;
}

/// <summary>
/// NexusERP has no variants, so it names a variant "Name (Color / Size)", "Name (Size)" or "Name (Color)". The storefront
/// keeps the name clean and stores the details as the variant's size and color. A suffix that does not read like a size
/// or a color (for example "(2-3 yaş)" or "(limited)" with digits) stays part of the name.
/// </summary>
public static partial class ProductNameParser
{
    public static ParsedProductName Parse(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        var match = TrailingGroup().Match(trimmed);

        if (!match.Success)
            return new ParsedProductName(trimmed, null, null);

        var baseName = match.Groups["base"].Value.Trim();
        var parts = match.Groups["inner"].Value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (baseName.Length == 0 || parts.Length is 0 or > 2)
            return new ParsedProductName(trimmed, null, null);

        string? size = null, color = null;

        if (parts.Length == 2)
        {
            (color, size) = (parts[0], parts[1]);
        }
        else if (SizePattern().IsMatch(parts[0]))
        {
            size = parts[0].ToUpperInvariant();
        }
        else if (ColorPattern().IsMatch(parts[0]))
        {
            color = parts[0];
        }

        if ((size is null && color is null)
            || size?.Length > ProductVariant.MaxSizeLength
            || color?.Length > ProductVariant.MaxColorLength)
            return new ParsedProductName(trimmed, null, null);

        return new ParsedProductName(baseName, size?.ToUpperInvariant(), color);
    }

    [GeneratedRegex(@"^(?<base>.*?)\s*\((?<inner>[^()]*)\)$")]
    private static partial Regex TrailingGroup();

    [GeneratedRegex(@"^(?:[2-6]?XS|[2-6]?XL|XXL|XXXL|S|M|L|\d{1,3}(?:[.,]5)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex SizePattern();

    [GeneratedRegex(@"^\p{L}[\p{L} ]*$")]
    private static partial Regex ColorPattern();
}
