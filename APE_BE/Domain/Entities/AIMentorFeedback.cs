using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class AIMentorFeedback
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string? AgentId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string SubmissionId { get; set; } = null!;

    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string QuestionType { get; set; } = "PE";
    public string? Verdict { get; set; }

    public MentorQualityScore? QualityScore { get; set; }
    public MentorPerformanceSummary? PerformanceSummary { get; set; }
    public List<MentorErrorAnalysisItem> ErrorAnalysis { get; set; } = new();
    public List<MentorImprovementSuggestion> ImprovementSuggestions { get; set; } = new();
    public List<string> IssueCategories { get; set; } = new();

    public string? FeedbackText { get; set; }
    public string? SuggestedComplexity { get; set; }
    public string? ModelName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonIgnore]
    public string PeSubmissionId
    {
        get => SubmissionId;
        set => SubmissionId = value;
    }

    [BsonIgnore]
    public List<string> PeSubmissionIds
    {
        get => string.IsNullOrWhiteSpace(SubmissionId) ? new List<string>() : new List<string> { SubmissionId };
        set => SubmissionId = value?.FirstOrDefault() ?? SubmissionId;
    }
}

public class MentorQualityScore
{
    public double Overall { get; set; }
    public double Correctness { get; set; }
    public double Robustness { get; set; }
    public double CodeQuality { get; set; }
    public double Efficiency { get; set; }
    public double Confidence { get; set; }
}

public class MentorPerformanceSummary
{
    public string? Summary { get; set; }
    public string? TimeComplexity { get; set; }
    public string? SpaceComplexity { get; set; }
    public List<string> Notes { get; set; } = new();
}

public class MentorErrorAnalysisItem
{
    public string? Category { get; set; }
    public string? Severity { get; set; }
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public List<string> FailingScenarios { get; set; } = new();
}

public class MentorImprovementSuggestion
{
    public string? Priority { get; set; }
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public string? ExpectedImpact { get; set; }
}
