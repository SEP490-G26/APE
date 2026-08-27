using Application.DTOs;
using Domain.Entities;

namespace Application.Interfaces;

public interface IAIVndBillingTransactionRepository
{
    Task CreateAsync(AIVndBillingTransaction transaction, CancellationToken cancellationToken = default);
    Task<AIVndBillingTransaction?> GetByIdAsync(string transactionId, CancellationToken cancellationToken = default);
    Task UpdateAsync(AIVndBillingTransaction transaction, CancellationToken cancellationToken = default);
    Task<AIVndBillingTransaction?> GetLatestBySourceEntityAsync(
        string sourceEntityType,
        string sourceEntityId,
        string? featureKey = null,
        CancellationToken cancellationToken = default);
    Task<(List<AIVndBillingTransaction> Items, long Total)> ListAsync(
        string? userId,
        string? featureKey,
        string? status,
        string? sourceEntityType,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int limit,
        CancellationToken cancellationToken = default);
    Task<List<AIVndBillingTransactionSummaryItemDto>> SummarizeAsync(
        string? featureKey,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int take,
        CancellationToken cancellationToken = default);
    Task<List<AIVndBillingDailySummaryItemDto>> SummarizeDailyAsync(
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int take,
        CancellationToken cancellationToken = default);
}
