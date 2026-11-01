using System.Text.Json;
using System.Text.Json.Serialization;
using Khazar.Application.Common.Exceptions;

namespace Khazar.Application.Content;

/// <summary>
/// The editable content sections by key, and their JSON form. The key is the property name under "sections" in
/// GET /api/content and the {key} of PUT /api/admin/content/sections/{key}.
/// </summary>
public static class ContentSchema
{
    public const string InvalidContentErrorCode = "invalid_content";

    public static IReadOnlyDictionary<string, Type> Sections { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["site"] = typeof(SiteSettingsSection),
        ["hero"] = typeof(HeroSection),
        ["newArrivals"] = typeof(NewArrivalsSection),
        ["manifesto"] = typeof(ManifestoSection),
        ["lookbook"] = typeof(LookbookSection),
        ["slogan"] = typeof(SloganSection),
        ["featureBlocks"] = typeof(FeatureBlocksSection),
        ["craftsmanship"] = typeof(CraftsmanshipSection),
        ["journal"] = typeof(JournalSection),
        ["catalog"] = typeof(CatalogSection),
        ["about"] = typeof(AboutSection),
        ["faqPage"] = typeof(FaqPageSection),
        ["shipping"] = typeof(ShippingSection),
        ["contact"] = typeof(ContactSection),
        ["privacy"] = typeof(PrivacySection),
        ["sizeGuide"] = typeof(SizeGuideSection),
        ["search"] = typeof(SearchSection),
    };

    // Saving is strict: a misspelled field is an error rather than silently dropped.
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    // Reading stored data is lenient, so a field removed from a section type does not break the stored JSON.
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    public static bool IsKnown(string key) => Sections.ContainsKey(key);

    /// <summary>A section with every field empty, for keys that were never saved.</summary>
    public static object CreateEmpty(string key) => Activator.CreateInstance(Sections[key])!;

    /// <summary>Parses and validates a section sent by the admin panel; throws a ValidationException ("invalid_content").</summary>
    public static object Parse(string key, JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
            throw new ValidationException("The section must be a JSON object.", InvalidContentErrorCode);

        object? section;
        try
        {
            section = data.Deserialize(Sections[key], WriteOptions);
        }
        catch (JsonException ex)
        {
            var path = ex.Path is { Length: > 1 } jsonPath ? jsonPath[2..] : key;
            throw new ValidationException($"Field '{path}' is unknown or has the wrong type.", InvalidContentErrorCode);
        }

        return ContentValidator.Normalize(section ?? CreateEmpty(key));
    }

    /// <summary>Reads stored section data; null when it cannot be read, so one broken row never breaks the site.</summary>
    public static object? Read(string key, string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, Sections[key], ReadOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize(object value) => JsonSerializer.Serialize(value, value.GetType(), ReadOptions);

    /// <summary>Serializes a typed value (an article body, lookbook hotspots) for an entity's JSON column.</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, ReadOptions);

    public static T ReadOrDefault<T>(string json, T fallback)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, ReadOptions) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
