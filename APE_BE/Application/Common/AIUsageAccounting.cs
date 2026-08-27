namespace Application.Common;

public static class AIUsageAccounting
{
    public static AIEmbeddingUsageSnapshot ForEmbedding(string provider, string configuredModel, string? effectiveModel, int? tokensUsed, decimal? reportedCostUsd, string? usageSource, string? costSource)
    {
        var normalizedProvider = NormalizeProvider(provider);
        var resolvedModel = string.IsNullOrWhiteSpace(effectiveModel) ? configuredModel : effectiveModel;
        var safeTokens = Math.Max(0, tokensUsed ?? 0);
        var normalizedUsageSource = !string.IsNullOrWhiteSpace(usageSource)
            ? usageSource
            : tokensUsed.HasValue
                ? "provider_response"
                : "missing_from_provider_response";
        var normalizedCostSource = NormalizeCostSource(costSource, reportedCostUsd, normalizedUsageSource);
        var normalizedCostUsd = reportedCostUsd ?? EstimateEmbeddingCostUsd(normalizedProvider, resolvedModel, safeTokens);

        return new AIEmbeddingUsageSnapshot(
            normalizedProvider,
            configuredModel,
            resolvedModel,
            NormalizeModelFamily(resolvedModel),
            NormalizeModelKey(normalizedProvider, resolvedModel),
            safeTokens,
            normalizedUsageSource,
            normalizedCostUsd,
            normalizedCostSource);
    }

    public static AITextUsageSnapshot ForText(string provider, string configuredModel, string? effectiveModel, int? inputTokens, int? outputTokens, int? totalTokens, decimal? reportedCostUsd, string? usageSource, string? costSource)
    {
        var normalizedProvider = NormalizeProvider(provider);
        var resolvedModel = string.IsNullOrWhiteSpace(effectiveModel) ? configuredModel : effectiveModel;
        var safeInput = Math.Max(0, inputTokens ?? 0);
        var safeOutput = Math.Max(0, outputTokens ?? 0);
        var safeTotal = Math.Max(0, totalTokens ?? (safeInput + safeOutput));
        var normalizedUsageSource = NormalizeUsageSource(usageSource, inputTokens, outputTokens, totalTokens);
        var normalizedCostSource = NormalizeCostSource(costSource, reportedCostUsd, normalizedUsageSource);
        var normalizedCostUsd = reportedCostUsd ?? EstimateTextCostUsd(normalizedProvider, resolvedModel, safeInput, safeOutput);

        return new AITextUsageSnapshot(
            normalizedProvider,
            configuredModel,
            resolvedModel,
            NormalizeModelFamily(resolvedModel),
            NormalizeModelKey(normalizedProvider, resolvedModel),
            safeInput,
            safeOutput,
            safeTotal,
            normalizedUsageSource,
            normalizedCostUsd,
            normalizedCostSource);
    }

    public static string NormalizeProvider(string? provider)
    {
        return string.IsNullOrWhiteSpace(provider) ? "Unknown" : provider.Trim();
    }

    public static string NormalizeModelFamily(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return "unknown";
        }

        var normalized = model.Trim().ToLowerInvariant();
        if (normalized.StartsWith("gpt-5.4-mini")) return "gpt-5.4";
        if (normalized.StartsWith("gpt-5.4")) return "gpt-5.4";
        if (normalized.StartsWith("gpt-4o-mini")) return "gpt-4o";
        if (normalized.StartsWith("gpt-4o")) return "gpt-4o";
        if (normalized.StartsWith("gemini-3.5-flash")) return "gemini-3.5-flash";
        if (normalized.StartsWith("gemini-3.1-flash-lite")) return "gemini-3.1-flash-lite";
        if (normalized.StartsWith("gemini-3.1-flash")) return "gemini-3.1-flash";
        if (normalized.StartsWith("gemini-2.5-flash")) return "gemini-2.5-flash";
        if (normalized.StartsWith("command-r7b")) return "command-r7b";
        if (normalized.StartsWith("embed-multilingual-v3")) return "embed-multilingual-v3";

