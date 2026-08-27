using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class AIPolicyArtifact
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string ArtifactKey { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = "Policy";
    public string Version { get; set; } = "v1";
    public bool IsActive { get; set; } = true;
    public string ContentJson { get; set; } = "{}";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
