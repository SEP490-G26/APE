using Domain.Entities;
using Application.DTOs;

namespace Application.Interfaces;

public interface IAIUsageLogRepository
{
    Task CreateAsync(AIUsageLog log);
    Task<(List<AIUsageLog> Items, long Total)> ListAsync(
        string? triggeredBy,
        string? agentId,
        string? provider,
        string? model,
        string? feature,
        string? step,
        bool? fallbackUsed,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int limit);
    Task<(List<string> TriggeredByIds, List<string> Features, List<string> Providers, List<string> Models)> ListFilterOptionsAsync(
        DateTime? fromDate,
        DateTime? toDate);
    Task<List<AIUsageSummaryItemDto>> SummarizeAsync(
        string? agentId,
        string? provider,
        string? model,
        string? feature,
        string? step,
        DateTime? fromDate,
        DateTime? toDate,
        int take);
    Task<List<AIArtifactUsageSummaryItemDto>> SummarizeByArtifactsAsync(
        string? agentId,
        string? feature,
        string? step,
        string? promptKey,
        string? promptVersion,
        string? policyKey,
        string? policyVersion,
        string? rubricKey,
        string? rubricVersion,
        DateTime? fromDate,
        DateTime? toDate,
        int take);
    Task<List<AIUserUsageSummaryItemDto>> SummarizeByUsersAsync(
        string? triggeredBy,
        string? provider,
        string? model,
        string? feature,
        string? step,
        DateTime? fromDate,
        DateTime? toDate,
        int take);
}
