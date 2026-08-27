namespace Application.Exceptions;

public class AIProviderException : InvalidOperationException
{
    public AIProviderException(
        string provider,
        string errorCode,
        string message,
        int? statusCode = null,
        string? responseBody = null,
        Exception? innerException = null,
        object? debugDetails = null) : base(message, innerException)
    {
        Provider = provider;
        ErrorCode = errorCode;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        DebugDetails = debugDetails;
    }

    public string Provider { get; }
    public string ErrorCode { get; }
    public int? StatusCode { get; }
    public string? ResponseBody { get; }
    public object? DebugDetails { get; }
}
