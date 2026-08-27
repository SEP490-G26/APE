using Application.Exceptions;

namespace Application.Common;

public static class AIErrorCatalog
{
    public static string FromProviderFailure(string provider, int? statusCode, string? responseBody)
    {
        var body = responseBody ?? string.Empty;

        if (statusCode == 400) return $"ai.{provider.ToLowerInvariant()}.bad_request";
        if (statusCode == 401 || statusCode == 403) return $"ai.{provider.ToLowerInvariant()}.auth";
        if (statusCode == 404) return $"ai.{provider.ToLowerInvariant()}.not_found";
        if (statusCode == 408) return $"ai.{provider.ToLowerInvariant()}.timeout";
        if (statusCode == 409) return $"ai.{provider.ToLowerInvariant()}.conflict";
        if (statusCode == 422) return $"ai.{provider.ToLowerInvariant()}.unprocessable";
        if (statusCode == 429 || body.Contains("rate limit", StringComparison.OrdinalIgnoreCase)) return $"ai.{provider.ToLowerInvariant()}.rate_limit";
        if (statusCode >= 500) return $"ai.{provider.ToLowerInvariant()}.server_error";
        return $"ai.{provider.ToLowerInvariant()}.request_failed";
    }

    public static string FromException(Exception exception, string provider = "unknown")
    {
        return exception switch
        {
            AIProviderException providerException => providerException.ErrorCode,
            HttpRequestException => $"ai.{provider.ToLowerInvariant()}.network",
            TaskCanceledException => $"ai.{provider.ToLowerInvariant()}.timeout",
            _ => $"ai.{provider.ToLowerInvariant()}.unexpected"
        };
    }
}
