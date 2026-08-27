using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class AIExtractionDraftRepository : IAIExtractionDraftRepository
{
    private readonly IMongoCollection<AIExtractionDraft> _collection;

    public AIExtractionDraftRepository(DbContext dbContext)
    {
        _collection = dbContext.AIExtractionDrafts;
    }

    public Task CreateAsync(AIExtractionDraft draft)
        => _collection.InsertOneAsync(draft);

    public async Task<AIExtractionDraft?> GetByIdAsync(string draftId)
        => await _collection.Find(item => item.Id == draftId).FirstOrDefaultAsync();

    public async Task<AIExtractionDraft?> GetLatestByDocumentIdAsync(string documentId)
        => await _collection.Find(item => item.DocumentId == documentId)
            .SortByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync();

    public Task DeleteByDocumentIdAsync(string documentId)
        => _collection.DeleteManyAsync(item => item.DocumentId == documentId);

    public Task UpdateAsync(AIExtractionDraft draft)
        => _collection.ReplaceOneAsync(item => item.Id == draft.Id, draft);
}
