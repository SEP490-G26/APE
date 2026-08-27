using Application.DTOs;

namespace Application.Interfaces;

public interface IAIVndBillingService
{
    Task<AIVndBillingConfigDto> GetConfigAsync(CancellationToken cancellationToken = default);
    Task<AIVndBillingConfigDto> UpsertConfigAsync(AIVndBillingConfigUpsertRequestDto request, string updatedBy, CancellationToken cancellationToken = default);
    Task EnsureMinimumBalanceAsync(string userId, CancellationToken cancellationToken = default);
    Task<AIVndChargeResult> ChargeAsync(AIVndChargeRequest request, CancellationToken cancellationToken = default);
    Task<AIVndChargeResult> RecordUsageAsync(AIVndChargeRequest request, CancellationToken cancellationToken = default);
    Task<AIVndRefundResult> RefundAsync(AIVndRefundRequest request, CancellationToken cancellationToken = default);
}

public sealed class AIVndChargeRequest
{
    public string UserId { get; init; } = null!;
    public string FeatureKey { get; init; } = null!;
    public string FeatureName { get; init; } = null!;
    public string SourceEntityType { get; init; } = null!;
    public string SourceEntityId { get; init; } = null!;
    public string CreatedBy { get; init; } = null!;
    public decimal ReportedCostUsd { get; init; }
    public string? Notes { get; init; }
    public List<string> UsageLogIds { get; init; } = new();
    public Dictionary<string, object?> UsageSnapshot { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record AIVndChargeResult(
    string TransactionId,
    decimal ReportedCostUsd,
    decimal UsdToVndRate,
    decimal ChargeMultiplier,
    long MinimumBalanceVnd,
    long ActualCostVnd,
    long ChargedVnd,
    long ActualDeductedVnd,
    long AbsorbedVnd,
    long BalanceBeforeVnd,
    long BalanceAfterVnd);

public sealed class AIVndRefundRequest
{
    public string TransactionId { get; init; } = null!;
    public string RefundedBy { get; init; } = null!;
    public string RefundReason { get; init; } = null!;
}

public sealed record AIVndRefundResult(
    string TransactionId,
    string Status,
    long RefundedVnd,
    long BalanceAfterVnd);
