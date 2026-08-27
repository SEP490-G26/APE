using System.Net.Http.Headers;
using Application.Options;

namespace Infrastructure.AI;

internal static class AIProviderHttpRequestHelper
{
    public static Uri BuildUri(AIProviderOptions provider, string relativePath)
    {
        var baseUrl = provider.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("AI provider base URL is not configured.");
        }

        var normalizedBaseUrl = baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : $"{baseUrl}/";

        var baseUri = new Uri(normalizedBaseUrl);
        var apiVersion = provider.ApiVersion?.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(apiVersion))
        {
            return new Uri(baseUri, relativePath);
        }

        var absolutePath = baseUri.AbsolutePath.Trim('/');
        var versionAlreadyPresent =
            absolutePath.Equals(apiVersion, StringComparison.OrdinalIgnoreCase) ||
            absolutePath.EndsWith($"/{apiVersion}", StringComparison.OrdinalIgnoreCase) ||
            absolutePath.Contains($"/{apiVersion}/", StringComparison.OrdinalIgnoreCase);

        if (versionAlreadyPresent)
        {
            return new Uri(baseUri, relativePath);
        }

        var versionedBase = new Uri(baseUri, $"{apiVersion}/");
        return new Uri(versionedBase, relativePath);
    }

    public static void ApplyAdditionalHeaders(
        HttpRequestMessage message,
        IReadOnlyDictionary<string, string> headers,
        params string[] excludedHeaderNames)
    {
        if (headers.Count == 0)
        {
            return;
        }

        var excluded = new HashSet<string>(excludedHeaderNames, StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            if (excluded.Contains(header.Key) || string.IsNullOrWhiteSpace(header.Value))
            {
                continue;
            }

            if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                message.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }

    public static CancellationTokenSource? CreateTimeoutCts(AIProviderOptions provider, CancellationToken cancellationToken)
    {
        if (provider.TimeoutSeconds <= 0)
        {
            return null;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(provider.TimeoutSeconds));
        return cts;
    }
}
