using System.Net.Http.Headers;
using System.Text.Json;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;

namespace Infrastructure.AI;

public class AIProviderModelCatalogService : IAIProviderModelCatalogService
{
    private readonly HttpClient _httpClient;
    private readonly IAIProviderSettingsResolver _providerSettingsResolver;

    public AIProviderModelCatalogService(HttpClient httpClient, IAIProviderSettingsResolver providerSettingsResolver)
    {
        _httpClient = httpClient;
        _providerSettingsResolver = providerSettingsResolver;
    }

    public async Task<List<AIProviderModelOptionDto>> ListModelsAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var normalizedProvider = providerName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProvider))
        {
            throw new ValidationException("Provider is required.");
        }

        return normalizedProvider.ToLowerInvariant() switch
        {
            "openai" => await ListOpenAiModelsAsync(cancellationToken),
            "deepseek" => await ListDeepSeekModelsAsync(cancellationToken),
            "gemini" => await ListGeminiModelsAsync(cancellationToken),
            "cohere" => await ListCohereModelsAsync(cancellationToken),
            _ => throw new ValidationException($"Provider '{normalizedProvider}' is not supported for model discovery.")
        };
    }

    private async Task<List<AIProviderModelOptionDto>> ListDeepSeekModelsAsync(CancellationToken cancellationToken)
    {
        var provider = GetConfiguredProvider("DeepSeek");
        using var message = new HttpRequestMessage(HttpMethod.Get, AIProviderHttpRequestHelper.BuildUri(provider, "models"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        AIProviderHttpRequestHelper.ApplyAdditionalHeaders(message, provider.Headers, "Authorization");

        using var timeoutCts = AIProviderHttpRequestHelper.CreateTimeoutCts(provider, cancellationToken);
        using var response = await _httpClient.SendAsync(message, timeoutCts?.Token ?? cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess("DeepSeek", response, raw);

        using var document = JsonDocument.Parse(raw);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return new List<AIProviderModelOptionDto>();
        }

        return data.EnumerateArray()
            .Select(item => item.TryGetProperty("id", out var idElement) ? idElement.GetString() : null)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => new AIProviderModelOptionDto
            {
                Id = item!,
                Label = item!
            })
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<AIProviderModelOptionDto>> ListOpenAiModelsAsync(CancellationToken cancellationToken)
    {
        var provider = GetConfiguredProvider("OpenAI");
        using var message = new HttpRequestMessage(HttpMethod.Get, AIProviderHttpRequestHelper.BuildUri(provider, "models"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        AIProviderHttpRequestHelper.ApplyAdditionalHeaders(message, provider.Headers, "Authorization");

        using var timeoutCts = AIProviderHttpRequestHelper.CreateTimeoutCts(provider, cancellationToken);
        using var response = await _httpClient.SendAsync(message, timeoutCts?.Token ?? cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess("OpenAI", response, raw);

        using var document = JsonDocument.Parse(raw);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return new List<AIProviderModelOptionDto>();
        }

        return data.EnumerateArray()
            .Select(item => item.TryGetProperty("id", out var idElement) ? idElement.GetString() : null)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => new AIProviderModelOptionDto
            {
                Id = item!,
                Label = item!
            })
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<AIProviderModelOptionDto>> ListGeminiModelsAsync(CancellationToken cancellationToken)
    {
        var provider = GetConfiguredProvider("Gemini");
        using var message = new HttpRequestMessage(HttpMethod.Get, AIProviderHttpRequestHelper.BuildUri(provider, "models"));
        message.Headers.Add("x-goog-api-key", provider.ApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        AIProviderHttpRequestHelper.ApplyAdditionalHeaders(message, provider.Headers, "x-goog-api-key");

        using var timeoutCts = AIProviderHttpRequestHelper.CreateTimeoutCts(provider, cancellationToken);
        using var response = await _httpClient.SendAsync(message, timeoutCts?.Token ?? cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess("Gemini", response, raw);

        using var document = JsonDocument.Parse(raw);
        if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
        {
            return new List<AIProviderModelOptionDto>();
        }

        return models.EnumerateArray()
            .Select(item =>
            {
                var rawName = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(rawName))
                {
                    return null;
                }

                var normalizedName = rawName!.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                    ? rawName["models/".Length..]
                    : rawName;

                return new AIProviderModelOptionDto
                {
                    Id = normalizedName,
                    Label = normalizedName
                };
            })
            .Where(item => item is not null)
            .OrderBy(item => item!.Label, StringComparer.OrdinalIgnoreCase)
            .Cast<AIProviderModelOptionDto>()
            .ToList();
    }

    private async Task<List<AIProviderModelOptionDto>> ListCohereModelsAsync(CancellationToken cancellationToken)
    {
        var provider = GetConfiguredProvider("Cohere");
        using var message = new HttpRequestMessage(HttpMethod.Get, AIProviderHttpRequestHelper.BuildUri(provider, "models"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        message.Headers.Add("X-Client-Name", "APE_BE");
        AIProviderHttpRequestHelper.ApplyAdditionalHeaders(message, provider.Headers, "Authorization", "X-Client-Name");

        using var timeoutCts = AIProviderHttpRequestHelper.CreateTimeoutCts(provider, cancellationToken);
        using var response = await _httpClient.SendAsync(message, timeoutCts?.Token ?? cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess("Cohere", response, raw);

        using var document = JsonDocument.Parse(raw);
        if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
        {
            return new List<AIProviderModelOptionDto>();
        }

        return models.EnumerateArray()
            .Select(item => item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => new AIProviderModelOptionDto
            {
                Id = item!,
                Label = item!
            })
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Application.Options.AIProviderOptions GetConfiguredProvider(string providerName)
    {
        var provider = _providerSettingsResolver.GetProvider(providerName);
        if (!provider.Enabled)
        {
            throw new ValidationException($"Provider '{providerName}' is disabled.");
        }

        if (string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            throw new ValidationException($"Provider '{providerName}' does not have an API key configured.");
        }

        return provider;
    }

    private static void EnsureSuccess(string providerName, HttpResponseMessage response, string raw)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new AIProviderException(
            providerName,
            "provider_model_catalog_failed",
            $"{providerName} model listing failed with status {(int)response.StatusCode}.",
            (int)response.StatusCode,
            raw);
    }
}
