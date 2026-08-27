using Domain.Entities;

namespace Application.Interfaces;

public interface IKnowledgeChunkRepository
{
    Task CreateAsync(KnowledgeChunk chunk);
    Task CreateManyAsync(IEnumerable<KnowledgeChunk> chunks);
    Task<List<KnowledgeChunk>> GetByDocumentIdAsync(string documentId);
    Task<KnowledgeChunk?> GetFirstByDocumentIdAsync(string documentId);
    Task ReplaceForDocumentAsync(string documentId, IEnumerable<KnowledgeChunk> chunks);
    Task DeleteByDocumentIdAsync(string documentId);
    Task UpdateContentAsync(string chunkId, string newContent);
}
