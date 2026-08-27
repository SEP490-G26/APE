using Domain.Entities;

namespace Application.Interfaces;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(string id);
    Task CreateAsync(Document doc);
    Task UpdateAsync(Document doc);                   
    Task<Document?> FindReusableByFileChecksumAsync(string source, string? courseId, string? userId, string fileChecksum);
    Task<Document?> FindReusableByNormalizedChecksumAsync(string source, string? courseId, string? userId, string normalizedContentChecksum);
    Task<int> CountByUserAsync(string userId);
    Task<(List<Document> Items, long Total)> ListByUserAsync(string userId, int page, int limit);
    Task<(List<Document> Items, long Total)> ListSystemAsync(int page, int limit, string? courseId = null);
    Task<(List<Document> Items, long Total)> ListByCourseAsync(string courseId, int page, int limit);
}
