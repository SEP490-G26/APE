/**
 * IAIExecutionService.cs
 * Interface for AI Execution and Auto-Fallback Orchestration.
 * Core routing hub in Step 4 of the APE AI Pipeline.
 */

using Application.DTOs;

namespace Application.Interfaces;

/// <summary>
/// Service contract for executing AI text generation and vector embedding requests.
/// Manages client resolution, auto-fallback circuit breaking, token usage logging, and cost accounting.
/// </summary>
public interface IAIExecutionService
{
    /// <summary>
    /// Executes a text generation request with automatic fallback if the primary provider fails.
    /// </summary>
    /// <param name="resolved">Resolved execution options (primary/fallback provider and model, timeouts, thresholds).</param>
    /// <param name="request">Text request (system prompt, user prompt, images, temperature, max_tokens).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="AITextResponse"/> containing model response and token consumption metrics.</returns>
    Task<AITextResponse> ExecuteTextAsync(
        AIResolvedExecutionOptions resolved,
        AITextRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an embedding request with configured provider settings.
    /// </summary>
    Task<AIEmbeddingResponse> ExecuteEmbeddingAsync(
        AIResolvedExecutionOptions resolved,
        AIEmbeddingRequest request,
        CancellationToken cancellationToken = default);
}
