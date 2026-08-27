using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class AIUsageLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string TriggeredBy { get; set; } = null!; // UserId

    [BsonRepresentation(BsonType.ObjectId)]
    public string AgentId { get; set; } = null!;

    public double CreditsDeducted { get; set; }
    public int TokensUsed { get; set; }
    public decimal CostUsd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public object? PayloadData { get; set; } // JSON, có thể xóa sau 30 ngày
}