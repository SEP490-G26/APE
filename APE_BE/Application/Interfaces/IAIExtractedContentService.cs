/**
 * IAIExtractedContentService.cs
 * Interface for AI-powered document normalization, image OCR, and chapter/topic structure detection.
 * Part of Step 2 in the APE AI Pipeline.
 */

using Application.DTOs;

namespace Application.Interfaces;

/// <summary>
/// Service contract for normalizing extracted document text, transcribing visual content,
/// and building hierarchical chapter/topic trees using LLMs.
/// </summary>
public interface IAIExtractedContentService
{
    /// <summary>
    /// Normalizes raw extracted document text into clean Markdown and performs structure detection
    /// to extract Chapters and Topics.
    /// </summary>
    /// <param name="extracted">Raw extraction DTO containing text, slides, and image placeholders.</param>
    /// <param name="runtimeOverride">Optional runtime AI provider/model overrides.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="ExtractedContentResultDto"/> with clean markdown and detected chapter hierarchy.</returns>
    Task<ExtractedContentResultDto> NormalizeAsync(ExtractionResultDto extracted, AIRuntimeOverride? runtimeOverride = null, CancellationToken cancellationToken = default);
}
