using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Judge0.Contracts;

internal sealed class NullableIntJsonConverter
    : JsonConverter<int?>
{
    public override int? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetInt32();

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();

            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var result))
            {
                return result;
            }
        }

        throw new JsonException(
            "Judge0 memory value is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        int? value,
        JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}