using System.Net.Http.Headers;
using System.Text.Json;
using Application.DTOs;
using Application.Common;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

public class GeminiTextClient : IAITextClient
{
    private readonly HttpClient _httpClient;
    private readonly IAIProviderSettingsResolver _providerSettingsResolver;
    private readonly ILogger<GeminiTextClient> _logger;

    public GeminiTextClient(HttpClient httpClient, IAIProviderSettingsResolver providerSettingsResolver, ILogger<GeminiTextClient> logger)
    {
        _httpClient = httpClient;
        _providerSettingsResolver = providerSettingsResolver;
        _logger = logger;
    }

    public string ProviderName => "Gemini";

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
        var provider = _providerSettingsResolver.GetProvider(ProviderName);
        using var message = new HttpRequestMessage(HttpMethod.Post, AIProviderHttpRequestHelper.BuildUri(provider, $"models/{request.Model}:generateContent"));
        message.Headers.Add("x-goog-api-key", provider.ApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Content = AIClientJsonHelpers.CreateJsonContent(new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new { text = request.SystemPrompt }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = BuildGeminiParts(request)
                }
            },
            generationConfig = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxTokens
            }
        });
        AIProviderHttpRequestHelper.ApplyAdditionalHeaders(message, provider.Headers, "x-goog-api-key");

        using var timeoutCts = AIProviderHttpRequestHelper.CreateTimeoutCts(provider, cancellationToken);
        using var response = await _httpClient.SendAsync(message, timeoutCts?.Token ?? cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini text request failed with status {StatusCode}: {Body}", response.StatusCode, raw);
            throw new AIProviderException(
                ProviderName,
                AIErrorCatalog.FromProviderFailure(ProviderName, (int)response.StatusCode, raw),
                $"Gemini request failed with status {(int)response.StatusCode}.",
                (int)response.StatusCode,
                raw);
        }

        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        var content = root.TryGetProperty("candidates", out var candidates) &&
                      candidates.ValueKind == JsonValueKind.Array &&
                      candidates.GetArrayLength() > 0
            ? AIClientJsonHelpers.FindFirstText(candidates[0])
            : null;

        var usage = root.TryGetProperty("usageMetadata", out var usageMetadata) ? usageMetadata : default;

        return new AITextResponse
        {
            Content = content ?? string.Empty,
            Provider = ProviderName,
            Model = request.Model,
            InputTokens = usage.ValueKind == JsonValueKind.Object ? AIClientJsonHelpers.GetInt32(usage, "promptTokenCount") : null,
            OutputTokens = usage.ValueKind == JsonValueKind.Object ? AIClientJsonHelpers.GetInt32(usage, "candidatesTokenCount") : null,
            TotalTokens = usage.ValueKind == JsonValueKind.Object ? AIClientJsonHelpers.GetInt32(usage, "totalTokenCount") : null,
            UsageSource = usage.ValueKind == JsonValueKind.Object ? "provider_response" : "missing_from_provider_response",
            ReportedCostUsd = AIClientJsonHelpers.GetDecimalByPath(root, "usageMetadata.costUsd", "cost.usd"),
            CostSource = AIClientJsonHelpers.GetDecimalByPath(root, "usageMetadata.costUsd", "cost.usd").HasValue ? "provider_response" : "not_available",
            RawResponse = raw
        };
    }

    private static List<object> BuildGeminiParts(AITextRequest request)
    {
        var parts = new List<object>
        {
            new { text = request.UserPrompt }
        };

        foreach (var image in request.Images.Where(image => !string.IsNullOrWhiteSpace(image.Base64Data)))
        {
            parts.Add(new
            {
                inlineData = new
                {
                    mimeType = string.IsNullOrWhiteSpace(image.MimeType) ? "image/png" : image.MimeType,
                    data = image.Base64Data
                }
            });
        }

        return parts;
    }
}
