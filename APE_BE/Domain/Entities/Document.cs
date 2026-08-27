using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Domain.Enums;

namespace Domain.Entities;

public class Document
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string CourseId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    public string FileName { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public string? FileChecksum { get; set; }
    public string? NormalizedContentChecksum { get; set; }
    public string? Source { get; set; }
    public string? SubjectCode { get; set; }
    public string? GatekeeperVerdict { get; set; }
    [BsonRepresentation(BsonType.ObjectId)]
    public string? LastExtractionDraftId { get; set; }
    public List<DocumentChapterSummary> ChapterSummaries { get; set; } = new();
    public DateTime? LastExtractedAt { get; set; }
    public DateTime? LastEmbeddedAt { get; set; }

    [BsonRepresentation(BsonType.String)]
    public DocumentStatus Status { get; set; } = DocumentStatus.Processing;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }
    [BsonRepresentation(BsonType.ObjectId)]
    public string? DeletedBy { get; set; }
}
