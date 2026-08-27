using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class TopupPackage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long AmountVnd { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
