using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using ValidationException = Khazar.Application.Common.Exceptions.ValidationException;

namespace Khazar.Application.Content;

/// <summary>
/// Normalizes and validates content records (sections, article bodies, lookbook looks, FAQ items) in place:
/// strings are trimmed (null becomes empty), lists and nested records are never null, and every limit is checked.
/// Rules: a string is at most its [MaxLength] (default 500, list items 2000); a list has at most [MaxLength] items
/// (default 30); [Required] strings are not empty; numbers respect [Range]; a property whose name ends in "Url",
/// "Link" or "To" holds a site path ("/katalog") or an http(s) address, so a "javascript:" link can never be stored.
/// Errors are ValidationExceptions ("invalid_content") naming the field, e.g. "Field 'hero.ctaLink' ...".
/// </summary>
public static class ContentValidator
{
    private const int DefaultMaxStringLength = 500;
    private const int DefaultMaxListItemLength = 2000;
    private const int DefaultMaxItems = 30;
    private const int MaxDepth = 6;

    private static readonly string[] LinkSuffixes = ["Url", "Link", "To"];

    public static T Normalize<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        NormalizeObject(value, string.Empty, 0);
        return value;
    }

    /// <summary>True for "", a site path ("/katalog?x=1") or an absolute http(s) URL.</summary>
    public static bool IsSafeLink(string value) =>
        value.Length == 0 ||
        (value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal) && !value.StartsWith("/\\", StringComparison.Ordinal)) ||
        (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));

    private static void NormalizeObject(object target, string path, int depth)
    {
        if (depth > MaxDepth)
            throw Invalid(path, "is nested too deeply");

        foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0)
                continue;

            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            var fieldPath = path.Length == 0 ? name : $"{path}.{name}";
            var type = property.PropertyType;
            var value = property.GetValue(target);
            var maxLength = property.GetCustomAttribute<MaxLengthAttribute>()?.Length;

            if (type == typeof(string))
            {
                var text = NormalizeString((string?)value, fieldPath, maxLength ?? DefaultMaxStringLength);
                if (text.Length == 0 && property.GetCustomAttribute<RequiredAttribute>() is not null)
                    throw Invalid(fieldPath, "is required");
                if (IsLinkProperty(property.Name) && !IsSafeLink(text))
                    throw Invalid(fieldPath, "must be a site path starting with \"/\" or an http(s) address");
                property.SetValue(target, text);
            }
            else if (type.IsValueType)
            {
                if (property.GetCustomAttribute<RangeAttribute>() is { } range && !range.IsValid(value))
                    throw Invalid(fieldPath, $"must be between {range.Minimum} and {range.Maximum}");
            }
            else if (TryGetListElementType(type, out var elementType))
            {
                var list = (IList?)value ?? (IList)Activator.CreateInstance(type)!;
                var maxItems = maxLength ?? DefaultMaxItems;
                if (list.Count > maxItems)
                    throw Invalid(fieldPath, $"can have at most {maxItems} items");

                for (var i = 0; i < list.Count; i++)
                {
                    var itemPath = $"{fieldPath}[{i}]";
                    if (elementType == typeof(string))
                        list[i] = NormalizeString((string?)list[i], itemPath, DefaultMaxListItemLength);
                    else
                        NormalizeObject(list[i] ?? throw Invalid(itemPath, "cannot be empty"), itemPath, depth + 1);
                }

                property.SetValue(target, list);
            }
            else if (type.IsClass)
            {
                var nested = value ?? Activator.CreateInstance(type)!;
                NormalizeObject(nested, fieldPath, depth + 1);
                property.SetValue(target, nested);
            }
        }
    }

    private static string NormalizeString(string? value, string path, int maxLength)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length > maxLength)
            throw Invalid(path, $"can be at most {maxLength} characters");
        return text;
    }

    private static bool IsLinkProperty(string name) =>
        LinkSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal));

    private static bool TryGetListElementType(Type type, out Type elementType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }

    private static ValidationException Invalid(string path, string problem) =>
        new($"Field '{path}' {problem}.", ContentSchema.InvalidContentErrorCode);
}
