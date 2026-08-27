namespace Application.Common;

public class ApiResponse
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public string? TraceId { get; init; }

    public static ApiResponse Ok()
    {
        return new ApiResponse
        {
            Success = true
        };
    }

    public static ApiResponse<T> Ok<T>(T data)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data
        };
    }

    public static ApiResponse Fail(
        string error,
        string? traceId = null)
    {
        return new ApiResponse
        {
            Success = false,
            Error = error,
            TraceId = traceId
        };
    }

    public static ApiResponse<T> Fail<T>(
        string error,
        string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Error = error,
            TraceId = traceId
        };
    }
}

public sealed class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }
}