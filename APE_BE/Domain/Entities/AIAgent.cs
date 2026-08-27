using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Domain.Enums;

namespace Domain.Entities;

public class AIAgent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.String)]
    public AIAgentRole AgentRole { get; set; }

    [BsonRepresentation(BsonType.String)]
    public AIProvider Provider { get; set; }

    public string ModelName { get; set; } = null!;
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public double CreditCost { get; set; }
    public bool IsEnabled { get; set; } = true;

    [BsonRepresentation(BsonType.String)]
    public AIProvider? FallbackProvider { get; set; }

    public string? FallbackModelName { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    
}
