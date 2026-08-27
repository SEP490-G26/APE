/**
 * IAIEmbeddingClient.cs
 * Interface for AI text embedding clients (e.g., Cohere embed-multilingual-v3.0).
 * Part of Step 3 in the APE AI Pipeline.
 */

using Application.DTOs;

namespace Application.Interfaces;

/// <summary>
/// Service contract for generating dense vector embeddings from text chunks.
/// </summary>
public interface IAIEmbeddingClient
{
    /// <summary>
    /// Name of the embedding provider (e.g., "Cohere").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Indicates whether the embedding client has valid API credentials configured.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Generates high-dimensional vector embeddings for a list of input text strings.
    /// </summary>
    /// <param name="request">Request containing texts, model name, and input type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="AIEmbeddingResponse"/> containing float vector arrays.</returns>
    Task<AIEmbeddingResponse> CreateEmbeddingsAsync(AIEmbeddingRequest request, CancellationToken cancellationToken = default);
}
