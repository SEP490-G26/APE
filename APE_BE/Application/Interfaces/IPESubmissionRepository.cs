using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces;

public interface IPESubmissionRepository
{
    Task<bool> TryCreateAsync(
        PE_Submission submission,
        CancellationToken cancellationToken = default);

    Task CreateAsync(
        PE_Submission submission,
        CancellationToken cancellationToken = default);

    Task<PE_Submission?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PE_Submission>> GetBySessionIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<int> GetLatestAttemptNumberAsync(
        string sessionId,
        string questionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PE_Submission>> GetByStatusesAsync(
        IReadOnlyCollection<SubmissionProcessingStatus> statuses,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PE_Submission?> TryAcquirePendingAsync(
        string submissionId,
        string leaseOwner,
        DateTime leaseAcquiredAt,
        DateTime leaseExpiresAt,
        CancellationToken cancellationToken = default);

    Task<PE_Submission?> TryReclaimExpiredProcessingAsync(
        string submissionId,
        string leaseOwner,
        DateTime leaseAcquiredAt,
        DateTime leaseExpiresAt,
        CancellationToken cancellationToken = default);

    Task<bool> RenewLeaseAsync(
        string submissionId,
        string leaseOwner,
        DateTime utcNow,
        DateTime newLeaseExpiresAt,
        CancellationToken cancellationToken = default);

    Task<bool> TryPersistExecutionStateAsync(
        PE_Submission submission,
        string leaseOwner,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<bool> TryPersistTerminalStateAsync(
        PE_Submission submission,
        string leaseOwner,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        PE_Submission submission,
        CancellationToken cancellationToken = default);
}
