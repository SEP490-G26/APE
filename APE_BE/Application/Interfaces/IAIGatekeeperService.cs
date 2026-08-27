/**
 * IAIGatekeeperService.cs
 * Interface and result model for the AI Gatekeeper safety and domain validation subsystem.
 * Role in system: Acts as the primary security firewall and content validator before documents
 * enter the extraction, chunking, and question generation pipelines.
 */

namespace Application.Interfaces;

using Application.DTOs;

/// <summary>
/// Represents the evaluation result returned by the Gatekeeper Agent.
/// Contains domain suitability verdict, safety status, token consumption, and fallback metadata.
/// </summary>
public class GatekeeperResult
{
    /// <summary>
    /// Indicates whether the document is supported and safe for processing.
    /// </summary>
    public bool IsSupported { get; set; }

    /// <summary>
    /// The primary academic domain identified (e.g., "Programming", "OOP", "Data Structures").
    /// </summary>
    public string PrimaryDomain { get; set; } = string.Empty;

    /// <summary>
    /// Detailed human-readable explanation of why the document was accepted or rejected.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Machine-readable verdict code: "supported" or "unsupported".
    /// </summary>
    public string Verdict { get; set; } = "unsupported";

    /// <summary>
    /// List of matched subjects from the system taxonomy.
    /// </summary>
    public List<string> MatchedSubjects { get; set; } = new();

    /// <summary>
    /// List of topics detected within the document excerpt.
    /// </summary>
    public List<string> DetectedTopics { get; set; } = new();

    /// <summary>
    /// AI classification confidence score (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Structured rejection code (e.g., "empty_content", "off_topic", "prompt_injection").
    /// </summary>
    public string? RejectionReasonCode { get; set; }

    /// <summary>
    /// The AI provider that executed the evaluation (e.g., "DeepSeek", "OpenAI").
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// The model name that performed the evaluation.
    /// </summary>
    public string? Model { get; set; }

    public string? ConfiguredModel { get; set; }
    public string? EffectiveModel { get; set; }
    public string? UsageSource { get; set; }
    public string? CostSource { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public decimal? CostUsd { get; set; }

    /// <summary>
    /// True if the primary provider failed and the fallback provider fulfilled the request.
    /// </summary>
    public bool FallbackUsed { get; set; }
    public string? FallbackFromProvider { get; set; }
    public string? FallbackFromModel { get; set; }
    public string? FallbackReasonCode { get; set; }

    public string Category
    {
        get => PrimaryDomain;
        set => PrimaryDomain = value;
    }
}

/// <summary>
/// Service contract for validating document content against academic domain boundaries and safety policies.
/// </summary>
public interface IAIGatekeeperService
{
    /// <summary>
    /// Validates raw document content for language, domain relevance (OOP/Programming), and security.
    /// </summary>
    /// <param name="content">The plain text extracted from the user's document.</param>
    /// <param name="context">Optional context such as file name, subject hints, or explicit language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GatekeeperResult"/> indicating whether the document is allowed into the system.</returns>
    Task<GatekeeperResult> ValidateAsync(string content, GatekeeperRequestContext? context = null, CancellationToken cancellationToken = default);
}
