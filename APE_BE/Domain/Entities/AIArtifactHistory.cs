using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class AIArtifactHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string ArtifactKey { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = string.Empty;
    public string Version { get; set; } = "v1";
    public string ContentJson { get; set; } = "{}";
    public string? Description { get; set; }
    public string Source { get; set; } = "db";
    public string? SourcePath { get; set; }
    public string? ContentHash { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? SnapshotOfArtifactId { get; set; }

    public DateTime SnapshottedAt { get; set; } = DateTime.UtcNow;
}
