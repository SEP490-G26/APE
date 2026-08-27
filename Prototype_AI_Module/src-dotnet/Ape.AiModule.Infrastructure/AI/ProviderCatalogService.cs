using System.Net.Http.Headers;
using System.Text.Json;
using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Application.Options.AI;
using Microsoft.Extensions.Options;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class ProviderCatalogService : IProviderCatalogService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<AiProviderOptions> _optionsMonitor;

    public ProviderCatalogService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<AiProviderOptions> optionsMonitor)
    {
        _httpClientFactory = httpClientFactory;
        _optionsMonitor = optionsMonitor;
    }

    public Task<IReadOnlyList<ProviderConnectionSummary>> GetProvidersAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        IReadOnlyList<ProviderConnectionSummary> providers =
        [
            ToSummary("openai", options.OpenAI),
            ToSummary("gemini", options.Gemini),
            ToSummary("cohere", options.Cohere)
        ];

        return Task.FromResult(providers);
    }

    public async Task<ProviderCatalogResult> GetProviderCatalogAsync(string provider, CancellationToken cancellationToken)
    {
        var normalizedProvider = provider.Trim().ToLowerInvariant();
        return normalizedProvider switch
        {
            "openai" => await GetOpenAiCatalogAsync(cancellationToken),
            "gemini" => await GetGeminiCatalogAsync(cancellationToken),
            "cohere" => await GetCohereCatalogAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Provider '{provider}' is not supported.")
        };
    }

    public async Task<ProviderPingResult> PingProviderAsync(string provider, CancellationToken cancellationToken)
    {
        try
        {
            var catalog = await GetProviderCatalogAsync(provider, cancellationToken);
            return new ProviderPingResult(provider.ToLowerInvariant(), true, 200, $"Connected. Retrieved {catalog.Models.Count} models.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            return new ProviderPingResult(provider.ToLowerInvariant(), false, (int)ex.StatusCode.Value, ex.Message);
        }
        catch (Exception ex)
        {
            return new ProviderPingResult(provider.ToLowerInvariant(), false, null, ex.Message);
        }
    }

    private async Task<ProviderCatalogResult> GetOpenAiCatalogAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue.OpenAI;
        EnsureConfigured("openai", options);

        var client = _httpClientFactory.CreateClient("ai-provider-openai");
        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var models = ParseOpenAiModels(payload);
        return new ProviderCatalogResult(ToSummary("openai", options), models);
    }

    private async Task<ProviderCatalogResult> GetGeminiCatalogAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue.Gemini;
        EnsureConfigured("gemini", options);

        var client = _httpClientFactory.CreateClient("ai-provider-gemini");
        var versionPrefix = string.IsNullOrWhiteSpace(options.ApiVersion) ? "v1beta" : options.ApiVersion.Trim('/');
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{versionPrefix}/models?key={Uri.EscapeDataString(options.ApiKey)}");

        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var models = ParseGeminiModels(payload);
        return new ProviderCatalogResult(ToSummary("gemini", options), models);
    }

    private async Task<ProviderCatalogResult> GetCohereCatalogAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue.Cohere;
        EnsureConfigured("cohere", options);

        var client = _httpClientFactory.CreateClient("ai-provider-cohere");
        using var request = new HttpRequestMessage(HttpMethod.Get, "v1/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var models = ParseCohereModels(payload);
        return new ProviderCatalogResult(ToSummary("cohere", options), models);
    }

    private static ProviderConnectionSummary ToSummary(string provider, ProviderOptionsBase options)
    {
        return new ProviderConnectionSummary(
            Provider: provider,
            Enabled: options.Enabled,
            HasApiKey: !string.IsNullOrWhiteSpace(options.ApiKey),
            BaseUrl: options.BaseUrl,
            ApiVersion: options.ApiVersion,
            DefaultTextModel: options.DefaultTextModel,
            DefaultVisionModel: options.DefaultVisionModel,
            DefaultEmbeddingModel: options.DefaultEmbeddingModel);
    }

    private static void EnsureConfigured(string provider, ProviderOptionsBase options)
    {
        if (!options.Enabled)
        {
            throw new InvalidOperationException($"Provider '{provider}' is disabled.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException($"Provider '{provider}' does not have an API key configured.");
        }
    }

    private static IReadOnlyList<ProviderModelInfo> ParseOpenAiModels(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return data.EnumerateArray()
            .Select(model =>
            {
                var id = model.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
                return new ProviderModelInfo(id, id, []);
            })
            .Where(static model => !string.IsNullOrWhiteSpace(model.Id))
            .OrderBy(static model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<ProviderModelInfo> ParseGeminiModels(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("models", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return data.EnumerateArray()
            .Select(model =>
            {
                var rawName = model.TryGetProperty("name", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
                var baseModelId = model.TryGetProperty("baseModelId", out var baseIdElement) ? baseIdElement.GetString() ?? rawName : rawName;
                var capabilities = model.TryGetProperty("supportedActions", out var actions) && actions.ValueKind == JsonValueKind.Array
                    ? actions.EnumerateArray().Select(static action => action.GetString() ?? string.Empty).Where(static action => !string.IsNullOrWhiteSpace(action)).ToList()
                    : [];
                var id = NormalizeGeminiModelId(baseModelId);
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = NormalizeGeminiModelId(rawName);
                }

                return new ProviderModelInfo(id, baseModelId, capabilities);
            })
            .Where(static model => !string.IsNullOrWhiteSpace(model.Id))
            .OrderBy(static model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeGeminiModelId(string? model)
    {
        var value = model?.Trim() ?? string.Empty;
        if (value.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
        {
            value = value["models/".Length..];
        }

        return value;
    }

    private static IReadOnlyList<ProviderModelInfo> ParseCohereModels(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("models", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return data.EnumerateArray()
            .Select(model =>
            {
                var name = model.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
                var endpoints = model.TryGetProperty("endpoints", out var endpointsElement) && endpointsElement.ValueKind == JsonValueKind.Array
                    ? endpointsElement.EnumerateArray().Select(static endpoint => endpoint.GetString() ?? string.Empty).Where(static endpoint => !string.IsNullOrWhiteSpace(endpoint)).ToList()
                    : [];

                return new ProviderModelInfo(name, name, endpoints);
            })
            .Where(static model => !string.IsNullOrWhiteSpace(model.Id))
            .OrderBy(static model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
