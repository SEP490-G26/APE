using Domain.Entities;

namespace Application.Interfaces;

public interface IPracticeSessionRepository
{
    Task<PracticeSession?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<PracticeSession?> GetActiveSessionAsync(
        string userId,
        string examId,
        CancellationToken cancellationToken = default);

    Task CreateAsync(
        PracticeSession session,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        PracticeSession session,
        CancellationToken cancellationToken = default);

    Task<List<PracticeSession>> GetByUserIdAsync(
        string userId,
        int page = 1,
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<int> CountByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<PracticeSession?> EndSessionAsync(
        string sessionId,
        DateTime endTime,
        double? totalScore = null,
        CancellationToken cancellationToken = default);
}