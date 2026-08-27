using System.Net.Http.Headers;
using System.Text.Json;
using Application.DTOs;
using Application.Common;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

public class OpenAITextClient : IAITextClient
{
    private readonly HttpClient _httpClient;
    private readonly IAIProviderSettingsResolver _providerSettingsResolver;
    private readonly ILogger<OpenAITextClient> _logger;

    public OpenAITextClient(HttpClient httpClient, IAIProviderSettingsResolver providerSettingsResolver, ILogger<OpenAITextClient> logger)
    {
        _httpClient = httpClient;
        _providerSettingsResolver = providerSettingsResolver;
        _logger = logger;
    }

    public string ProviderName => "OpenAI";

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

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new
            {
                role = "system",
                content = request.SystemPrompt
            });
        }

        messages.Add(request.Images is { Count: > 0 }
            ? new
            {
                role = "user",
                content = BuildMultimodalContent(request)
            }
            : new
            {
                role = "user",
                content = (object)request.UserPrompt
            });

        // Determine token parameter: official OpenAI endpoints prefer max_completion_tokens, while generic proxies often prefer max_tokens
        bool preferMaxCompletionTokens = string.IsNullOrWhiteSpace(provider.BaseUrl) ||
                                         provider.BaseUrl.Contains("openai.com", StringComparison.OrdinalIgnoreCase);

        var (response, raw) = await SendRequestAsync(provider, request, messages, preferMaxCompletionTokens, cancellationToken);

        // Auto-retry with alternate token parameter if the provider rejects the selected parameter
        if (!response.IsSuccessStatusCode &&
            (raw.Contains("max_tokens", StringComparison.OrdinalIgnoreCase) || raw.Contains("max_completion_tokens", StringComparison.OrdinalIgnoreCase)) &&
            raw.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("OpenAI token parameter incompatibility detected. Retrying with alternate token parameter...");
            (response, raw) = await SendRequestAsync(provider, request, messages, !preferMaxCompletionTokens, cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI text request failed with status {StatusCode}: {Body}", response.StatusCode, raw);
            var providerMessage = TryExtractProviderMessage(raw);
            throw new AIProviderException(
                ProviderName,
                AIErrorCatalog.FromProviderFailure(ProviderName, (int)response.StatusCode, raw),
                string.IsNullOrWhiteSpace(providerMessage)
                    ? $"OpenAI request failed with status {(int)response.StatusCode}."
                    : $"OpenAI request failed: {providerMessage}",
                (int)response.StatusCode,
                raw);
        }

        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        var choices = root.TryGetProperty("choices", out var choicesElement) ? choicesElement : default;
        var firstChoice = choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0
            ? choices[0]
            : default;
        var messageElement = firstChoice.ValueKind == JsonValueKind.Object && firstChoice.TryGetProperty("message", out var choiceMessage)
            ? choiceMessage
            : default;
        var content = messageElement.ValueKind == JsonValueKind.Object && messageElement.TryGetProperty("content", out var contentElement)
            ? contentElement.GetString() ?? string.Empty
            : (root.TryGetProperty("output", out var outputElement) ? AIClientJsonHelpers.FindFirstText(outputElement) ?? string.Empty : string.Empty);
        var usage = root.TryGetProperty("usage", out var usageElement) ? usageElement : default;

        return new AITextResponse
        {
            Content = content,
            Provider = ProviderName,
            Model = root.TryGetProperty("model", out var modelElement) ? modelElement.GetString() : request.Model,
            InputTokens = usage.ValueKind == JsonValueKind.Object ? AIClientJsonHelpers.GetInt32(usage, "prompt_tokens") ?? AIClientJsonHelpers.GetInt32(usage, "input_tokens") : null,
            OutputTokens = usage.ValueKind == JsonValueKind.Object ? AIClientJsonHelpers.GetInt32(usage, "completion_tokens") ?? AIClientJsonHelpers.GetInt32(usage, "output_tokens") : null,
            TotalTokens = usage.ValueKind == JsonValueKind.Object ? AIClientJsonHelpers.GetInt32(usage, "total_tokens") : null,
            UsageSource = usage.ValueKind == JsonValueKind.Object ? "provider_response" : "missing_from_provider_response",
            ReportedCostUsd = AIClientJsonHelpers.GetDecimalByPath(root, "usage.cost_usd", "cost.usd"),
            CostSource = AIClientJsonHelpers.GetDecimalByPath(root, "usage.cost_usd", "cost.usd").HasValue ? "provider_response" : "not_available",
            RawResponse = raw
        };
    }

    private async Task<(HttpResponseMessage Response, string Raw)> SendRequestAsync(
        Application.Options.AIProviderOptions provider,
        AITextRequest request,
        List<object> messages,
        bool useMaxCompletionTokens,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, AIProviderHttpRequestHelper.BuildUri(provider, "chat/completions"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

        object payload = useMaxCompletionTokens
            ? new
            {
                model = request.Model,
                messages,
                temperature = request.Temperature,
                max_completion_tokens = request.MaxTokens
            }
            : new
            {
                model = request.Model,
                messages,
                temperature = request.Temperature,
                max_tokens = request.MaxTokens
            };

        message.Content = AIClientJsonHelpers.CreateJsonContent(payload);
        AIProviderHttpRequestHelper.ApplyAdditionalHeaders(message, provider.Headers, "Authorization");

        using var timeoutCts = AIProviderHttpRequestHelper.CreateTimeoutCts(provider, cancellationToken);
        var effectiveToken = timeoutCts?.Token ?? cancellationToken;
        using var response = await _httpClient.SendAsync(message, effectiveToken);
        var raw = await response.Content.ReadAsStringAsync(effectiveToken);
        return (response, raw);
    }

    private static List<object> BuildMultimodalContent(AITextRequest request)
    {
        var content = new List<object>
        {
            new
            {
                type = "text",
                text = request.UserPrompt
            }
        };

        foreach (var image in request.Images.Where(image => !string.IsNullOrWhiteSpace(image.Base64Data)))
        {
            content.Add(new
            {
                type = "image_url",
                image_url = new
                {
                    url = AIClientJsonHelpers.ToDataUrl(image.MimeType, image.Base64Data)
                }
            });
        }

        return content;
    }

    private static string? TryExtractProviderMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.Object &&
                errorElement.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString();
            }
        }
        catch
        {
            // Ignore parse errors and fall back to the generic message.
        }

        return null;
    }
}
