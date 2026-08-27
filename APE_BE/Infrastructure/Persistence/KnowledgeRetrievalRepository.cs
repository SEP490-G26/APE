using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class KnowledgeRetrievalRepository : IKnowledgeRetrievalRepository
{
    private readonly IMongoCollection<KnowledgeChunk> _chunks;
    private readonly IMongoCollection<Document> _documents;

    public KnowledgeRetrievalRepository(DbContext dbContext)
    {
        _chunks = dbContext.KnowledgeChunks;
        _documents = dbContext.Documents;
    }

    public async Task<List<KnowledgeChunk>> SearchKnowledgeChunksAsync(
        string userId,
        string? courseId,
        string? documentId,
        string? chapterKey,
        string subject,
        string sourceScope,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<KnowledgeChunk>.Filter.Eq(chunk => chunk.RetrievalEnabled, true) &
                     Builders<KnowledgeChunk>.Filter.Eq(chunk => chunk.Status, "active");

        if (!string.IsNullOrWhiteSpace(documentId))
        {
            filter &= Builders<KnowledgeChunk>.Filter.Eq(chunk => chunk.DocumentId, documentId);
        }
        else if (!string.IsNullOrWhiteSpace(courseId))
        {
            filter &= Builders<KnowledgeChunk>.Filter.Eq(chunk => chunk.CourseId, courseId);
        }

        if (!string.IsNullOrWhiteSpace(subject))
        {
            filter &= Builders<KnowledgeChunk>.Filter.Eq(chunk => chunk.SubjectCode, subject);
        }

        if (!string.IsNullOrWhiteSpace(chapterKey))
        {
            filter &= Builders<KnowledgeChunk>.Filter.Eq(chunk => chunk.ChapterKey, chapterKey);
        }

        var chunks = await _chunks.Find(filter)
            .SortBy(chunk => chunk.ChunkIndex)
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            return chunks;
        }

        var normalizedScope = (sourceScope ?? "HYBRID").Trim().ToUpperInvariant();
        if (normalizedScope == "HYBRID")
        {
            return chunks;
        }

        var documentIds = chunks.Select(chunk => chunk.DocumentId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var docs = await _documents.Find(Builders<Document>.Filter.In(doc => doc.Id, documentIds))
            .ToListAsync(cancellationToken);
        var docsById = docs.ToDictionary(doc => doc.Id, StringComparer.OrdinalIgnoreCase);

        return chunks.Where(chunk =>
        {
            if (!docsById.TryGetValue(chunk.DocumentId, out var doc))
            {
                return false;
            }

            return normalizedScope switch
            {
                "BYOS" => string.Equals(doc.Source, "BYOS", StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(doc.UserId, userId, StringComparison.OrdinalIgnoreCase),
                "SYSTEM" => !string.Equals(doc.Source, "BYOS", StringComparison.OrdinalIgnoreCase),
                "PRIVATE" => string.Equals(doc.UserId, userId, StringComparison.OrdinalIgnoreCase),
                _ => true
            };
        }).ToList();
    }
}
