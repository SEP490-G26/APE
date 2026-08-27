using System.Net.Http.Headers;
using System.Text.Json;
using Application.DTOs;
using Application.Common;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

public class CohereTextClient : IAITextClient
{
    private readonly HttpClient _httpClient;
    private readonly IAIProviderSettingsResolver _providerSettingsResolver;
    private readonly ILogger<CohereTextClient> _logger;

    public CohereTextClient(HttpClient httpClient, IAIProviderSettingsResolver providerSettingsResolver, ILogger<CohereTextClient> logger)
    {
        _httpClient = httpClient;
        _providerSettingsResolver = providerSettingsResolver;
        _logger = logger;
    }

    public string ProviderName => "Cohere";

    public bool IsConfigured
    {
        get
        {
            var provider = _providerSettingsResolver.GetProvider(ProviderName);
            return provider.Enabled && !string.IsNullOrWhiteSpace(provider.ApiKey);
        }
    }

    public async Task<AITextResponse> GenerateAsync(AITextRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Images is { Count: > 0 })
        {
            throw new InvalidOperationException("CohereTextClient does not support multimodal image inputs in the current runtime.");
        }

        var provider = _providerSettingsResolver.GetProvider(ProviderName);
        using var message = new HttpRequestMessage(HttpMethod.Post, AIProviderHttpRequestHelper.BuildUri(provider, "chat"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        message.Headers.Add("X-Client-Name", "APE_BE");
        message.Content = AIClientJsonHelpers.CreateJsonContent(new
        {
            model = request.Model,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            temperature = request.Temperature,
            max_tokens = request.MaxTokens
        });
        AIProviderHttpRequestHelper.ApplyAdditionalHeaders(message, provider.Headers, "Authorization", "X-Client-Name");

        using var timeoutCts = AIProviderHttpRequestHelper.CreateTimeoutCts(provider, cancellationToken);
        using var response = await _httpClient.SendAsync(message, timeoutCts?.Token ?? cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Cohere text request failed with status {StatusCode}: {Body}", response.StatusCode, raw);
            throw new AIProviderException(
                ProviderName,
                AIErrorCatalog.FromProviderFailure(ProviderName, (int)response.StatusCode, raw),
                $"Cohere request failed with status {(int)response.StatusCode}.",
                (int)response.StatusCode,
                raw);
        }

        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        var content = root.TryGetProperty("message", out var messageElement)
            ? AIClientJsonHelpers.FindFirstText(messageElement) ?? string.Empty
            : AIClientJsonHelpers.FindFirstText(root) ?? string.Empty;

        return new AITextResponse
        {
            Content = content,
            Provider = ProviderName,
            Model = root.TryGetProperty("model", out var modelElement) ? modelElement.GetString() : request.Model,
            InputTokens = AIClientJsonHelpers.GetInt32ByPath(root, "usage.tokens.input_tokens", "usage.input_tokens", "meta.billed_units.input_tokens", "meta.tokens.input_tokens", "billed_units.input_tokens"),
            OutputTokens = AIClientJsonHelpers.GetInt32ByPath(root, "usage.tokens.output_tokens", "usage.output_tokens", "meta.billed_units.output_tokens", "meta.tokens.output_tokens", "billed_units.output_tokens"),
            TotalTokens = AIClientJsonHelpers.GetInt32ByPath(root, "usage.tokens.total_tokens", "usage.total_tokens", "meta.billed_units.total_tokens", "meta.tokens.total_tokens", "billed_units.total_tokens"),
            UsageSource = AIClientJsonHelpers.GetInt32ByPath(root, "usage.tokens.input_tokens", "usage.input_tokens", "meta.billed_units.input_tokens", "meta.tokens.input_tokens", "billed_units.input_tokens").HasValue ||
                          AIClientJsonHelpers.GetInt32ByPath(root, "usage.tokens.output_tokens", "usage.output_tokens", "meta.billed_units.output_tokens", "meta.tokens.output_tokens", "billed_units.output_tokens").HasValue ||
                          AIClientJsonHelpers.GetInt32ByPath(root, "usage.tokens.total_tokens", "usage.total_tokens", "meta.billed_units.total_tokens", "meta.tokens.total_tokens", "billed_units.total_tokens").HasValue
                ? "provider_response"
                : "missing_from_provider_response",
            ReportedCostUsd = AIClientJsonHelpers.GetDecimalByPath(root, "meta.cost.usd", "usage.cost_usd", "cost.usd"),
            CostSource = AIClientJsonHelpers.GetDecimalByPath(root, "meta.cost.usd", "usage.cost_usd", "cost.usd").HasValue ? "provider_response" : "not_available",
            RawResponse = raw
        };
    }
}
