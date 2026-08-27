using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

[BsonIgnoreExtraElements]
public class AIRuleArtifact
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string ArtifactType { get; set; } = string.Empty;
    public string ArtifactKey { get; set; } = string.Empty;
    public AIRuleArtifactScope Scope { get; set; } = new();
    public string Version { get; set; } = "v1";
    public bool IsActive { get; set; } = true;
    public string ContentJson { get; set; } = "{}";
    public string? Description { get; set; }
    public string? Summary { get; set; }
    public string? ChangeReason { get; set; }
    public List<string> ChangeNotes { get; set; } = new();
    public string Source { get; set; } = "db";
    public string? SourcePath { get; set; }
    public string? ContentHash { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? CreatedBy { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? UpdatedBy { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ActivatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAt { get; set; }
}

public class AIRuleArtifactScope
{
    public string? SubjectCode { get; set; }
    public string? QuestionType { get; set; }
    public string? Language { get; set; }
}
