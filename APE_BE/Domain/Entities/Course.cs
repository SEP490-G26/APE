using MongoDB.Bson;

using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class Course
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!; // unique code
    public string? Description { get; set; }
    public string? SyllabusPath { get; set; }
    public List<ExamMatrixItem>? ExamMatrix { get; set; }
    public string Status { get; set; } = "Inactive"; // Active, Inactive, Deleted

    // audit & approval
    [BsonRepresentation(BsonType.ObjectId)]
    public string? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
   
    [BsonRepresentation(BsonType.ObjectId)]
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    [BsonRepresentation(BsonType.ObjectId)]
    public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class ExamMatrixItem
{
    public string Difficulty { get; set; } = null!; // Easy, Medium, Hard
    public int Count { get; set; }
    public double PointsPerQuestion { get; set; }
}
