namespace Application.DTOs;

public sealed class PECodeRunServiceResult
{
    public int StatusCode { get; init; }

    public PECodeRunResultDto? Payload { get; init; }

    public string? Error { get; init; }

    public bool Success => StatusCode == 200 && Payload is not null;

    public static PECodeRunServiceResult Ok(
        PECodeRunResultDto payload)
    {
        return new PECodeRunServiceResult
        {
            StatusCode = 200,
            Payload = payload
        };
    }

    public static PECodeRunServiceResult Fail(
        int statusCode,
        string error)
    {
        return new PECodeRunServiceResult
        {
            StatusCode = statusCode,
            Error = error
        };
    }
}
