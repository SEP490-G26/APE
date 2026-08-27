using Domain.Entities;

namespace Application.Interfaces;

public interface IPEQuestionRepository
{
    Task<PEQuestion?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<List<PEQuestion>> GetByIdsAsync(
        List<string> ids,
        CancellationToken cancellationToken = default);

    Task<(List<PEQuestion> Items, long Total)> ListAsync(
        string? status,
        string? courseId,
        string? difficulty,
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PEQuestion?> GetByTitleAsync(
        string title,
        CancellationToken cancellationToken = default);

    Task<PEQuestion?> GetByFingerprintAsync(
        string courseId,
        string fingerprint,
        CancellationToken cancellationToken = default);

    Task<PEQuestion?> GetByTitleInScopeAsync(
        string sourceScope,
        string? courseId,
        string? ownerUserId,
        string title,
        CancellationToken cancellationToken = default);

    Task<PEQuestion?> GetByFingerprintInScopeAsync(
        string sourceScope,
        string? courseId,
        string? ownerUserId,
        string fingerprint,
        CancellationToken cancellationToken = default);

    Task CreateAsync(
        PEQuestion question,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        PEQuestion question,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<(List<PEQuestion> Items, long Total)> ListAdminOwnedAsync(
        string? status,
        string? courseId,
        string? difficulty,
        int page,
        int limit,
        CancellationToken cancellationToken = default);
    Task<List<PEQuestion>> ListAdminOwnedAllAsync(
        string? status,
        string? courseId,
        string? difficulty,
        CancellationToken cancellationToken = default);

    Task<(List<PEQuestion> Items, long Total)> ListOwnedByStudentAsync(
        string ownerUserId,
        string? courseId,
        string? difficulty,
        string? status,
        IReadOnlyCollection<string>? topicTags,
        string? keyword,
        int page,
        int limit,
        CancellationToken cancellationToken = default);
    Task<List<PEQuestion>> ListOwnedByStudentAllAsync(
        string ownerUserId,
        string? courseId,
        string? difficulty,
        string? status,
        IReadOnlyCollection<string>? topicTags,
        string? keyword,
        CancellationToken cancellationToken = default);

    Task<List<string>> GetOwnedStudentTopicTagsAsync(
        string ownerUserId,
        string? courseId,
        CancellationToken cancellationToken = default);
}
