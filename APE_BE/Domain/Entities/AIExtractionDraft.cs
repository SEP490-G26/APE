using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Entities;

[BsonIgnoreExtraElements]
public class AIExtractionDraft
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string DocumentId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? CourseId { get; set; }

    public string SourceType { get; set; } = "text";
    public string SourceName { get; set; } = string.Empty;
    public string? SourceStoragePath { get; set; }
    public long SourceSizeBytes { get; set; }
    public string SourceChecksum { get; set; } = string.Empty;

    public string Language { get; set; } = "en";
    public string SubjectCode { get; set; } = string.Empty;
    public string OwnershipType { get; set; } = "byos";

    public string IngestParser { get; set; } = string.Empty;
    public string IngestParserVersion { get; set; } = "v1";
    public string ExtractionMode { get; set; } = "text_only";
    public string? VisionProvider { get; set; }
    public string? VisionModel { get; set; }

    public int TotalPages { get; set; }
    public int TotalSegments { get; set; }
    public int ApprovedSegments { get; set; }
    public int RejectedSegments { get; set; }
    public int DetectedImagePlaceholderCount { get; set; }
    public List<string> DetectedImageReferences { get; set; } = new();
    public int EmbeddedImageCount { get; set; }
    public int VisionEnrichmentAttemptedCount { get; set; }
    public int VisionEnrichmentSucceededCount { get; set; }
    public int VisionEnrichmentFailedCount { get; set; }
    public int UnresolvedImagePlaceholderCount { get; set; }
    public List<string> CandidateTitles { get; set; } = new();
    public List<string> CandidateChapterMarkers { get; set; } = new();
    public List<string> RejectedHeadingCandidates { get; set; } = new();
    public List<string> CleanDisplayTitleCandidates { get; set; } = new();
    public List<string> CleanupWarnings { get; set; } = new();

    public string? RawTextPreview { get; set; }
    public string? CleanMarkdownPreview { get; set; }
    public string? ApprovedMarkdownPreview { get; set; }
    public string ReviewStatus { get; set; } = "needs_review";
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int ApprovalVersion { get; set; }

    public long ExtractionInputTokens { get; set; }
    public long ExtractionOutputTokens { get; set; }
    public decimal ExtractionCostUsd { get; set; }
    public decimal ExtractionLatencyMs { get; set; }

    public string CreditChargeStatus { get; set; } = "pending";
    public decimal CreditChargeAmount { get; set; }
    public ExtractionPricingBasisSnapshot PricingBasisSnapshot { get; set; } = new();

    public bool ChunkingReady { get; set; }
    public int ChunkCount { get; set; }
    public string? LastEmbeddingRunId { get; set; }
    public List<DocumentChapterSummary> ChapterSummaries { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class ExtractionPricingBasisSnapshot
{
    public long SourceSizeBytes { get; set; }
    public int TotalPages { get; set; }
    public int TotalWordsEstimate { get; set; }
    public int DetectedImagePlaceholderCount { get; set; }
    public int EmbeddedImageCount { get; set; }
    public int VisionEnrichmentAttemptedCount { get; set; }
    public int VisionEnrichmentSucceededCount { get; set; }
    public int VisionEnrichmentFailedCount { get; set; }
    public int UnresolvedImagePlaceholderCount { get; set; }
    public long ExtractionInputTokens { get; set; }
    public long ExtractionOutputTokens { get; set; }
    public decimal ExtractionCostUsd { get; set; }
}
