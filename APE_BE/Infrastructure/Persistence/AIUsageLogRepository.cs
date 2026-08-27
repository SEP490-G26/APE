using Application.Interfaces;
using Application.DTOs;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class AIUsageLogRepository : IAIUsageLogRepository
{
    private readonly IMongoCollection<AIUsageLog> _collection;

    public AIUsageLogRepository(DbContext context)
    {
        _collection = context.APIUsageLogs;
    }

    public async Task CreateAsync(AIUsageLog log)
    {
        await _collection.InsertOneAsync(log);
    }

    public async Task<(List<AIUsageLog> Items, long Total)> ListAsync(
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
        int limit)
    {
        var filter = Builders<AIUsageLog>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(triggeredBy))
            filter &= ObjectId.TryParse(triggeredBy, out _)
                ? Builders<AIUsageLog>.Filter.Eq(item => item.TriggeredBy, triggeredBy)
                : Builders<AIUsageLog>.Filter.Regex(item => item.TriggeredBy, new BsonRegularExpression(triggeredBy, "i"));
        if (!string.IsNullOrWhiteSpace(agentId))
            filter &= ObjectId.TryParse(agentId, out _)
                ? Builders<AIUsageLog>.Filter.Eq(item => item.AgentId, agentId)
                : Builders<AIUsageLog>.Filter.Regex(item => item.AgentId, new BsonRegularExpression(agentId, "i"));
        if (fromDate.HasValue)
            filter &= Builders<AIUsageLog>.Filter.Gte(item => item.CreatedAt, fromDate.Value);
        if (toDate.HasValue)
            filter &= Builders<AIUsageLog>.Filter.Lte(item => item.CreatedAt, toDate.Value);

        page = Math.Max(1, page);
        limit = Math.Max(1, limit);
        var allItems = await _collection.Find(filter)
            .SortByDescending(item => item.CreatedAt)
            .ToListAsync();

        var filteredItems = allItems
            .Where(item => MatchesPayloadFilters(item, provider, model, feature, step, fallbackUsed))
            .ToList();

        var total = filteredItems.Count;
        var skip = (page - 1) * limit;
        var items = filteredItems
            .Skip(skip)
            .Take(limit)
            .ToList();

        return (items, total);
    }

    public async Task<(List<string> TriggeredByIds, List<string> Features, List<string> Providers, List<string> Models)> ListFilterOptionsAsync(
        DateTime? fromDate,
        DateTime? toDate)
    {
        var filter = Builders<AIUsageLog>.Filter.Empty;
        if (fromDate.HasValue)
            filter &= Builders<AIUsageLog>.Filter.Gte(item => item.CreatedAt, fromDate.Value);
        if (toDate.HasValue)
            filter &= Builders<AIUsageLog>.Filter.Lte(item => item.CreatedAt, toDate.Value);

        var triggeredByIds = await _collection
            .Distinct<string>(nameof(AIUsageLog.TriggeredBy), filter)
            .ToListAsync();

        var payloads = await _collection
            .Find(filter)
            .Project(item => item.PayloadData)
            .ToListAsync();

        var features = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var payloadData in payloads)
        {
            var payload = ToDictionary(payloadData);

            AddIfPresent(features, ReadString(payload, "feature"), ReadString(payload, "Feature"));
            AddIfPresent(providers, ReadString(payload, "provider"), ReadString(payload, "Provider"));
            AddIfPresent(
                models,
                ReadString(payload, "effective_model"),
                ReadString(payload, "effectiveModel"),
                ReadString(payload, "model"),
                ReadString(payload, "Model"),
                ReadString(payload, "normalized_model_key"),
                ReadString(payload, "normalizedModelKey"),
                ReadString(payload, "NormalizedModelKey"));
        }

        return (
            triggeredByIds
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            features
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            providers
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            models
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static void AddIfPresent(ISet<string> target, params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                target.Add(candidate.Trim());
                return;
            }
        }
    }

    private static IDictionary<string, object?> ToDictionary(object? payload)
    {
        if (payload is IDictionary<string, object?> dictionary)
        {
            return new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase);
        }

        if (payload is BsonDocument bsonDocument)
        {
            return bsonDocument.Elements.ToDictionary(
                item => item.Name,
                item => ConvertBsonValue(item.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        if (payload is IDictionary<string, object> plainDictionary)
        {
            return plainDictionary.ToDictionary(
                item => item.Key,
                item => (object?)item.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static object? ConvertBsonValue(BsonValue value)
    {
        if (value.IsBsonNull)
        {
            return null;
        }

        return value.BsonType switch
        {
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Double => value.AsDouble,
            BsonType.Decimal128 => (decimal)value.AsDecimal128,
            BsonType.Boolean => value.AsBoolean,
            BsonType.String => value.AsString,
            BsonType.Document => value.AsBsonDocument.Elements.ToDictionary(
                item => item.Name,
                item => ConvertBsonValue(item.Value),
                StringComparer.OrdinalIgnoreCase),
            BsonType.Array => value.AsBsonArray.Select(ConvertBsonValue).ToList(),
            _ => value.ToString()
        };
    }

    private static string? ReadString(IDictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static bool? ReadBool(IDictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        return bool.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static bool MatchesPayloadFilters(
        AIUsageLog item,
        string? provider,
        string? model,
        string? feature,
        string? step,
        bool? fallbackUsed)
    {
        var payload = ToDictionary(item.PayloadData);

        if (!string.IsNullOrWhiteSpace(provider))
        {
            var payloadProvider = FirstNonEmpty(
                ReadString(payload, "provider"),
                ReadString(payload, "Provider"));
            if (!string.Equals(payloadProvider, provider, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            var payloadModel = FirstNonEmpty(
                ReadString(payload, "effective_model"),
                ReadString(payload, "effectiveModel"),
                ReadString(payload, "model"),
                ReadString(payload, "Model"),
                ReadString(payload, "normalized_model_key"),
                ReadString(payload, "normalizedModelKey"),
                ReadString(payload, "NormalizedModelKey"));
            if (!string.Equals(payloadModel, model, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(feature))
        {
            var payloadFeature = FirstNonEmpty(
                ReadString(payload, "feature"),
                ReadString(payload, "Feature"));
            if (!string.Equals(payloadFeature, feature, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(step))
        {
            var payloadStep = FirstNonEmpty(
                ReadString(payload, "step"),
                ReadString(payload, "Step"));
            if (!string.Equals(payloadStep, step, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (fallbackUsed.HasValue)
        {
            var payloadFallbackUsed = ReadBool(payload, "fallback_used")
                ?? ReadBool(payload, "fallbackUsed")
                ?? ReadBool(payload, "FallbackUsed");
            if (payloadFallbackUsed != fallbackUsed.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    public async Task<List<AIUsageSummaryItemDto>> SummarizeAsync(
        string? agentId,
        string? provider,
        string? model,
        string? feature,
        string? step,
        DateTime? fromDate,
        DateTime? toDate,
        int take)
    {
        take = Math.Clamp(take, 1, 200);

        var filter = Builders<AIUsageLog>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(agentId))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq(item => item.AgentId, agentId);
        }

        if (fromDate.HasValue)
        {
            filter &= Builders<AIUsageLog>.Filter.Gte(item => item.CreatedAt, fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filter &= Builders<AIUsageLog>.Filter.Lte(item => item.CreatedAt, toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.provider", provider);
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            filter &= Builders<AIUsageLog>.Filter.Or(
                Builders<AIUsageLog>.Filter.Eq("PayloadData.model", model),
                Builders<AIUsageLog>.Filter.Eq("PayloadData.effective_model", model),
                Builders<AIUsageLog>.Filter.Eq("PayloadData.normalized_model_key", model));
        }

        if (!string.IsNullOrWhiteSpace(feature))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.feature", feature);
        }

        if (!string.IsNullOrWhiteSpace(step))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.step", step);
        }

        var pipeline = await _collection.Aggregate()
            .Match(filter)
            .Group(new BsonDocument
            {
                { "_id", new BsonDocument
                    {
                        { "agent_id", "$AgentId" },
                        { "provider", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.provider", string.Empty }) },
                        { "model", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.effective_model", "$PayloadData.model", string.Empty }) },
                        { "normalized_model_key", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.normalized_model_key", string.Empty }) },
                        { "feature", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.feature", string.Empty }) },
                        { "step", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.step", string.Empty }) }
                    }
                },
                { "call_count", new BsonDocument("$sum", 1) },
                { "total_tokens", new BsonDocument("$sum", "$TokensUsed") },
                { "total_cost_usd", new BsonDocument("$sum", "$CostUsd") },
                { "total_credits_deducted", new BsonDocument("$sum", "$CreditsDeducted") },
                { "first_used_at", new BsonDocument("$min", "$CreatedAt") },
                { "last_used_at", new BsonDocument("$max", "$CreatedAt") },
                { "distinct_triggered_users", new BsonDocument("$addToSet", "$TriggeredBy") },
                { "fallback_call_count", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$PayloadData.fallback_used", true }),
                        1,
                        0
                    }))
                }
            })
            .Sort(new BsonDocument
            {
                { "last_used_at", -1 },
                { "call_count", -1 }
            })
            .Limit(take)
            .ToListAsync();

        return pipeline.Select(document => new AIUsageSummaryItemDto
        {
            AgentId = ReadString(document, "_id", "agent_id") ?? string.Empty,
            Provider = ReadString(document, "_id", "provider") ?? string.Empty,
            Model = ReadString(document, "_id", "model") ?? string.Empty,
            NormalizedModelKey = ReadString(document, "_id", "normalized_model_key") ?? string.Empty,
            Feature = NormalizeEmpty(ReadString(document, "_id", "feature")),
            Step = NormalizeEmpty(ReadString(document, "_id", "step")),
            CallCount = ReadInt32(document, "call_count"),
            DistinctTriggeredUsers = ReadArrayCount(document, "distinct_triggered_users"),
            TotalTokens = ReadInt32(document, "total_tokens"),
            TotalCostUsd = ReadDecimal(document, "total_cost_usd"),
            TotalCreditsDeducted = ReadDouble(document, "total_credits_deducted"),
            FallbackCallCount = ReadInt32(document, "fallback_call_count"),
            FirstUsedAt = ReadDateTime(document, "first_used_at"),
            LastUsedAt = ReadDateTime(document, "last_used_at")
        }).ToList();
    }

    public async Task<List<AIArtifactUsageSummaryItemDto>> SummarizeByArtifactsAsync(
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
        int take)
    {
        take = Math.Clamp(take, 1, 200);

        var filter = Builders<AIUsageLog>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(agentId))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq(item => item.AgentId, agentId);
        }

        if (!string.IsNullOrWhiteSpace(feature))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.feature", feature);
        }

        if (!string.IsNullOrWhiteSpace(step))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.step", step);
        }

        if (!string.IsNullOrWhiteSpace(promptKey))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.prompt_key", promptKey);
        }

        if (!string.IsNullOrWhiteSpace(promptVersion))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.prompt_version", promptVersion);
        }

        if (!string.IsNullOrWhiteSpace(policyKey))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.policy_key", policyKey);
        }

        if (!string.IsNullOrWhiteSpace(policyVersion))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.policy_version", policyVersion);
        }

        if (!string.IsNullOrWhiteSpace(rubricKey))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.rubric_key", rubricKey);
        }

        if (!string.IsNullOrWhiteSpace(rubricVersion))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.rubric_version", rubricVersion);
        }

        if (fromDate.HasValue)
        {
            filter &= Builders<AIUsageLog>.Filter.Gte(item => item.CreatedAt, fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filter &= Builders<AIUsageLog>.Filter.Lte(item => item.CreatedAt, toDate.Value);
        }

        var pipeline = await _collection.Aggregate()
            .Match(filter)
            .Group(new BsonDocument
            {
                { "_id", new BsonDocument
                    {
                        { "agent_id", "$AgentId" },
                        { "provider", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.provider", string.Empty }) },
                        { "model", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.effective_model", "$PayloadData.model", string.Empty }) },
                        { "feature", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.feature", string.Empty }) },
                        { "step", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.step", string.Empty }) },
                        { "prompt_key", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.prompt_key", string.Empty }) },
                        { "prompt_version", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.prompt_version", string.Empty }) },
                        { "policy_key", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.policy_key", string.Empty }) },
                        { "policy_version", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.policy_version", string.Empty }) },
                        { "rubric_key", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.rubric_key", string.Empty }) },
                        { "rubric_version", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.rubric_version", string.Empty }) }
                    }
                },
                { "call_count", new BsonDocument("$sum", 1) },
                { "total_tokens", new BsonDocument("$sum", "$TokensUsed") },
                { "total_cost_usd", new BsonDocument("$sum", "$CostUsd") },
                { "first_used_at", new BsonDocument("$min", "$CreatedAt") },
                { "last_used_at", new BsonDocument("$max", "$CreatedAt") },
                { "fallback_call_count", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$PayloadData.fallback_used", true }),
                        1,
                        0
                    }))
                }
            })
            .Sort(new BsonDocument
            {
                { "last_used_at", -1 },
                { "call_count", -1 }
            })
            .Limit(take)
            .ToListAsync();

        return pipeline.Select(document => new AIArtifactUsageSummaryItemDto
        {
            AgentId = ReadString(document, "_id", "agent_id") ?? string.Empty,
            Provider = ReadString(document, "_id", "provider") ?? string.Empty,
            Model = ReadString(document, "_id", "model") ?? string.Empty,
            Feature = NormalizeEmpty(ReadString(document, "_id", "feature")),
            Step = NormalizeEmpty(ReadString(document, "_id", "step")),
            PromptKey = NormalizeEmpty(ReadString(document, "_id", "prompt_key")),
            PromptVersion = NormalizeEmpty(ReadString(document, "_id", "prompt_version")),
            PolicyKey = NormalizeEmpty(ReadString(document, "_id", "policy_key")),
            PolicyVersion = NormalizeEmpty(ReadString(document, "_id", "policy_version")),
            RubricKey = NormalizeEmpty(ReadString(document, "_id", "rubric_key")),
            RubricVersion = NormalizeEmpty(ReadString(document, "_id", "rubric_version")),
            CallCount = ReadInt32(document, "call_count"),
            TotalTokens = ReadInt32(document, "total_tokens"),
            TotalCostUsd = ReadDecimal(document, "total_cost_usd"),
            FallbackCallCount = ReadInt32(document, "fallback_call_count"),
            FirstUsedAt = ReadDateTime(document, "first_used_at"),
            LastUsedAt = ReadDateTime(document, "last_used_at")
        }).ToList();
    }

    public async Task<List<AIUserUsageSummaryItemDto>> SummarizeByUsersAsync(
        string? triggeredBy,
        string? provider,
        string? model,
        string? feature,
        string? step,
        DateTime? fromDate,
        DateTime? toDate,
        int take)
    {
        take = Math.Clamp(take, 1, 500);

        var filter = Builders<AIUsageLog>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(triggeredBy))
        {
            filter &= Builders<AIUsageLog>.Filter.Regex(item => item.TriggeredBy, new BsonRegularExpression(triggeredBy, "i"));
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.provider", provider);
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            filter &= Builders<AIUsageLog>.Filter.Or(
                Builders<AIUsageLog>.Filter.Eq("PayloadData.model", model),
                Builders<AIUsageLog>.Filter.Eq("PayloadData.effective_model", model),
                Builders<AIUsageLog>.Filter.Eq("PayloadData.normalized_model_key", model));
        }

        if (!string.IsNullOrWhiteSpace(feature))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.feature", feature);
        }

        if (!string.IsNullOrWhiteSpace(step))
        {
            filter &= Builders<AIUsageLog>.Filter.Eq("PayloadData.step", step);
        }

        if (fromDate.HasValue)
        {
            filter &= Builders<AIUsageLog>.Filter.Gte(item => item.CreatedAt, fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filter &= Builders<AIUsageLog>.Filter.Lte(item => item.CreatedAt, toDate.Value);
        }

        var pipeline = await _collection.Aggregate()
            .Match(filter)
            .Group(new BsonDocument
            {
                { "_id", new BsonDocument
                    {
                        { "triggered_by", "$TriggeredBy" },
                        { "provider", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.provider", string.Empty }) },
                        { "model", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.effective_model", "$PayloadData.model", string.Empty }) },
                        { "normalized_model_key", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.normalized_model_key", string.Empty }) },
                        { "feature", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.feature", string.Empty }) },
                        { "step", new BsonDocument("$ifNull", new BsonArray { "$PayloadData.step", string.Empty }) }
                    }
                },
                { "call_count", new BsonDocument("$sum", 1) },
                { "total_tokens", new BsonDocument("$sum", "$TokensUsed") },
                { "total_cost_usd", new BsonDocument("$sum", "$CostUsd") },
                { "total_credits_deducted", new BsonDocument("$sum", "$CreditsDeducted") },
                { "first_used_at", new BsonDocument("$min", "$CreatedAt") },
                { "last_used_at", new BsonDocument("$max", "$CreatedAt") },
                { "fallback_call_count", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$PayloadData.fallback_used", true }),
                        1,
                        0
                    }))
                }
            })
            .Sort(new BsonDocument
            {
                { "total_cost_usd", -1 },
                { "call_count", -1 },
                { "last_used_at", -1 }
            })
            .Limit(take)
            .ToListAsync();

        return pipeline.Select(document =>
        {
            var callCount = ReadInt32(document, "call_count");
            var totalCostUsd = ReadDecimal(document, "total_cost_usd");
            var totalTokens = ReadInt32(document, "total_tokens");
            var fallbackCallCount = ReadInt32(document, "fallback_call_count");

            return new AIUserUsageSummaryItemDto
            {
                TriggeredBy = ReadString(document, "_id", "triggered_by") ?? string.Empty,
                Provider = ReadString(document, "_id", "provider") ?? string.Empty,
                Model = ReadString(document, "_id", "model") ?? string.Empty,
                NormalizedModelKey = ReadString(document, "_id", "normalized_model_key") ?? string.Empty,
                Feature = NormalizeEmpty(ReadString(document, "_id", "feature")),
                Step = NormalizeEmpty(ReadString(document, "_id", "step")),
                CallCount = callCount,
                TotalTokens = totalTokens,
                TotalCostUsd = totalCostUsd,
                TotalCreditsDeducted = ReadDouble(document, "total_credits_deducted"),
                FallbackCallCount = fallbackCallCount,
                AvgTokensPerCall = callCount <= 0 ? 0 : Math.Round(totalTokens / (decimal)callCount, 2),
                AvgCostUsdPerCall = callCount <= 0 ? 0 : Math.Round(totalCostUsd / callCount, 6),
                FallbackRate = callCount <= 0 ? 0 : Math.Round(fallbackCallCount / (decimal)callCount, 4),
                FirstUsedAt = ReadDateTime(document, "first_used_at"),
                LastUsedAt = ReadDateTime(document, "last_used_at")
            };
        }).ToList();
    }

    private static string? ReadString(BsonDocument document, string parentKey, string childKey)
    {
        if (!document.TryGetValue(parentKey, out var parentValue) || parentValue is not BsonDocument parent)
        {
            return null;
        }

        if (!parent.TryGetValue(childKey, out var value) || value.IsBsonNull)
        {
            return null;
        }

        return value.BsonType switch
        {
            BsonType.String => value.AsString,
            BsonType.ObjectId => value.AsObjectId.ToString(),
            _ => value.ToString()
        };
    }

    private static int ReadInt32(BsonDocument document, string key)
    {
        if (!document.TryGetValue(key, out var value) || value.IsBsonNull)
        {
            return 0;
        }

        return value.ToInt32();
    }

    private static decimal ReadDecimal(BsonDocument document, string key)
    {
        if (!document.TryGetValue(key, out var value) || value.IsBsonNull)
        {
            return 0m;
        }

        return Convert.ToDecimal(value.ToDouble());
    }

    private static double ReadDouble(BsonDocument document, string key)
    {
        if (!document.TryGetValue(key, out var value) || value.IsBsonNull)
        {
            return 0d;
        }

        return value.ToDouble();
    }

    private static DateTime ReadDateTime(BsonDocument document, string key)
    {
        if (!document.TryGetValue(key, out var value) || value.IsBsonNull)
        {
            return DateTime.MinValue;
        }

        return value.ToUniversalTime();
    }

    private static int ReadArrayCount(BsonDocument document, string key)
    {
        if (!document.TryGetValue(key, out var value) || value is not BsonArray array)
        {
            return 0;
        }

        return array.Count;
    }

    private static string? NormalizeEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
