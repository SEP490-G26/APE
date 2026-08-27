using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Judge0.Contracts;

internal sealed class Judge0CreateSubmissionResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ValidationErrors
    {
        get;
        init;
    }
}