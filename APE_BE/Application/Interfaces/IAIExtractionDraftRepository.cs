using Domain.Entities;

namespace Application.Interfaces;

public interface IAIExtractionDraftRepository
{
    Task CreateAsync(AIExtractionDraft draft);
    Task<AIExtractionDraft?> GetByIdAsync(string draftId);
    Task<AIExtractionDraft?> GetLatestByDocumentIdAsync(string documentId);
    Task DeleteByDocumentIdAsync(string documentId);
    Task UpdateAsync(AIExtractionDraft draft);
}
