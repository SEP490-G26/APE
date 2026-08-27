using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Driver;

namespace BE_IntegrationTests.External;

public sealed class ControlledMentorBillingService : IAIVndBillingService
{
    private readonly DbContext _dbContext;

    public ControlledMentorBillingService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AIVndBillingConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.SystemSettings
            .Find(item => item.SettingName == "AI_VND_BILLING")
            .FirstOrDefaultAsync(cancellationToken);

        if (setting?.SettingData is not Dictionary<string, object?> data)
        {
            return new AIVndBillingConfigDto
            {
                SettingName = "AI_VND_BILLING",
                Version = "default",
                UsdToVndRate = 26000m,
                MinBalanceVnd = 1000,
                ChargeMultiplier = 1.5m
            };
        }

        return new AIVndBillingConfigDto
        {
            SettingName = "AI_VND_BILLING",
            Version = data.TryGetValue("version", out var version) ? version?.ToString() ?? "default" : "default",
            UsdToVndRate = data.TryGetValue("usdToVndRate", out var rate) ? Convert.ToDecimal(rate) : 26000m,
            MinBalanceVnd = data.TryGetValue("minBalanceVnd", out var minBalance) ? Convert.ToInt64(minBalance) : 1000,
            ChargeMultiplier = data.TryGetValue("chargeMultiplier", out var multiplier) ? Convert.ToDecimal(multiplier) : 1.5m,
            UpdatedBy = setting.UpdatedBy,
            LastUpdated = setting.LastUpdated
        };
    }

    public async Task<AIVndBillingConfigDto> UpsertConfigAsync(
        AIVndBillingConfigUpsertRequestDto request,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var version = $"v{DateTime.UtcNow:yyyyMMddHHmmss}";
        var setting = await _dbContext.SystemSettings
            .Find(item => item.SettingName == "AI_VND_BILLING")
            .FirstOrDefaultAsync(cancellationToken)
            ?? new SystemSetting
            {
                Id = "AI_VND_BILLING",
                SettingName = "AI_VND_BILLING"
            };

        setting.SettingData = new Dictionary<string, object?>
        {
            ["usdToVndRate"] = request.UsdToVndRate,
            ["minBalanceVnd"] = request.MinBalanceVnd,
            ["chargeMultiplier"] = request.ChargeMultiplier,
            ["version"] = version
        };
        setting.UpdatedBy = updatedBy;
        setting.LastUpdated = DateTime.UtcNow;

        var exists = await _dbContext.SystemSettings
            .Find(item => item.Id == setting.Id)
            .AnyAsync(cancellationToken);

        if (exists)
        {
            await _dbContext.SystemSettings.ReplaceOneAsync(
                item => item.Id == setting.Id,
                setting,
                cancellationToken: cancellationToken);
        }
        else
        {
            await _dbContext.SystemSettings.InsertOneAsync(setting, cancellationToken: cancellationToken);
        }

        return new AIVndBillingConfigDto
        {
            SettingName = setting.SettingName,
            Version = version,
            UsdToVndRate = request.UsdToVndRate,
            MinBalanceVnd = request.MinBalanceVnd,
            ChargeMultiplier = request.ChargeMultiplier,
            UpdatedBy = setting.UpdatedBy,
            LastUpdated = setting.LastUpdated
        };
    }

    public Task EnsureMinimumBalanceAsync(string userId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task<AIVndChargeResult> ChargeAsync(AIVndChargeRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = new AIVndBillingTransaction
        {
            UserId = request.UserId,
            FeatureKey = request.FeatureKey,
            SourceEntityType = request.SourceEntityType,
            SourceEntityId = request.SourceEntityId,
            Status = "charged",
            ReportedCostUsd = request.ReportedCostUsd,
            UsdToVndRate = 25000m,
            ChargeMultiplier = 1m,
            MinimumBalanceVnd = 0,
            ActualCostVnd = 250,
            ChargedVnd = 250,
            ActualDeductedVnd = 250,
            AbsorbedVnd = 0,
            BalanceBeforeVnd = 100000,
            BalanceAfterVnd = 99750,
            UsageLogIds = request.UsageLogIds,
            PolicySettingName = "controlled",
            PolicyVersion = "integration-test",
            PolicySnapshot = new { mode = "controlled" },
            UsageSnapshot = request.UsageSnapshot,
            ChargeBreakdown = new { mode = "controlled" },
            CreatedBy = request.CreatedBy,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContext.AIVndBillingTransactions.InsertOneAsync(transaction, cancellationToken: cancellationToken);

        return new AIVndChargeResult(
            transaction.Id,
            transaction.ReportedCostUsd,
            transaction.UsdToVndRate,
            transaction.ChargeMultiplier,
            transaction.MinimumBalanceVnd,
            transaction.ActualCostVnd,
            transaction.ChargedVnd,
            transaction.ActualDeductedVnd,
            transaction.AbsorbedVnd,
            transaction.BalanceBeforeVnd,
            transaction.BalanceAfterVnd);
    }

    public Task<AIVndChargeResult> RecordUsageAsync(AIVndChargeRequest request, CancellationToken cancellationToken = default)
        => ChargeAsync(request, cancellationToken);

    public async Task<AIVndRefundResult> RefundAsync(AIVndRefundRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.AIVndBillingTransactions
            .Find(item => item.Id == request.TransactionId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Billing transaction not found.");

        if (transaction.RefundedVnd <= 0)
        {
            var user = await _dbContext.Users
                .Find(item => item.Id == transaction.UserId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("User not found.");

            user.AiWalletBalanceVnd += transaction.ActualDeductedVnd;
            transaction.RefundedVnd = transaction.ActualDeductedVnd;
            transaction.BalanceAfterVnd = user.AiWalletBalanceVnd;
            transaction.Status = "refunded";
            transaction.RefundReason = request.RefundReason;
            transaction.UpdatedAt = DateTime.UtcNow;

            await _dbContext.Users.ReplaceOneAsync(item => item.Id == user.Id, user, cancellationToken: cancellationToken);
            await _dbContext.AIVndBillingTransactions.ReplaceOneAsync(
                item => item.Id == transaction.Id,
                transaction,
                cancellationToken: cancellationToken);
        }

        return new AIVndRefundResult(
            transaction.Id,
            transaction.Status,
            transaction.RefundedVnd,
            transaction.BalanceAfterVnd);
    }
}
