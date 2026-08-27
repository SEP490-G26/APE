using System.Text.Json.Serialization;

namespace Infrastructure.Judge0.Contracts;

internal sealed class Judge0SubmissionResultResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonPropertyName("status_id")]
    public int StatusId { get; init; }

    [JsonPropertyName("status")]
    public Judge0StatusResponse? Status { get; init; }

    [JsonPropertyName("stdout")]
    public string? StandardOutput { get; init; }

    [JsonPropertyName("stderr")]
    public string? StandardError { get; init; }

    [JsonPropertyName("compile_output")]
    public string? CompileOutput { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("time")]
    [JsonConverter(typeof(NullableDoubleJsonConverter))]
    public double? TimeSeconds { get; init; }

    [JsonPropertyName("memory")]
    [JsonConverter(typeof(NullableIntJsonConverter))]
    public int? MemoryKb { get; init; }
}