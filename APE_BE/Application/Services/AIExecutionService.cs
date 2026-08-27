/**
 * AIExecutionService.cs
 * Core AI Dispatcher and Auto-Fallback Circuit Breaker in Step 4 of the APE AI Pipeline.
 * 
 * Responsibilities:
 * 1. Resolves appropriate AI text/embedding clients from AIClientFactory.
 * 2. Attempts execution on the Primary Provider/Model.
 * 3. On failure (HTTP 502, 400, 429, Timeout), automatically activates Fallback Provider/Model.
 * 4. Decorates responses with metadata (FromFallback, FallbackReasonCode, Token counts, Latency).
 */

using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

/// <summary>
/// Central execution coordinator for all AI operations in APE, providing resilience and failover routing.
/// </summary>
public class AIExecutionService : IAIExecutionService
{
    private readonly IAIClientFactory _clientFactory;
    private readonly AIOptions _options;
    private readonly ILogger<AIExecutionService> _logger;

    public AIExecutionService(
        IAIClientFactory clientFactory,
        IOptions<AIOptions> options,
        ILogger<AIExecutionService> logger)
    {
        _clientFactory = clientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executes a text generation request on the primary provider.
    /// If primary fails, catches the failure and seamlessly executes on the configured fallback provider.
    /// </summary>
    /// <param name="resolved">Routing options containing primary and fallback configurations.</param>
    /// <param name="request">Text prompt payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="AITextResponse"/> from either primary or fallback provider.</returns>
    public async Task<AITextResponse> ExecuteTextAsync(
        AIResolvedExecutionOptions resolved,
        AITextRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("AI is disabled.");
        }

        var primaryFailure = await TryPrimaryTextAsync(resolved, request, cancellationToken);
        if (primaryFailure.Response is not null)
        {
            return primaryFailure.Response;
        }

        if (!CanUseFallback(resolved))
        {
            throw primaryFailure.Exception ?? new InvalidOperationException(
                $"AI text execution failed for feature '{resolved.FeatureName}' and no fallback is configured.");
        }

        var fallbackProvider = resolved.FallbackProvider!;
        var fallbackModel = resolved.FallbackModel!;
        var fallbackClient = _clientFactory.GetTextClient(fallbackProvider);
        if (fallbackClient is null || !fallbackClient.IsConfigured)
        {
            throw primaryFailure.Exception ?? new InvalidOperationException(
                $"AI fallback text client '{fallbackProvider}' is not configured for feature '{resolved.FeatureName}'.");
        }

        try
        {
            var fallbackRequest = CloneTextRequest(request, fallbackProvider, fallbackModel, resolved);
            var fallbackResponse = await fallbackClient.GenerateAsync(fallbackRequest, cancellationToken);
            fallbackResponse.FromFallback = true;
            fallbackResponse.FallbackFromProvider = resolved.Provider;
            fallbackResponse.FallbackFromModel = resolved.Model;
            fallbackResponse.FallbackReasonCode = primaryFailure.ReasonCode;
            fallbackResponse.FallbackReason = primaryFailure.Reason;

            _logger.LogWarning(
                "AI text execution fallback activated for feature {Feature}. Primary {PrimaryProvider}/{PrimaryModel} -> fallback {FallbackProvider}/{FallbackModel}. ReasonCode={ReasonCode}",
                resolved.FeatureName,
                resolved.Provider,
                resolved.Model,
                fallbackProvider,
                fallbackModel,
                primaryFailure.ReasonCode);

            return fallbackResponse;
        }
        catch (Exception fallbackException)
        {
            _logger.LogWarning(
                fallbackException,
                "AI text fallback also failed for feature {Feature}. Primary {PrimaryProvider}/{PrimaryModel}, fallback {FallbackProvider}/{FallbackModel}.",
                resolved.FeatureName,
                resolved.Provider,
                resolved.Model,
                fallbackProvider,
                fallbackModel);

            throw new AIProviderException(
                fallbackProvider,
                "ai.runtime.fallback_chain_failed",
                $"AI text execution failed on both primary and fallback providers for feature '{resolved.FeatureName}'.",
                502,
                null,
                fallbackException,
                new
                {
                    feature = resolved.FeatureName,
                    execution_type = "text",
                    primary = new
                    {
                        provider = resolved.Provider,
                        model = resolved.Model,
                        reason_code = primaryFailure.ReasonCode,
                        reason = primaryFailure.Reason,
                        exception_type = primaryFailure.Exception?.GetType().Name
                    },
                    fallback = new
                    {
                        provider = fallbackProvider,
                        model = fallbackModel,
                        reason_code = AIErrorCatalog.FromException(fallbackException, fallbackProvider),
                        reason = fallbackException.Message,
                        exception_type = fallbackException.GetType().Name
                    }
                });
        }
    }

