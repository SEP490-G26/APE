using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

public class AIContextPack
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string PackType { get; set; } = "generation_context";
    public string PackStrategy { get; set; } = "summary_first";
    public string PackStatus { get; set; } = "active";

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? CourseId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? DocumentId { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string TargetDifficulty { get; set; } = string.Empty;
    public List<string> TargetTopics { get; set; } = new();
    public string SourceScope { get; set; } = "HYBRID";
    public string? RetrievalQuery { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> SourceChunkIds { get; set; } = new();

    public List<RetrievedChunkSnapshot> RetrievedChunks { get; set; } = new();
    public List<PackedChunkSnapshot> Chunks { get; set; } = new();
    public string SummaryText { get; set; } = string.Empty;
    public string ContextText { get; set; } = string.Empty;
    public string? DominantChapterKey { get; set; }
    public List<string> RequestedChapterKeys { get; set; } = new();
    public int? SourcePageFrom { get; set; }
    public int? SourcePageTo { get; set; }
    public List<string> CoveredTopics { get; set; } = new();
    public List<string> UncoveredTopics { get; set; } = new();
    public decimal TopicCoverageRatio { get; set; }
    public int DistinctSectionCount { get; set; }
    public int TokenCount { get; set; }
    public int SourceTokenCount { get; set; }
    public decimal CompressionRatio { get; set; }
    public int RecommendedQuestionCount { get; set; }
    public int MaxQuestionCount { get; set; }
    public int UsageCount { get; set; }
    public string? LastRetrievalPlanId { get; set; }
    public DateTime? LastRetrievedAt { get; set; }
    public string? LastGenerationRunId { get; set; }
    public string? LastGenerationStatus { get; set; }
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> LastPersistedQuestionIds { get; set; } = new();
    public DateTime? LastGeneratedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class RetrievedChunkSnapshot
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string ChunkId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? DocumentId { get; set; }

    public int ChunkIndex { get; set; }
    public string? SectionTitle { get; set; }
    public string? ChapterKey { get; set; }
    public int? SourcePageFrom { get; set; }
    public int? SourcePageTo { get; set; }
    public List<string> TopicTags { get; set; } = new();
    public List<string> SelectionReasons { get; set; } = new();
    public int TokenCount { get; set; }
    public decimal TopicScore { get; set; }
    public decimal LexicalScore { get; set; }
    public decimal SectionScore { get; set; }
    public decimal FinalScore { get; set; }
    public string SelectionRole { get; set; } = "support";
    public string? ContentText { get; set; }
}

public class PackedChunkSnapshot
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string ChunkId { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string PackedText { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string SelectionRole { get; set; } = "support";
}
