/**
 * IFileExtractionService.cs
 * Interface for the file parsing subsystem in Step 2 of the APE AI Pipeline.
 * 
 * Responsibilities:
 * Parses incoming document binary streams (.pdf, .docx, .pptx) into plain text and image placeholders.
 */

using Application.DTOs;

namespace Application.Interfaces;

/// <summary>
/// Service contract for extracting text and image references from various file formats.
/// </summary>
public interface IFileExtractionService
{
    /// <summary>
    /// Extracts a limited preview excerpt from the document stream for fast pre-validation.
    /// </summary>
    /// <param name="fileStream">The binary stream of the uploaded document.</param>
    /// <param name="fileName">Original name of the file (used for extension resolution).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="ExtractionResultDto"/> containing preview text.</returns>
    Task<ExtractionResultDto> ExtractPreviewTextAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts full text and visual references from the document stream.
    /// </summary>
    /// <param name="fileStream">The binary stream of the uploaded document.</param>
    /// <param name="fileName">Original name of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="ExtractionResultDto"/> containing complete document text and media metadata.</returns>
    Task<ExtractionResultDto> ExtractTextAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}
