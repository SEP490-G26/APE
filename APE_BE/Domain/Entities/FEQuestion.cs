using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class FEQuestion
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string CourseId { get; set; } = null!;
    public List<string> TopicTags { get; set; } = new();
    public string Difficulty { get; set; } = "Medium";
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public List<string> Options { get; set; } = new();
    public string? Explanation { get; set; }
    public string Status { get; set; } = "Draft";
    public string Source { get; set; } = "Admin";
    public string SourceScope { get; set; } = "SYSTEM";
    [BsonRepresentation(BsonType.ObjectId)]
    public string? OwnerUserId { get; set; }
    [BsonRepresentation(BsonType.ObjectId)]
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPublic { get; set; } = true;
    public string? QuestionFingerprint { get; set; }
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> SourceDocumentIds { get; set; } = new();
    [BsonElement("SourceChunkIds")]
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> LegacySourceChunkIds { get; set; } = new();
    public List<string> CorrectAnswer { get; set; } = new(); // hỗ trợ nhiều đáp án đúng
    // audit & status
    [BsonRepresentation(BsonType.ObjectId)]
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string? DisabledReason { get; set; }
    [BsonRepresentation(BsonType.ObjectId)]
    public string? DisabledBy { get; set; }
    public DateTime? DisabledAt { get; set; }

}
