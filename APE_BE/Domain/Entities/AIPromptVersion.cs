using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class AIPromptVersion
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string PromptKey { get; set; } = string.Empty;
    public string Version { get; set; } = "v1";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public List<string>? Variables { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
