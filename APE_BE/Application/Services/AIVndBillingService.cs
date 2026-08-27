using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using MongoDB.Bson;

namespace Application.Services;

public class AIVndBillingService : IAIVndBillingService
{
    private const string SettingName = "AI_VND_BILLING";
    private const decimal DefaultUsdToVndRate = 26000m;
    private const long DefaultMinBalanceVnd = 1000L;
    private const decimal ChargeMultiplier = 1.5m;

    private readonly IUserRepository _userRepository;
    private readonly ISystemSettingRepository _systemSettingRepository;
    private readonly IAIVndBillingTransactionRepository _transactionRepository;

    public AIVndBillingService(
        IUserRepository userRepository,
        ISystemSettingRepository systemSettingRepository,
        IAIVndBillingTransactionRepository transactionRepository)
    {
        _userRepository = userRepository;
        _systemSettingRepository = systemSettingRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<AIVndBillingConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _systemSettingRepository.GetByNameAsync(SettingName, cancellationToken);
        var payload = ParseSettingData(setting?.SettingData);
        return new AIVndBillingConfigDto
        {
            SettingName = SettingName,
            Version = payload.Version,
            UsdToVndRate = payload.UsdToVndRate,
            MinBalanceVnd = payload.MinBalanceVnd,
            ChargeMultiplier = payload.ChargeMultiplier,
            UpdatedBy = setting?.UpdatedBy,
            LastUpdated = setting?.LastUpdated
        };
    }

    public async Task<AIVndBillingConfigDto> UpsertConfigAsync(AIVndBillingConfigUpsertRequestDto request, string updatedBy, CancellationToken cancellationToken = default)
    {
        if (request.UsdToVndRate <= 0)
        {
            throw new InvalidOperationException("UsdToVndRate must be greater than 0.");
        }

        if (request.MinBalanceVnd < 0)
        {
            throw new InvalidOperationException("MinBalanceVnd must not be negative.");
        }

        if (request.ChargeMultiplier <= 0)
        {
            throw new InvalidOperationException("ChargeMultiplier must be greater than 0.");
        }

        var setting = await _systemSettingRepository.GetByNameAsync(SettingName, cancellationToken)
            ?? new SystemSetting
            {
                Id = SettingName,
                SettingName = SettingName
            };

        var version = $"v{DateTime.UtcNow:yyyyMMddHHmmss}";
        setting.SettingData = new Dictionary<string, object?>
        {
            ["usdToVndRate"] = request.UsdToVndRate,
            ["minBalanceVnd"] = request.MinBalanceVnd,
            ["chargeMultiplier"] = request.ChargeMultiplier,
            ["version"] = version
        };
        setting.UpdatedBy = updatedBy;
        setting.LastUpdated = DateTime.UtcNow;

        await _systemSettingRepository.UpsertAsync(setting, cancellationToken);

        return new AIVndBillingConfigDto
        {
            SettingName = SettingName,
            Version = version,
            UsdToVndRate = request.UsdToVndRate,
            MinBalanceVnd = request.MinBalanceVnd,
            ChargeMultiplier = request.ChargeMultiplier,
            UpdatedBy = updatedBy,
            LastUpdated = setting.LastUpdated
        };
    }

    public async Task EnsureMinimumBalanceAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId) ?? throw new KeyNotFoundException("User not found.");

        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var config = await GetConfigAsync(cancellationToken);

        if (user.AiWalletBalanceVnd < config.MinBalanceVnd)
        {
            throw new InvalidOperationException($"Insufficient AI wallet balance. At least {config.MinBalanceVnd:N0} VND is required to use this feature.");
        }
    }

    public async Task<AIVndChargeResult> ChargeAsync(AIVndChargeRequest request, CancellationToken cancellationToken = default)
        => await CreateChargeAsync(request, applyWalletDeduction: true, trackActualUsageOnly: false, cancellationToken);

    public async Task<AIVndChargeResult> RecordUsageAsync(AIVndChargeRequest request, CancellationToken cancellationToken = default)
        => await CreateChargeAsync(request, applyWalletDeduction: false, trackActualUsageOnly: true, cancellationToken);

    private async Task<AIVndChargeResult> CreateChargeAsync(
        AIVndChargeRequest request,
        bool applyWalletDeduction,
        bool trackActualUsageOnly,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId) ?? throw new KeyNotFoundException("User not found.");
        var config = await GetConfigAsync(cancellationToken);
        var isAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        var shouldTrackActualUsageOnly = trackActualUsageOnly || isAdmin;
        var shouldApplyWalletDeduction = applyWalletDeduction && !isAdmin;

        var reportedCostUsd = request.ReportedCostUsd < 0 ? 0 : request.ReportedCostUsd;
        var actualCostVnd = reportedCostUsd <= 0
            ? 0L
            : (long)Math.Ceiling(reportedCostUsd * config.UsdToVndRate);
        var effectiveMultiplier = shouldTrackActualUsageOnly ? 1m : config.ChargeMultiplier;
        var billableCostUsd = reportedCostUsd <= 0
            ? 0m
            : decimal.Round(reportedCostUsd * effectiveMultiplier, 8, MidpointRounding.AwayFromZero);
        var chargedVnd = billableCostUsd <= 0
            ? 0L
            : (long)Math.Ceiling(billableCostUsd * config.UsdToVndRate);
        var balanceBefore = user.AiWalletBalanceVnd;
        var actualDeducted = shouldApplyWalletDeduction ? Math.Min(balanceBefore, chargedVnd) : 0L;
        var absorbed = shouldApplyWalletDeduction ? Math.Max(0L, chargedVnd - actualDeducted) : 0L;
        var balanceAfter = shouldApplyWalletDeduction
            ? Math.Max(0L, balanceBefore - actualDeducted)
            : balanceBefore;
        var status = chargedVnd == 0
            ? "waived"
            : shouldTrackActualUsageOnly
                ? "recorded"
                : actualDeducted > 0
                    ? "charged"
                    : "absorbed";

        if (shouldApplyWalletDeduction)
        {
            user.AiWalletBalanceVnd = balanceAfter;
            await _userRepository.UpdateAsync(user);
        }

        var transaction = new AIVndBillingTransaction
        {
            UserId = request.UserId,
            FeatureKey = request.FeatureKey,
            SourceEntityType = request.SourceEntityType,
            SourceEntityId = request.SourceEntityId,
            Status = status,
            ReportedCostUsd = reportedCostUsd,
            UsdToVndRate = config.UsdToVndRate,
            ChargeMultiplier = effectiveMultiplier,
            MinimumBalanceVnd = config.MinBalanceVnd,
            ActualCostVnd = actualCostVnd,
            ChargedVnd = chargedVnd,
            ActualDeductedVnd = actualDeducted,
            AbsorbedVnd = absorbed,
            RefundedVnd = 0,
            BalanceBeforeVnd = balanceBefore,
            BalanceAfterVnd = balanceAfter,
            UsageLogIds = request.UsageLogIds,
            PolicySettingName = config.SettingName,
            PolicyVersion = config.Version,
            PolicySnapshot = new Dictionary<string, object?>
            {
                ["usd_to_vnd_rate"] = config.UsdToVndRate,
                ["min_balance_vnd"] = config.MinBalanceVnd,
                ["charge_multiplier"] = effectiveMultiplier
            },
            UsageSnapshot = request.UsageSnapshot,
            ChargeBreakdown = new Dictionary<string, object?>
            {
                ["reported_cost_usd"] = reportedCostUsd,
                ["actual_cost_vnd"] = actualCostVnd,
                ["billable_cost_usd"] = billableCostUsd,
                ["charge_multiplier"] = effectiveMultiplier,
                ["charged_vnd"] = chargedVnd,
                ["actual_deducted_vnd"] = actualDeducted,
                ["absorbed_vnd"] = absorbed
            },
            CreatedBy = request.CreatedBy,
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? chargedVnd == 0
                    ? "No charge was applied because the provider did not return a valid USD cost."
                    : shouldTrackActualUsageOnly
                        ? "Only the actual AI usage cost was recorded for the system. No wallet deduction was applied."
                        : $"The charged amount was calculated from reportedCostUsd x {effectiveMultiplier:0.##}."
                : request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _transactionRepository.CreateAsync(transaction, cancellationToken);

        return new AIVndChargeResult(
            transaction.Id,
            reportedCostUsd,
            config.UsdToVndRate,
            effectiveMultiplier,
            config.MinBalanceVnd,
            actualCostVnd,
            chargedVnd,
            actualDeducted,
            absorbed,
            balanceBefore,
            balanceAfter);
    }

    public async Task<AIVndRefundResult> RefundAsync(AIVndRefundRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await _transactionRepository.GetByIdAsync(request.TransactionId, cancellationToken)
            ?? throw new KeyNotFoundException("Billing transaction not found.");

        if (transaction.RefundedVnd > 0)
        {
            return new AIVndRefundResult(transaction.Id, transaction.Status, transaction.RefundedVnd, transaction.BalanceAfterVnd);
        }

        var user = await _userRepository.GetByIdAsync(transaction.UserId) ?? throw new KeyNotFoundException("User not found.");
        user.AiWalletBalanceVnd += transaction.ActualDeductedVnd;
        await _userRepository.UpdateAsync(user);

        transaction.RefundedVnd = transaction.ActualDeductedVnd;
        transaction.BalanceAfterVnd = user.AiWalletBalanceVnd;
        transaction.Status = "refunded";
        transaction.RefundReason = request.RefundReason;
        transaction.UpdatedAt = DateTime.UtcNow;
        await _transactionRepository.UpdateAsync(transaction, cancellationToken);

        return new AIVndRefundResult(transaction.Id, transaction.Status, transaction.RefundedVnd, transaction.BalanceAfterVnd);
    }

    private static BillingConfigPayload ParseSettingData(object? settingData)
    {
        if (settingData is Dictionary<string, object?> dictionary)
        {
            return FromDictionary(dictionary);
        }

        if (settingData is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Object
                ? new BillingConfigPayload(
                    ReadDecimal(element, "usdToVndRate", DefaultUsdToVndRate),
                    ReadInt64(element, "minBalanceVnd", DefaultMinBalanceVnd),
                    ReadDecimal(element, "chargeMultiplier", ChargeMultiplier),
                    ReadString(element, "version") ?? "default")
                : new BillingConfigPayload(DefaultUsdToVndRate, DefaultMinBalanceVnd, ChargeMultiplier, "default");
        }

        if (settingData is BsonDocument bsonDocument)
        {
            var version = bsonDocument.TryGetValue("version", out var versionValue) && versionValue.BsonType != BsonType.Null
                ? versionValue.ToString() ?? "default"
                : "default";

            return new BillingConfigPayload(
                bsonDocument.TryGetValue("usdToVndRate", out var rateValue) ? rateValue.ToDecimal() : DefaultUsdToVndRate,
                bsonDocument.TryGetValue("minBalanceVnd", out var minValue) ? minValue.ToInt64() : DefaultMinBalanceVnd,
                bsonDocument.TryGetValue("chargeMultiplier", out var multiplierValue) ? multiplierValue.ToDecimal() : ChargeMultiplier,
                version);
        }

        return new BillingConfigPayload(DefaultUsdToVndRate, DefaultMinBalanceVnd, ChargeMultiplier, "default");
    }

    private static BillingConfigPayload FromDictionary(IDictionary<string, object?> dictionary)
    {
        var rate = ReadDecimal(dictionary, "usdToVndRate", DefaultUsdToVndRate);
        var minBalance = ReadInt64(dictionary, "minBalanceVnd", DefaultMinBalanceVnd);
        var chargeMultiplier = ReadDecimal(dictionary, "chargeMultiplier", ChargeMultiplier);
        var version = dictionary.TryGetValue("version", out var versionObj) ? versionObj?.ToString() : null;
        return new BillingConfigPayload(rate, minBalance, chargeMultiplier, version ?? "default");
    }

    private static decimal ReadDecimal(IDictionary<string, object?> source, string key, decimal fallback)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return fallback;
        }

        return decimal.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
    }

    private static long ReadInt64(IDictionary<string, object?> source, string key, long fallback)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return fallback;
        }

        return long.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
    }

    private static decimal ReadDecimal(JsonElement element, string propertyName, decimal fallback)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetDecimal(out var value)
            ? value
            : fallback;
    }

    private static long ReadInt64(JsonElement element, string propertyName, long fallback)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var value)
            ? value
            : fallback;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private sealed record BillingConfigPayload(decimal UsdToVndRate, long MinBalanceVnd, decimal ChargeMultiplier, string Version);
}
