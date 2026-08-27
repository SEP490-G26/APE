namespace Application.DTOs;

public class DocumentUploadDto
{
    public string CourseId { get; set; } = null!;

}

public class DocumentDto
{
    public string Id { get; set; } = null!;
    public string CourseId { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public string Status
    {
        get => IngestionStatus ?? "processing";
        set => IngestionStatus = value;
    }
    public bool IsActive { get; set; } = true;
    public string FilePath { get; set; } = null!;
    public string? Source { get; set; }
    public string? SubjectCode { get; set; }
    public string? GatekeeperVerdict { get; set; }
    public string? LastExtractionDraftId { get; set; }
    public string? IngestionStatus { get; set; }
    public string? IngestionStage { get; set; }
    public bool HasExtractionDraft { get; set; }
    public bool HasEmbeddedChunks { get; set; }
    public bool IsReadyForGeneration { get; set; }
    public int ChunkCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastExtractedAt { get; set; }
    public DateTime? LastEmbeddedAt { get; set; }
    public bool DuplicateDetected { get; set; }
    public string? DuplicateOfDocumentId { get; set; }
    public string? DuplicateMatchType { get; set; }
    public decimal UsdToVndRate { get; set; }
    public long ActualCostVnd { get; set; }
    public long ChargedVnd { get; set; }
    public long ActualDeductedVnd { get; set; }
    public long AbsorbedVnd { get; set; }
    public long RemainingBalanceVnd { get; set; }
}

public class DocumentListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Source { get; set; }
    public string? SubjectCode { get; set; }
    public string? GatekeeperVerdict { get; set; }
    public string IngestionStatus { get; set; } = string.Empty;
    public string IngestionStage { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool HasExtractionDraft { get; set; }
    public bool HasEmbeddedChunks { get; set; }
    public bool IsReadyForGeneration { get; set; }
    public bool HasTopics { get; set; }
    public int ChunkCount { get; set; }
    public int TagCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastExtractedAt { get; set; }
    public DateTime? LastEmbeddedAt { get; set; }
}

public class DocumentPreviewDto
{
    public string Id { get; set; } = null!;
    public string PreviewText { get; set; } = null!;
    public string FilePath { get; set; } = null!;
}

public class DocumentPreviewResultDto
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Source { get; set; }
    public string? SubjectCode { get; set; }
    public string? GatekeeperVerdict { get; set; }
    public string? IngestionStatus { get; set; }
    public string? IngestionStage { get; set; }
    public string? LastExtractionDraftId { get; set; }
    public string PreviewText { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public int ChunkCount { get; set; }
    public bool HasExtractionDraft { get; set; }
    public bool HasEmbeddedChunks { get; set; }
    public bool IsReadyForGeneration { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastExtractedAt { get; set; }
    public DateTime? LastEmbeddedAt { get; set; }
    public List<string> CleanupWarnings { get; set; } = new();
    public List<DocumentChapterSummaryDto> Chapters { get; set; } = new();
}

public class DocumentChapterSummaryDto
{
    public string ChapterKey { get; set; } = string.Empty;
    public string ChapterTitle { get; set; } = string.Empty;
    public int? ChapterOrder { get; set; }
    public int ChunkCount { get; set; }
    public int EstimatedTokens { get; set; }
    public List<string> CoveredTopics { get; set; } = new();
    public List<string> SampleSectionTitles { get; set; } = new();
    public string? OverviewShort { get; set; }
}

public class EditContentDto
{
    public string Content { get; set; } = null!;
}

public class ReviewDecisionDto
{
    public string? Reason { get; set; }
}

public class ExtractionDraftDto
{
    public string DraftId { get; set; } = null!;
    public string DocumentId { get; set; } = null!;
    public string? CourseId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string SubjectCode { get; set; } = string.Empty;
    public string IngestParser { get; set; } = string.Empty;
    public string ExtractionMode { get; set; } = string.Empty;
    public string? VisionModel { get; set; }
    public int TotalPages { get; set; }
    public int TotalSegments { get; set; }
    public int DetectedImagePlaceholderCount { get; set; }
    public int EmbeddedImageCount { get; set; }
    public int VisionEnrichmentAttemptedCount { get; set; }
    public int VisionEnrichmentSucceededCount { get; set; }
    public int VisionEnrichmentFailedCount { get; set; }
    public int UnresolvedImagePlaceholderCount { get; set; }
    public List<string> CandidateTitles { get; set; } = new();
    public List<string> CandidateChapterMarkers { get; set; } = new();
    public List<string> RejectedHeadingCandidates { get; set; } = new();
    public List<string> CleanDisplayTitleCandidates { get; set; } = new();
    public string ReviewStatus { get; set; } = "auto_ingested";
    public string IngestionStage { get; set; } = string.Empty;
    public string? CleanMarkdownPreview { get; set; }
    public List<string> CleanupWarnings { get; set; } = new();
    public int ApprovedSegments { get; set; }
    public int RejectedSegments { get; set; }
    public bool ChunkingReady { get; set; }
    public bool HasEmbeddedChunks { get; set; }
    public bool IsReadyForGeneration { get; set; }
    public int ChunkCount { get; set; }
    public string? LastEmbeddingRunId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
