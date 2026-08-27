using System.Text.Json.Serialization;

namespace Infrastructure.Judge0.Contracts;

internal sealed class Judge0StatusResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}