    public async Task<AIEmbeddingResponse> ExecuteEmbeddingAsync(
        AIResolvedExecutionOptions resolved,
        AIEmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("AI is disabled.");
        }

        var primaryFailure = await TryPrimaryEmbeddingAsync(resolved, request, cancellationToken);
        if (primaryFailure.Response is not null)
        {
            return primaryFailure.Response;
        }

        if (!CanUseFallback(resolved))
        {
            throw primaryFailure.Exception ?? new InvalidOperationException(
                $"AI embedding execution failed for feature '{resolved.FeatureName}' and no fallback is configured.");
        }

        var fallbackProvider = resolved.FallbackProvider!;
        var fallbackModel = resolved.FallbackModel!;
        var fallbackClient = _clientFactory.GetEmbeddingClient(fallbackProvider);
        if (fallbackClient is null || !fallbackClient.IsConfigured)
        {
            throw primaryFailure.Exception ?? new InvalidOperationException(
                $"AI fallback embedding client '{fallbackProvider}' is not configured for feature '{resolved.FeatureName}'.");
        }

        try
        {
            var fallbackRequest = CloneEmbeddingRequest(request, fallbackProvider, fallbackModel);
            var fallbackResponse = await fallbackClient.CreateEmbeddingsAsync(fallbackRequest, cancellationToken);
            fallbackResponse.FromFallback = true;
            fallbackResponse.FallbackFromProvider = resolved.Provider;
            fallbackResponse.FallbackFromModel = resolved.Model;
            fallbackResponse.FallbackReasonCode = primaryFailure.ReasonCode;
            fallbackResponse.FallbackReason = primaryFailure.Reason;

            _logger.LogWarning(
                "AI embedding fallback activated for feature {Feature}. Primary {PrimaryProvider}/{PrimaryModel} -> fallback {FallbackProvider}/{FallbackModel}. ReasonCode={ReasonCode}",
                resolved.FeatureName,
                resolved.Provider,
                resolved.Model,
                fallbackProvider,
                fallbackModel,
                primaryFailure.ReasonCode);

            return fallbackResponse;
        }
        catch (Exception fallbackException)
        {
            _logger.LogWarning(
                fallbackException,
                "AI embedding fallback also failed for feature {Feature}. Primary {PrimaryProvider}/{PrimaryModel}, fallback {FallbackProvider}/{FallbackModel}.",
                resolved.FeatureName,
                resolved.Provider,
                resolved.Model,
                fallbackProvider,
                fallbackModel);

            throw new AIProviderException(
                fallbackProvider,
                "ai.runtime.fallback_chain_failed",
                $"AI embedding execution failed on both primary and fallback providers for feature '{resolved.FeatureName}'.",
                502,
                null,
                fallbackException,
                new
                {
                    feature = resolved.FeatureName,
                    execution_type = "embedding",
                    primary = new
                    {
                        provider = resolved.Provider,
                        model = resolved.Model,
                        reason_code = primaryFailure.ReasonCode,
                        reason = primaryFailure.Reason,
                        exception_type = primaryFailure.Exception?.GetType().Name
                    },
                    fallback = new
                    {
                        provider = fallbackProvider,
                        model = fallbackModel,
                        reason_code = AIErrorCatalog.FromException(fallbackException, fallbackProvider),
                        reason = fallbackException.Message,
                        exception_type = fallbackException.GetType().Name
                    }
                });
        }
    }

    private async Task<TextExecutionFailure> TryPrimaryTextAsync(
        AIResolvedExecutionOptions resolved,
        AITextRequest request,
        CancellationToken cancellationToken)
    {
        var primaryClient = _clientFactory.GetTextClient(resolved.Provider);
        if (primaryClient is null)
        {
            return new TextExecutionFailure(
                null,
                "ai.runtime.primary_client_missing",
                $"Primary AI text client '{resolved.Provider}' is not available.");
        }

        if (!primaryClient.IsConfigured)
        {
            return new TextExecutionFailure(
                null,
                "ai.runtime.primary_not_configured",
                $"Primary AI text client '{resolved.Provider}' is not configured.");
        }

        try
        {
            var primaryRequest = CloneTextRequest(request, resolved.Provider, resolved.Model, resolved);
            var response = await primaryClient.GenerateAsync(primaryRequest, cancellationToken);
            response.FromFallback = false;
            return new TextExecutionFailure(response, null, null, null);
        }
        catch (Exception exception)
        {
            return new TextExecutionFailure(
                null,
                AIErrorCatalog.FromException(exception, resolved.Provider),
                exception.Message,
                exception);
        }
    }

    private async Task<EmbeddingExecutionFailure> TryPrimaryEmbeddingAsync(
        AIResolvedExecutionOptions resolved,
        AIEmbeddingRequest request,
        CancellationToken cancellationToken)
    {
        var primaryClient = _clientFactory.GetEmbeddingClient(resolved.Provider);
        if (primaryClient is null)
        {
            return new EmbeddingExecutionFailure(
                null,
                "ai.runtime.primary_client_missing",
                $"Primary AI embedding client '{resolved.Provider}' is not available.");
        }

        if (!primaryClient.IsConfigured)
        {
            return new EmbeddingExecutionFailure(
                null,
                "ai.runtime.primary_not_configured",
                $"Primary AI embedding client '{resolved.Provider}' is not configured.");
        }

        try
        {
            var primaryRequest = CloneEmbeddingRequest(request, resolved.Provider, resolved.Model);
            var response = await primaryClient.CreateEmbeddingsAsync(primaryRequest, cancellationToken);
            response.FromFallback = false;
            return new EmbeddingExecutionFailure(response, null, null, null);
        }
        catch (Exception exception)
        {
            return new EmbeddingExecutionFailure(
                null,
                AIErrorCatalog.FromException(exception, resolved.Provider),
                exception.Message,
                exception);
        }
    }

    private static bool CanUseFallback(AIResolvedExecutionOptions resolved)
    {
        return !string.IsNullOrWhiteSpace(resolved.FallbackProvider) &&
               !string.IsNullOrWhiteSpace(resolved.FallbackModel) &&
               (!string.Equals(resolved.Provider, resolved.FallbackProvider, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(resolved.Model, resolved.FallbackModel, StringComparison.OrdinalIgnoreCase));
    }

    private static AITextRequest CloneTextRequest(
        AITextRequest request,
        string provider,
        string model,
        AIResolvedExecutionOptions resolved)
    {
        return new AITextRequest
        {
            FeatureName = request.FeatureName,
            SystemPrompt = request.SystemPrompt,
            UserPrompt = request.UserPrompt,
            Images = request.Images
                .Select(image => new AIImageInput
                {
                    ImageId = image.ImageId,
                    MimeType = image.MimeType,
                    Base64Data = image.Base64Data,
                    Reference = image.Reference,
                    Label = image.Label,
                    Width = image.Width,
                    Height = image.Height
                })
                .ToList(),
            Provider = provider,
            Model = model,
            Temperature = request.Temperature ?? resolved.Temperature,
            MaxTokens = request.MaxTokens ?? resolved.MaxTokens,
            RuntimeOverride = request.RuntimeOverride,
            Metadata = request.Metadata is null
                ? null
                : new Dictionary<string, string>(request.Metadata, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static AIEmbeddingRequest CloneEmbeddingRequest(
        AIEmbeddingRequest request,
        string provider,
        string model)
    {
        return new AIEmbeddingRequest
        {
            FeatureName = request.FeatureName,
            Inputs = new List<string>(request.Inputs),
            Provider = provider,
            Model = model,
            RuntimeOverride = request.RuntimeOverride,
            Metadata = request.Metadata is null
                ? null
                : new Dictionary<string, string>(request.Metadata, StringComparer.OrdinalIgnoreCase)
        };
    }

    private sealed record TextExecutionFailure(
        AITextResponse? Response,
        string? ReasonCode,
        string? Reason,
        Exception? Exception = null);

    private sealed record EmbeddingExecutionFailure(
        AIEmbeddingResponse? Response,
        string? ReasonCode,
        string? Reason,
        Exception? Exception = null);
}
