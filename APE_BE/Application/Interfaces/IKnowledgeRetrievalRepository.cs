using Domain.Entities;

namespace Application.Interfaces;

public interface IKnowledgeRetrievalRepository
{
    Task<List<KnowledgeChunk>> SearchKnowledgeChunksAsync(
        string userId,
        string? courseId,
        string? documentId,
        string? chapterKey,
        string subject,
        string sourceScope,
        CancellationToken cancellationToken = default);
}
