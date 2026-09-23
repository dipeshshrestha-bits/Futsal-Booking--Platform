using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Common;

/// <summary>Reads "18:00" or "18:00:00" and always writes "HH:mm" (what the frontend uses).</summary>
public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Formats = { "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss", "HH:mm:ss.FFFFFFF" };

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Time must be a string in HH:mm format, for example \"18:00\".");
        }

        var text = reader.GetString()?.Trim();
        if (TimeOnly.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            return value;
        }

        throw new JsonException($"'{text}' is not a valid time. Use HH:mm, for example \"18:00\".");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm", CultureInfo.InvariantCulture));
}