        var parts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : normalized;
    }

    public static string NormalizeModelKey(string provider, string? model)
    {
        return $"{NormalizeProvider(provider)}:{(string.IsNullOrWhiteSpace(model) ? "unknown" : model.Trim())}";
    }

    public static string NormalizeUsageSource(string? usageSource, int? inputTokens, int? outputTokens, int? totalTokens)
    {
        if (!string.IsNullOrWhiteSpace(usageSource))
        {
            return usageSource;
        }

        return (inputTokens.HasValue || outputTokens.HasValue || totalTokens.HasValue)
            ? "provider_response"
            : "missing_from_provider_response";
    }

    public static string NormalizeCostSource(string? costSource, decimal? reportedCostUsd, string usageSource)
    {
        if (!string.IsNullOrWhiteSpace(costSource) && !string.Equals(costSource, "not_available", StringComparison.OrdinalIgnoreCase))
        {
            return costSource;
        }

        if (reportedCostUsd.HasValue)
        {
            return "provider_response";
        }

        return string.Equals(usageSource, "provider_response", StringComparison.OrdinalIgnoreCase)
            ? "estimated_from_provider_usage"
            : "estimated_catalog";
    }

    public static decimal EstimateTextCostUsd(string provider, string? model, int inputTokens, int outputTokens)
    {
        var normalizedProvider = NormalizeProvider(provider);
        var effectiveModel = string.IsNullOrWhiteSpace(model) ? "*" : model.Trim();

        if (!TextPricingCatalog.TryGetValue($"{normalizedProvider}:{effectiveModel}", out var price) &&
            !TextPricingCatalog.TryGetValue($"{normalizedProvider}:*", out price))
        {
            return 0m;
        }

        var inputCost = (inputTokens / 1_000_000m) * price.InputPer1MUsd;
        var outputCost = (outputTokens / 1_000_000m) * price.OutputPer1MUsd;
        return decimal.Round(inputCost + outputCost, 8, MidpointRounding.AwayFromZero);
    }

    public static decimal EstimateEmbeddingCostUsd(string provider, string? model, int tokensUsed)
    {
        var normalizedProvider = NormalizeProvider(provider);
        var effectiveModel = string.IsNullOrWhiteSpace(model) ? "*" : model.Trim();

        if (!EmbeddingPricingCatalog.TryGetValue($"{normalizedProvider}:{effectiveModel}", out var price) &&
            !EmbeddingPricingCatalog.TryGetValue($"{normalizedProvider}:*", out price))
        {
            return 0m;
        }

        return decimal.Round((tokensUsed / 1_000_000m) * price.InputPer1MUsd, 8, MidpointRounding.AwayFromZero);
    }

    private static readonly Dictionary<string, TextPricePerMillion> TextPricingCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OpenAI:gpt-5.4-mini"] = new(0.25m, 2.00m),
        ["OpenAI:gpt-5.4"] = new(1.25m, 10.00m),
        ["OpenAI:gpt-4.1-mini"] = new(0.40m, 1.60m),
        ["OpenAI:*"] = new(0.40m, 1.60m),
        ["Gemini:gemini-3.5-flash"] = new(0.30m, 2.50m),
        ["Gemini:gemini-3-flash-preview"] = new(0.30m, 2.50m),
        ["Gemini:gemini-3.1-flash-lite"] = new(0.10m, 0.40m),
        ["Gemini:*"] = new(0.30m, 2.50m),
        ["Cohere:command-r7b-12-2024"] = new(0.15m, 0.60m),
        ["Cohere:*"] = new(0.15m, 0.60m)
    };

    private static readonly Dictionary<string, EmbeddingPricePerMillion> EmbeddingPricingCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Cohere:embed-multilingual-v3.0"] = new(0.10m),
        ["Cohere:embed-multilingual-v3"] = new(0.10m),
        ["Cohere:*"] = new(0.10m),
        ["OpenAI:text-embedding-3-small"] = new(0.02m),
        ["OpenAI:text-embedding-3-large"] = new(0.13m),
        ["OpenAI:*"] = new(0.02m)
    };

    private readonly record struct TextPricePerMillion(decimal InputPer1MUsd, decimal OutputPer1MUsd);
    private readonly record struct EmbeddingPricePerMillion(decimal InputPer1MUsd);
}

public sealed record AITextUsageSnapshot(
    string Provider,
    string ConfiguredModel,
    string EffectiveModel,
    string ModelFamily,
    string NormalizedModelKey,
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    string UsageSource,
    decimal CostUsd,
    string CostSource);

public sealed record AIEmbeddingUsageSnapshot(
    string Provider,
    string ConfiguredModel,
    string EffectiveModel,
    string ModelFamily,
    string NormalizedModelKey,
    int TokensUsed,
    string UsageSource,
    decimal CostUsd,
    string CostSource);
