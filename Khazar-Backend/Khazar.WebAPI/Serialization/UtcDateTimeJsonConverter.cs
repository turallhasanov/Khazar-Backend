using System.Text.Json;
using System.Text.Json.Serialization;

namespace Khazar.WebAPI.Serialization;

/// <summary>
/// Writes every DateTime as UTC with the "Z" suffix. The domain stores UTC, but values read back from SQL Server have
/// DateTimeKind.Unspecified and would serialize without an offset, which browsers parse as local time: a review
/// written a minute ago showed as "3 saat əvvəl" at UTC+3.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
