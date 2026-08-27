using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class FE_Submission
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string FeQuestionId { get; set; } = null!;
    [BsonRepresentation(BsonType.ObjectId)]
    public string SessionId { get; set; } = null!;
    [BsonRepresentation(BsonType.ObjectId)]
    public string CourseId { get; set; } = null!;
    public List<string> UserAnswer { get; set; } = new(); // hỗ trợ nhiều đáp án
    public bool IsCorrect { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
