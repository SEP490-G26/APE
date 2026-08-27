using System.Text.Json.Serialization;

namespace Infrastructure.Judge0.Contracts;

internal sealed class Judge0CreateBatchRequest
{
    [JsonPropertyName("submissions")]
    public required IReadOnlyList<Judge0SubmissionRequest> Submissions
    {
        get;
        init;
    }
}