using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Domain.Enums;

namespace Domain.Entities;

public class Exam
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string Title { get; set; } = null!;
    [BsonRepresentation(BsonType.ObjectId)]
    public string CourseId { get; set; } = null!;
    public string ExamType { get; set; } = "Mixed";

    [BsonRepresentation(BsonType.String)]
    public ExamMode Mode { get; set; }

    public int? TimeLimit { get; set; }              // phút

    [BsonRepresentation(BsonType.ObjectId)]
    public string CreatedBy { get; set; } = null!;

    [BsonRepresentation(BsonType.String)]
    public ExamVisibility Visibility { get; set; }

    public string PresetScope { get; set; } = "Course";

    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> FeExamQuestions { get; set; } = new();

    public List<PeExamQuestion> PeExamQuestions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [BsonRepresentation(BsonType.ObjectId)]
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Soft delete: keep history and audit, but hide from libraries and prevent new sessions.
    public bool IsDeleted { get; set; }
    [BsonRepresentation(BsonType.ObjectId)]
    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class PeExamQuestion
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string PeQuestionId { get; set; } = null!;
    public double AssignedPoints { get; set; }
}
