using System.Text.Json.Serialization;

namespace Infrastructure.Judge0.Contracts;

internal sealed class Judge0BatchResultResponse
{
    [JsonPropertyName("submissions")]
    public List<Judge0SubmissionResultResponse> Submissions
    {
        get;
        init;
    } = [];
}