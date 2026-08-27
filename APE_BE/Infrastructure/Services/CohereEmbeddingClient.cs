/**
 * CohereEmbeddingClient.cs
 * Implementation of IAIEmbeddingClient for Cohere API (embed-multilingual-v3.0).
 * Part of Step 3 in the APE AI Pipeline.
 * Generates 1024-dimensional dense vector embeddings optimized for multilingual search.
 */

using System.Net.Http.Headers;
using System.Text.Json;
using Application.DTOs;
using Application.Common;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AI;

/// <summary>
/// Client for interacting with the Cohere Vector Embedding REST API endpoint (/v1/embed).
/// </summary>
public class CohereEmbeddingClient : IAIEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly IAIProviderSettingsResolver _providerSettingsResolver;
    private readonly ILogger<CohereEmbeddingClient> _logger;

    public CohereEmbeddingClient(HttpClient httpClient, IAIProviderSettingsResolver providerSettingsResolver, ILogger<CohereEmbeddingClient> logger)
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

    /// <summary>
    /// Sends a batch of text chunks to Cohere Embed API to calculate 1024-dim float embeddings.
    /// </summary>
    /// <param name="request">Request containing list of text chunks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="AIEmbeddingResponse"/> containing array of float vectors.</returns>
    public async Task<AIEmbeddingResponse> CreateEmbeddingsAsync(AIEmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        var provider = _providerSettingsResolver.GetProvider(ProviderName);
        using var message = new HttpRequestMessage(HttpMethod.Post, AIProviderHttpRequestHelper.BuildUri(provider, "embed"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        message.Headers.Add("X-Client-Name", "APE_BE");
        message.Content = AIClientJsonHelpers.CreateJsonContent(new
        {
            model = request.Model,
            texts = request.Inputs,
            input_type = "search_document",
            embedding_types = new[] { "float" }
        });
        AIProviderHttpRequestHelper.ApplyAdditionalHeaders(message, provider.Headers, "Authorization", "X-Client-Name");

        using var timeoutCts = AIProviderHttpRequestHelper.CreateTimeoutCts(provider, cancellationToken);
        using var response = await _httpClient.SendAsync(message, timeoutCts?.Token ?? cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Cohere embedding request failed with status {StatusCode}: {Body}", response.StatusCode, raw);
            throw new AIProviderException(
                ProviderName,
                AIErrorCatalog.FromProviderFailure(ProviderName, (int)response.StatusCode, raw),
                $"Cohere embedding request failed with status {(int)response.StatusCode}.",
                (int)response.StatusCode,
                raw);
        }

        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        var embeddings = new List<List<double>>();

        if (root.TryGetProperty("embeddings", out var embeddingElement) &&
            embeddingElement.TryGetProperty("float", out var floatEmbeddings) &&
            floatEmbeddings.ValueKind == JsonValueKind.Array)
        {
            foreach (var vectorElement in floatEmbeddings.EnumerateArray())
            {
                var vector = new List<double>();
                foreach (var item in vectorElement.EnumerateArray())
                {
                    vector.Add(item.GetDouble());
                }

                embeddings.Add(vector);
            }
        }

        return new AIEmbeddingResponse
        {
            Embeddings = embeddings,
            Provider = ProviderName,
            Model = root.TryGetProperty("model", out var modelElement) ? modelElement.GetString() : request.Model,
            TokensUsed = AIClientJsonHelpers.GetInt32ByPath(root, "meta.billed_units.input_tokens", "meta.tokens.input_tokens", "usage.input_tokens", "usage.tokens.input_tokens"),
            UsageSource = AIClientJsonHelpers.GetInt32ByPath(root, "meta.billed_units.input_tokens", "meta.tokens.input_tokens", "usage.input_tokens", "usage.tokens.input_tokens").HasValue
                ? "provider_response"
                : "missing_from_provider_response",
            ReportedCostUsd = AIClientJsonHelpers.GetDecimalByPath(root, "meta.cost.usd", "usage.cost_usd", "cost.usd"),
            CostSource = AIClientJsonHelpers.GetDecimalByPath(root, "meta.cost.usd", "usage.cost_usd", "cost.usd").HasValue ? "provider_response" : "not_available",
            RawResponse = raw
        };
    }
}
