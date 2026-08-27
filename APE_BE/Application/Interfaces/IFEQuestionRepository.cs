using Domain.Entities;

namespace Application.Interfaces;

public interface IFEQuestionRepository
{
    Task<FEQuestion?> GetByIdAsync(string id);
    Task<List<FEQuestion>> GetByIdsAsync(List<string> ids);
    Task<(List<FEQuestion> Items, long Total)> ListAsync(string? status, string? courseId, string? difficulty, int page, int limit);
    Task UpdateAsync(FEQuestion question);
    Task<FEQuestion?> GetByTitleAsync(string title);          
    Task<FEQuestion?> GetByFingerprintAsync(string courseId, string fingerprint);
    Task<FEQuestion?> GetByTitleInScopeAsync(string sourceScope, string? courseId, string? ownerUserId, string title);
    Task<FEQuestion?> GetByFingerprintInScopeAsync(string sourceScope, string? courseId, string? ownerUserId, string fingerprint);
    Task CreateAsync(FEQuestion question);
    Task InsertManyAsync(List<FEQuestion> questions);
    Task<List<string>> GetAllTitlesAsync();
    Task DeleteAsync(string id);
    Task<(List<FEQuestion> Items, long Total)> ListAdminOwnedAsync(string? status, string? courseId, string? difficulty, int page, int limit);
    Task<List<FEQuestion>> ListAdminOwnedAllAsync(string? status, string? courseId, string? difficulty);
    Task<(List<FEQuestion> Items, long Total)> ListOwnedByStudentAsync(string ownerUserId, string? courseId, string? difficulty, string? status, IReadOnlyCollection<string>? topicTags, string? keyword, int page, int limit);
    Task<List<FEQuestion>> ListOwnedByStudentAllAsync(string ownerUserId, string? courseId, string? difficulty, string? status, IReadOnlyCollection<string>? topicTags, string? keyword);
    Task<List<string>> GetOwnedStudentTopicTagsAsync(string ownerUserId, string? courseId);
}
