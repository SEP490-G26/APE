using System.Text;
using System.Text.Json;

namespace Infrastructure.AI;

internal static class AIClientJsonHelpers
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static StringContent CreateJsonContent(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");
    }

    public static int? GetInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : null;
    }

    public static int? GetInt32ByPath(JsonElement element, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (TryResolvePath(element, path, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out var value))
            {
                return value;
            }
        }

        return null;
    }

    public static decimal? GetDecimalByPath(JsonElement element, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (TryResolvePath(element, path, out var property))
            {
                if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
                {
                    return value;
                }

                if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    public static string? FindFirstText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("text", out var textProperty) && textProperty.ValueKind == JsonValueKind.String)
            {
                return textProperty.GetString();
            }

            foreach (var property in element.EnumerateObject())
            {
                var value = FindFirstText(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var value = FindFirstText(item);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    public static string ToDataUrl(string mimeType, string base64Data)
    {
        var safeMimeType = string.IsNullOrWhiteSpace(mimeType) ? "image/png" : mimeType.Trim();
        return $"data:{safeMimeType};base64,{base64Data}";
    }

    private static bool TryResolvePath(JsonElement element, string path, out JsonElement resolved)
    {
        resolved = element;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (resolved.ValueKind != JsonValueKind.Object || !resolved.TryGetProperty(segment, out var next))
            {
                resolved = default;
                return false;
            }

            resolved = next;
        }

        return true;
    }
}
