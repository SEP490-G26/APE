using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Domain.Enums;

namespace Domain.Entities;

public class Report
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? TargetId { get; set; } // Nullable

    public string? Description { get; set; }

    [BsonRepresentation(BsonType.String)]
    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
