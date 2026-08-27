using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class AIVndBillingTransactionRepository : IAIVndBillingTransactionRepository
{
    private readonly IMongoCollection<AIVndBillingTransaction> _collection;

    public AIVndBillingTransactionRepository(DbContext dbContext)
    {
        _collection = dbContext.AIVndBillingTransactions;
    }

    public Task CreateAsync(AIVndBillingTransaction transaction, CancellationToken cancellationToken = default)
        => _collection.InsertOneAsync(transaction, cancellationToken: cancellationToken);

    public async Task<AIVndBillingTransaction?> GetByIdAsync(string transactionId, CancellationToken cancellationToken = default)
        => await _collection.Find(item => item.Id == transactionId).FirstOrDefaultAsync(cancellationToken);

    public Task UpdateAsync(AIVndBillingTransaction transaction, CancellationToken cancellationToken = default)
        => _collection.ReplaceOneAsync(item => item.Id == transaction.Id, transaction, cancellationToken: cancellationToken);

    public async Task<AIVndBillingTransaction?> GetLatestBySourceEntityAsync(
        string sourceEntityType,
        string sourceEntityId,
        string? featureKey = null,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<AIVndBillingTransaction>.Filter.Eq(item => item.SourceEntityType, sourceEntityType)
            & Builders<AIVndBillingTransaction>.Filter.Eq(item => item.SourceEntityId, sourceEntityId);

        if (!string.IsNullOrWhiteSpace(featureKey))
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Eq(item => item.FeatureKey, featureKey);
        }

        return await _collection.Find(filter)
            .SortByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(List<AIVndBillingTransaction> Items, long Total)> ListAsync(
        string? userId,
        string? featureKey,
        string? status,
        string? sourceEntityType,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        limit = Math.Max(1, limit);
        var skip = (page - 1) * limit;
        var filter = Builders<AIVndBillingTransaction>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Eq(item => item.UserId, userId);
        }

        if (!string.IsNullOrWhiteSpace(featureKey))
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Eq(item => item.FeatureKey, featureKey);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Eq(item => item.Status, status);
        }

        if (!string.IsNullOrWhiteSpace(sourceEntityType))
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Eq(item => item.SourceEntityType, sourceEntityType);
        }

        if (fromDate.HasValue)
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Gte(item => item.CreatedAt, fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Lte(item => item.CreatedAt, toDate.Value);
        }

        var items = await _collection.Find(filter)
            .SortByDescending(item => item.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        return (items, total);
    }

    public async Task<List<AIVndBillingTransactionSummaryItemDto>> SummarizeAsync(
        string? featureKey,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        var filter = Builders<AIVndBillingTransaction>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(featureKey))
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Eq(item => item.FeatureKey, featureKey);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Eq(item => item.Status, status);
        }

        if (fromDate.HasValue)
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Gte(item => item.CreatedAt, fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Lte(item => item.CreatedAt, toDate.Value);
        }

        var pipeline = await _collection.Aggregate()
            .Match(filter)
            .Group(new BsonDocument
            {
                { "_id", new BsonDocument
                    {
                        { "feature_key", "$FeatureKey" },
                        { "status", "$Status" },
                        { "policy_version", "$PolicyVersion" }
                    }
                },
                { "transaction_count", new BsonDocument("$sum", 1) },
                { "total_reported_cost_usd", new BsonDocument("$sum", "$ReportedCostUsd") },
                { "total_actual_cost_vnd", new BsonDocument("$sum", "$ActualCostVnd") },
                { "total_charged_vnd", new BsonDocument("$sum", "$ChargedVnd") },
                { "total_actual_deducted_vnd", new BsonDocument("$sum", "$ActualDeductedVnd") },
                { "total_absorbed_vnd", new BsonDocument("$sum", "$AbsorbedVnd") },
                { "distinct_users", new BsonDocument("$addToSet", "$UserId") },
                { "first_created_at", new BsonDocument("$min", "$CreatedAt") },
                { "last_created_at", new BsonDocument("$max", "$CreatedAt") }
            })
            .Sort(new BsonDocument { { "last_created_at", -1 }, { "transaction_count", -1 } })
            .Limit(take)
            .ToListAsync(cancellationToken);

        return pipeline.Select(document => new AIVndBillingTransactionSummaryItemDto
        {
            FeatureKey = ReadNestedString(document, "_id", "feature_key") ?? string.Empty,
            Status = ReadNestedString(document, "_id", "status") ?? string.Empty,
            PolicyVersion = ReadNestedString(document, "_id", "policy_version") ?? string.Empty,
            TransactionCount = ReadInt32(document, "transaction_count"),
            DistinctUserCount = ReadArrayCount(document, "distinct_users"),
            TotalReportedCostUsd = ReadDecimal(document, "total_reported_cost_usd"),
            TotalActualCostVnd = ReadInt64(document, "total_actual_cost_vnd"),
            TotalChargedVnd = ReadInt64(document, "total_charged_vnd"),
            TotalActualDeductedVnd = ReadInt64(document, "total_actual_deducted_vnd"),
            TotalAbsorbedVnd = ReadInt64(document, "total_absorbed_vnd"),
            FirstCreatedAt = ReadDateTime(document, "first_created_at"),
            LastCreatedAt = ReadDateTime(document, "last_created_at")
        }).ToList();
    }

    public async Task<List<AIVndBillingDailySummaryItemDto>> SummarizeDailyAsync(
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 366);
        var filter = Builders<AIVndBillingTransaction>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Eq(item => item.Status, status);
        }

        if (fromDate.HasValue)
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Gte(item => item.CreatedAt, fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filter &= Builders<AIVndBillingTransaction>.Filter.Lte(item => item.CreatedAt, toDate.Value);
        }

        var pipeline = await _collection.Aggregate()
            .Match(filter)
            .Group(new BsonDocument
            {
                { "_id", new BsonDocument
                    {
                        { "year", new BsonDocument("$year", "$CreatedAt") },
                        { "month", new BsonDocument("$month", "$CreatedAt") },
                        { "day", new BsonDocument("$dayOfMonth", "$CreatedAt") }
                    }
                },
                { "transaction_count", new BsonDocument("$sum", 1) },
                { "distinct_users", new BsonDocument("$addToSet", "$UserId") },
                { "total_actual_cost_vnd", new BsonDocument("$sum", "$ActualCostVnd") },
                { "total_charged_vnd", new BsonDocument("$sum", "$ChargedVnd") },
                { "total_actual_deducted_vnd", new BsonDocument("$sum", "$ActualDeductedVnd") },
                { "total_absorbed_vnd", new BsonDocument("$sum", "$AbsorbedVnd") }
            })
            .Sort(new BsonDocument
            {
                { "_id.year", 1 },
                { "_id.month", 1 },
                { "_id.day", 1 }
            })
            .Limit(take)
            .ToListAsync(cancellationToken);

        return pipeline.Select(document => new AIVndBillingDailySummaryItemDto
        {
            Date = ReadGroupedDate(document),
            TransactionCount = ReadInt32(document, "transaction_count"),
            DistinctUserCount = ReadArrayCount(document, "distinct_users"),
            TotalActualCostVnd = ReadInt64(document, "total_actual_cost_vnd"),
            TotalChargedVnd = ReadInt64(document, "total_charged_vnd"),
            TotalActualDeductedVnd = ReadInt64(document, "total_actual_deducted_vnd"),
            TotalAbsorbedVnd = ReadInt64(document, "total_absorbed_vnd")
        }).ToList();
    }

    private static string? ReadNestedString(BsonDocument document, string parentKey, string childKey)
    {
        if (!document.TryGetValue(parentKey, out var parentValue) || parentValue is not BsonDocument parent)
        {
            return null;
        }

        if (!parent.TryGetValue(childKey, out var value) || value.IsBsonNull)
        {
            return null;
        }

        return value.AsString;
    }

    private static int ReadInt32(BsonDocument document, string key)
        => !document.TryGetValue(key, out var value) || value.IsBsonNull ? 0 : value.ToInt32();

    private static long ReadInt64(BsonDocument document, string key)
        => !document.TryGetValue(key, out var value) || value.IsBsonNull ? 0L : value.ToInt64();

    private static decimal ReadDecimal(BsonDocument document, string key)
        => !document.TryGetValue(key, out var value) || value.IsBsonNull ? 0m : value.ToDecimal();

    private static int ReadArrayCount(BsonDocument document, string key)
        => !document.TryGetValue(key, out var value) || value is not BsonArray array ? 0 : array.Count;

    private static DateTime ReadDateTime(BsonDocument document, string key)
        => !document.TryGetValue(key, out var value) || value.IsBsonNull ? DateTime.MinValue : value.ToUniversalTime();

    private static DateTime ReadGroupedDate(BsonDocument document)
    {
        if (!document.TryGetValue("_id", out var value) || value is not BsonDocument grouped)
        {
            return DateTime.MinValue;
        }

        var year = grouped.TryGetValue("year", out var yearValue) ? yearValue.ToInt32() : 1;
        var month = grouped.TryGetValue("month", out var monthValue) ? monthValue.ToInt32() : 1;
        var day = grouped.TryGetValue("day", out var dayValue) ? dayValue.ToInt32() : 1;

        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }
}
