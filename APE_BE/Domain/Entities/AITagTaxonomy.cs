using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class AITagTaxonomy
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string TaxonomyKey { get; set; } = "tag-taxonomy";
    public string Version { get; set; } = "v1";
    public bool IsActive { get; set; } = true;
    public string ContentJson { get; set; } = "{}";
    public string? Description { get; set; }
    public string? SourcePath { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? CreatedBy { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
