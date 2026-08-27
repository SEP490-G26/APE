using Application.DTOs;

namespace Application.Interfaces;

public interface IAIEmbeddingTaggingService
{
    Task<EmbeddingTaggingRunResult> CreateChunksAsync(
        string documentId,
        string courseId,
        string userId,
        string content,
        string sourceType,
        string? subjectCode = null,
        ExtractionStructuralHintsDto? structuralHints = null,
        AIRuntimeOverride? embeddingOverride = null,
        AIRuntimeOverride? taggingOverride = null,
        CancellationToken cancellationToken = default);
}
