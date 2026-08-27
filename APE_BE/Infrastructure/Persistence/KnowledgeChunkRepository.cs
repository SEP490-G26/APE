using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class KnowledgeChunkRepository : IKnowledgeChunkRepository
{
    private readonly IMongoCollection<KnowledgeChunk> _col;

    public KnowledgeChunkRepository(DbContext ctx)
    {
        _col = ctx.KnowledgeChunks;
    }

    public async Task CreateAsync(KnowledgeChunk chunk) => await _col.InsertOneAsync(chunk);

    public async Task CreateManyAsync(IEnumerable<KnowledgeChunk> chunks)
    {
        var chunkList = chunks.ToList();
        if (chunkList.Count == 0)
        {
            return;
        }

        await _col.InsertManyAsync(chunkList);
    }

    public async Task<List<KnowledgeChunk>> GetByDocumentIdAsync(string documentId) =>
        await _col.Find(k => k.DocumentId == documentId).SortBy(k => k.ChunkIndex).ToListAsync();

    public async Task<KnowledgeChunk?> GetFirstByDocumentIdAsync(string documentId) =>
        await _col.Find(k => k.DocumentId == documentId).SortBy(k => k.ChunkIndex).FirstOrDefaultAsync();

    public async Task ReplaceForDocumentAsync(string documentId, IEnumerable<KnowledgeChunk> chunks)
    {
        await _col.DeleteManyAsync(k => k.DocumentId == documentId);
        await CreateManyAsync(chunks);
    }

    public async Task DeleteByDocumentIdAsync(string documentId) =>
        await _col.DeleteManyAsync(k => k.DocumentId == documentId);

    public async Task UpdateContentAsync(string chunkId, string newContent)
    {
        var update = Builders<KnowledgeChunk>.Update
            .Set(k => k.RawText, newContent)
            .Set(k => k.NormalizedText, newContent)
            .Set(k => k.MarkdownText, newContent)
            .Set(k => k.CharCount, newContent?.Length ?? 0)
            .Set(k => k.WordCount, string.IsNullOrWhiteSpace(newContent) ? 0 : newContent.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
            .Set(k => k.UpdatedAt, DateTime.UtcNow);
        await _col.UpdateOneAsync(k => k.Id == chunkId, update);
    }
}
