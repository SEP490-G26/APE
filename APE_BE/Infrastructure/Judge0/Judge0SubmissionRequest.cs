using System.Text.Json.Serialization;

namespace Infrastructure.Judge0.Contracts;

internal sealed class Judge0SubmissionRequest
{
    [JsonPropertyName("language_id")]
    public required int LanguageId { get; init; }

    [JsonPropertyName("source_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceCode { get; init; }

    [JsonPropertyName("additional_files")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AdditionalFiles { get; init; }

    [JsonPropertyName("stdin")]
    public required string StandardInput { get; init; }

    [JsonPropertyName("cpu_time_limit")]
    public required double CpuTimeLimitSeconds { get; init; }

    [JsonPropertyName("wall_time_limit")]
    public required double WallTimeLimitSeconds { get; init; }

    [JsonPropertyName("memory_limit")]
    public required int MemoryLimitKb { get; init; }
}
