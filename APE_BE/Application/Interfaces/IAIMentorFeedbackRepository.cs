using Domain.Entities;

namespace Application.Interfaces;

public interface IAIMentorFeedbackRepository
{
    Task CreateAsync(AIMentorFeedback feedback);
    Task<AIMentorFeedback?> GetLatestBySubmissionIdAsync(string submissionId);
    Task<List<AIMentorFeedback>> GetBySubmissionIdAsync(string submissionId, int take = 20);
    Task<long> CountBySubmissionIdsAsync(IReadOnlyCollection<string> submissionIds);
    Task<(List<AIMentorFeedback> Items, long Total)> ListAsync(
        string? submissionId,
        string? verdict,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int limit);
}
