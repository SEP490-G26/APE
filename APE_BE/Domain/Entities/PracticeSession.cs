using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Domain.Enums;

namespace Domain.Entities;

public class PracticeSession
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string StudentId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ExamId { get; set; } = null!;

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public DateTime? EndTime { get; set; }

    public int ActiveDurationSeconds { get; set; } = 0;

    public DateTime? LastResumedAt { get; set; }

    public DateTime? LastPausedAt { get; set; }

    public bool IsPaused { get; set; }

    [BsonRepresentation(BsonType.String)]
    public SessionStatus Status { get; set; } = SessionStatus.InProgress;

    public double TotalScore { get; set; } = 0;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? SelectedQuestionId { get; set; }

    public List<DraftCodeItem> DraftCodes { get; set; } = new();
}

public class DraftCodeItem
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string QuestionId { get; set; } = null!;

    public List<CodeFile> Files { get; set; } = new();
}
