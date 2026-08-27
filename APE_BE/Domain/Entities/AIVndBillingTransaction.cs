using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class AIVndBillingTransaction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    public string FeatureKey { get; set; } = null!;
    public string SourceEntityType { get; set; } = null!;
    public string SourceEntityId { get; set; } = null!;
    public string Status { get; set; } = "charged";
    public decimal ReportedCostUsd { get; set; }
    public decimal UsdToVndRate { get; set; }
    public decimal ChargeMultiplier { get; set; }
    public long MinimumBalanceVnd { get; set; }
    public long ActualCostVnd { get; set; }
    public long ChargedVnd { get; set; }
    public long ActualDeductedVnd { get; set; }
    public long AbsorbedVnd { get; set; }
    public long RefundedVnd { get; set; }
    public long BalanceBeforeVnd { get; set; }
    public long BalanceAfterVnd { get; set; }
    public string? RefundReason { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> UsageLogIds { get; set; } = new();

    public string PolicySettingName { get; set; } = null!;
    public string PolicyVersion { get; set; } = null!;
    public object? PolicySnapshot { get; set; }
    public object? UsageSnapshot { get; set; }
    public object? ChargeBreakdown { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string CreatedBy { get; set; } = null!;

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
