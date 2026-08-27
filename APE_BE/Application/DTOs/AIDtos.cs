using System.Text.Json;

namespace Application.DTOs;

public class GatekeeperRequestContext
{
    public string? FileName { get; set; }
    public string? SubjectHint { get; set; }
    public string? Language { get; set; }
    public AIRuntimeOverride? RuntimeOverride { get; set; }
}

public class AIPromptTemplateDto
{
    public string Key { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = "Prompt";
    public string Source { get; set; } = "unknown";
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public List<string>? Variables { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class AIJsonArtifactDto
{
    public string ArtifactKey { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Source { get; set; } = "unknown";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? UpdatedAt { get; set; }
    public JsonElement Content { get; set; }
}

public class AITextRequest
{
    public string FeatureName { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public List<AIImageInput> Images { get; set; } = new();
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public AIRuntimeOverride? RuntimeOverride { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class AIImageInput
{
    public string ImageId { get; set; } = string.Empty;
    public string MimeType { get; set; } = "image/png";
    public string Base64Data { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Label { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

public class AITextResponse
{
    public string Content { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string UsageSource { get; set; } = "missing_from_provider_response";
    public decimal? ReportedCostUsd { get; set; }
    public string CostSource { get; set; } = "not_available";
    public bool FromFallback { get; set; }
    public string? FallbackFromProvider { get; set; }
    public string? FallbackFromModel { get; set; }
    public string? FallbackReasonCode { get; set; }
    public string? FallbackReason { get; set; }
    public string? RawResponse { get; set; }
}

public class AIEmbeddingRequest
{
    public string FeatureName { get; set; } = string.Empty;
    public List<string> Inputs { get; set; } = new();
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public AIRuntimeOverride? RuntimeOverride { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class AIEmbeddingResponse
{
    public List<List<double>> Embeddings { get; set; } = new();
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public int? TokensUsed { get; set; }
    public string UsageSource { get; set; } = "missing_from_provider_response";
    public decimal? ReportedCostUsd { get; set; }
    public string CostSource { get; set; } = "not_available";
    public bool FromFallback { get; set; }
    public string? FallbackFromProvider { get; set; }
    public string? FallbackFromModel { get; set; }
    public string? FallbackReasonCode { get; set; }
    public string? FallbackReason { get; set; }
    public string? RawResponse { get; set; }
}

public class EmbeddingTaggingRunResult
{
    public IReadOnlyList<Domain.Entities.KnowledgeChunk> Chunks { get; set; } = Array.Empty<Domain.Entities.KnowledgeChunk>();
    public EmbeddingUsageSummaryDto? EmbeddingUsage { get; set; }
    public TaggingUsageSummaryDto? TaggingUsage { get; set; }
}

public class ExtractionStructuralHintsDto
{
    public List<string> CandidateTitles { get; set; } = new();
    public List<string> CandidateChapterMarkers { get; set; } = new();
    public List<string> RejectedHeadingCandidates { get; set; } = new();
    public List<string> CleanDisplayTitleCandidates { get; set; } = new();
    public List<ExtractedStructuralUnitDto> StructuralUnits { get; set; } = new();
}

public class ExtractedStructuralUnitDto
{
    public string UnitKey { get; set; } = string.Empty;
    public string Kind { get; set; } = "fallback_block";
    public string DisplayTitle { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int? EndLine { get; set; }
    public decimal? Confidence { get; set; }
    public bool LearnerFacing { get; set; } = true;
    public List<string> Aliases { get; set; } = new();
}

public class EmbeddingUsageSummaryDto
{
    public string Provider { get; set; } = string.Empty;
    public string ConfiguredModel { get; set; } = string.Empty;
    public string EffectiveModel { get; set; } = string.Empty;
    public string ModelFamily { get; set; } = string.Empty;
    public string NormalizedModelKey { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public string UsageSource { get; set; } = "missing_from_provider_response";
    public decimal CostUsd { get; set; }
    public string CostSource { get; set; } = "estimated_catalog";
    public int VectorCount { get; set; }
    public int EmbeddingDimension { get; set; }
    public bool FallbackUsed { get; set; }
    public string? FallbackFromProvider { get; set; }
    public string? FallbackFromModel { get; set; }
    public string? FallbackReasonCode { get; set; }
}

public class TaggingUsageSummaryDto
{
    public string Provider { get; set; } = string.Empty;
    public string ConfiguredModel { get; set; } = string.Empty;
    public string EffectiveModel { get; set; } = string.Empty;
    public string ModelFamily { get; set; } = string.Empty;
    public string NormalizedModelKey { get; set; } = string.Empty;
    public int CallCount { get; set; }
    public int SuccessfulCalls { get; set; }
    public int FallbackCalls { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public string UsageSource { get; set; } = "missing_from_provider_response";
    public decimal CostUsd { get; set; }
    public string CostSource { get; set; } = "estimated_catalog";
    public bool FallbackUsed { get; set; }
    public string? FallbackFromProvider { get; set; }
    public string? FallbackFromModel { get; set; }
    public string? FallbackReasonCode { get; set; }
}

public class AIRuntimeOverride
{
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
}

public class AIResolvedExecutionOptions
{
    public string FeatureName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public bool UsedFrontendOverride { get; set; }
    public string? FallbackProvider { get; set; }
    public string? FallbackModel { get; set; }
}

public class ExtractionResultDto
{
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string SourceType { get; set; } = "text";
    public bool IsVisionRecommended { get; set; }
    public string? ParserName { get; set; }
    public int EstimatedPageCount { get; set; }
    public List<string> DetectedImageReferences { get; set; } = new();
    public List<AIImageInput> ExtractedImages { get; set; } = new();
    public int EmbeddedImageCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class ExtractedContentResultDto
{
    public string Content
    {
        get => NormalizedMarkdown;
        set => NormalizedMarkdown = value ?? string.Empty;
    }

    public string RawText { get; set; } = string.Empty;
    public string NormalizedMarkdown { get; set; } = string.Empty;
    public string SourceType { get; set; } = "text";
    public bool UsedAiNormalization { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? ConfiguredModel { get; set; }
    public string? EffectiveModel { get; set; }
    public string? UsageSource { get; set; }
    public string? CostSource { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public decimal? CostUsd { get; set; }
    public bool FallbackUsed { get; set; }
    public string? FallbackFromProvider { get; set; }
    public string? FallbackFromModel { get; set; }
    public string? FallbackReasonCode { get; set; }
    public string? ParserName { get; set; }
    public string? VisionModel { get; set; }
    public string ExtractionMode { get; set; } = "text_only";
    public int WordCount { get; set; }
    public int DetectedImagePlaceholderCount { get; set; }
    public int VisionEnrichmentAttemptedCount { get; set; }
    public int VisionEnrichmentSucceededCount { get; set; }
    public int VisionEnrichmentFailedCount { get; set; }
    public int VisionEnrichmentSkippedCount { get; set; }
    public int UnresolvedImagePlaceholderCount { get; set; }
    public List<VisionEnrichmentDetailDto> VisionEnrichmentDetails { get; set; } = new();
    public List<string> DetectedImageReferences { get; set; } = new();
    public int EmbeddedImageCount { get; set; }
    public List<string> CandidateTitles { get; set; } = new();
    public List<string> CandidateChapterMarkers { get; set; } = new();
    public List<string> RejectedHeadingCandidates { get; set; } = new();
    public List<string> CleanDisplayTitleCandidates { get; set; } = new();
    public List<ExtractedStructuralUnitDto> StructuralUnits { get; set; } = new();
    public bool UsedAiStructureDetection { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class VisionEnrichmentDetailDto
{
    public int Index { get; set; }
    public string ImageId { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Label { get; set; }
    public int? PageNumber { get; set; }
    public int? SlideNumber { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string InitialImageKind { get; set; } = string.Empty;
    public string FinalImageKind { get; set; } = string.Empty;
    public int ContextWindow { get; set; }
    public int MaxTokens { get; set; }
    public bool Skipped { get; set; }
    public string? SkipReason { get; set; }
    public bool RetryUsed { get; set; }
    public string? RetryFallbackKind { get; set; }
    public bool Accepted { get; set; }
    public string? RejectionReason { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? ProviderStatus { get; set; }
    public string? Preview { get; set; }
}

public class RetrievalPlanRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public string? CourseId { get; set; }
    public string? DocumentId { get; set; }
    public string? ChapterKey { get; set; }
    public List<string> ChapterKeys { get; set; } = new();
    public string Subject { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public int RequestedQuestionCount { get; set; } = 1;
    public string GenerationMode { get; set; } = "Single";
    public List<string> TargetTopics { get; set; } = new();
    public string SourceScope { get; set; } = "HYBRID";
    public string? Language { get; set; }
    public bool UseCachedPack { get; set; } = true;
    public bool ForceRebuildPack { get; set; }
    public bool IncludeChunkText { get; set; } = true;
    public int MaxCandidateCount { get; set; } = 24;
    public int MaxPackedTokens { get; set; } = 2400;
    public string? RetrievalQuery { get; set; }
    public bool AllowExtendedScope { get; set; }
}

public class RetrievalTokenBudgetDto
{
    public int MaxPackedTokens { get; set; }
    public int EstimatedPackedTokens { get; set; }
    public int EstimatedSourceTokens { get; set; }
    public decimal CompressionRatio { get; set; }
}

public class RetrievalCandidateStatsDto
{
    public int PreFilterCount { get; set; }
    public int TopicMatchCount { get; set; }
    public int RankedCount { get; set; }
    public int SelectedChunkCount { get; set; }
    public int CoveredTopicCount { get; set; }
    public int RequestedTopicCount { get; set; }
    public decimal TopicCoverageRatio { get; set; }
}

public class RetrievedChunkDto
{
    public string ChunkId { get; set; } = string.Empty;
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
    public string SelectionRole { get; set; } = string.Empty;
    public string? ContentText { get; set; }
}

public class PackedChunkDto
{
    public string ChunkId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PackedText { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string SelectionRole { get; set; } = string.Empty;
}

public class ContextPackDto
{
    public string PackId { get; set; } = string.Empty;
    public string PackType { get; set; } = string.Empty;
    public string PackStrategy { get; set; } = string.Empty;
    public string PackStatus { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? CourseId { get; set; }
    public string? DocumentId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string TargetDifficulty { get; set; } = string.Empty;
    public List<string> TargetTopics { get; set; } = new();
    public string SourceScope { get; set; } = string.Empty;
    public string? RetrievalQuery { get; set; }
    public List<string> SourceChunkIds { get; set; } = new();
    public List<RetrievedChunkDto> RetrievedChunks { get; set; } = new();
    public List<PackedChunkDto> Chunks { get; set; } = new();
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
    public List<string> LastPersistedQuestionIds { get; set; } = new();
    public DateTime? LastGeneratedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RetrievalPlanResultDto
{
    public string PlanId { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public bool UsedCachedPack { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string GenerationMode { get; set; } = string.Empty;
    public List<string> TargetTopics { get; set; } = new();
    public string? RetrievalQuery { get; set; }
    public RetrievalTokenBudgetDto TokenBudget { get; set; } = new();
    public RetrievalCandidateStatsDto CandidateStats { get; set; } = new();
    public List<RetrievedChunkDto> SelectedChunks { get; set; } = new();
    public ContextPackDto ContextPack { get; set; } = new();
}

public class ContextPackSummaryDto
{
    public string PackId { get; set; } = string.Empty;
    public string? CourseId { get; set; }
    public string? DocumentId { get; set; }
    public string SourceScope { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public List<string> TargetTopics { get; set; } = new();
    public string PackStrategy { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public int RecommendedQuestionCount { get; set; }
    public int UsageCount { get; set; }
    public string PackStatus { get; set; } = string.Empty;
    public string? LastGenerationStatus { get; set; }
    public List<string> CoveredTopics { get; set; } = new();
    public List<string> UncoveredTopics { get; set; } = new();
    public decimal TopicCoverageRatio { get; set; }
    public string? DominantChapterKey { get; set; }
    public int? SourcePageFrom { get; set; }
    public int? SourcePageTo { get; set; }
    public string? LastGenerationRunId { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GenerationMetricsDto
{
    public string GeneratorProvider { get; set; } = string.Empty;
    public string GeneratorConfiguredModel { get; set; } = string.Empty;
    public string GeneratorEffectiveModel { get; set; } = string.Empty;
    public string GeneratorModelFamily { get; set; } = string.Empty;
    public string ReviewerProvider { get; set; } = string.Empty;
    public string ReviewerConfiguredModel { get; set; } = string.Empty;
    public string ReviewerEffectiveModel { get; set; } = string.Empty;
    public string ReviewerModelFamily { get; set; } = string.Empty;
    public int GenerationInputTokens { get; set; }
    public int GenerationOutputTokens { get; set; }
    public int ReviewInputTokens { get; set; }
    public int ReviewOutputTokens { get; set; }
    public string GenerationRunId { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public string ContextPackId { get; set; } = string.Empty;
    public decimal TotalReportedCostUsd { get; set; }
    public decimal UsdToVndRate { get; set; }
    public long ChargedVnd { get; set; }
    public long ActualDeductedVnd { get; set; }
    public long AbsorbedVnd { get; set; }
    public long RemainingBalanceVnd { get; set; }
    public string GenerationUsageSource { get; set; } = string.Empty;
    public string ReviewUsageSource { get; set; } = string.Empty;
    public string GenerationCostSource { get; set; } = string.Empty;
    public string ReviewCostSource { get; set; } = string.Empty;
    public List<string> PersistedFeQuestionIds { get; set; } = new();
    public List<string> PersistedPeQuestionIds { get; set; } = new();
    public string GeneratorPromptKey { get; set; } = string.Empty;
    public string GeneratorPromptVersion { get; set; } = string.Empty;
    public string ReviewerPromptKey { get; set; } = string.Empty;
    public string ReviewerPromptVersion { get; set; } = string.Empty;
    public string RubricKey { get; set; } = string.Empty;
    public string RubricVersion { get; set; } = string.Empty;
}

public class TopicSummaryItemDto
{
    public string Tag { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public int DocumentCount { get; set; }
    public int EstimatedTokens { get; set; }
    public List<string> SampleSectionTitles { get; set; } = new();
    public List<string> SourceChapterKeys { get; set; } = new();
}

public class TopicSummaryResultDto
{
    public string Scope { get; set; } = string.Empty;
    public string? CourseId { get; set; }
    public string? DocumentId { get; set; }
    public string? DocumentName { get; set; }
    public string? ChapterKey { get; set; }
    public List<string> ChapterKeys { get; set; } = new();
    public string? ChapterTitle { get; set; }
    public string? Subject { get; set; }
    public int TotalChunks { get; set; }
    public int DistinctTopicCount { get; set; }
    public List<TopicSummaryItemDto> Topics { get; set; } = new();
}

public class GenerationSourceDocumentDto
{
    public string DocumentId { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? SubjectCode { get; set; }
    public string? GatekeeperVerdict { get; set; }
    public int ChunkCount { get; set; }
    public int DistinctChapterCount { get; set; }
    public int DistinctTopicCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastEmbeddedAt { get; set; }
}

public class ChapterSummaryItemDto
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

public class ChapterSummaryResultDto
{
    public string Scope { get; set; } = string.Empty;
    public string? CourseId { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string? DocumentName { get; set; }
    public string? Subject { get; set; }
    public int TotalChunks { get; set; }
    public int DistinctChapterCount { get; set; }
    public List<ChapterSummaryItemDto> Chapters { get; set; } = new();
}

public class GeneratedQuestionCandidateDto
{
    public string Type { get; set; } = string.Empty;
    public List<string> TopicTags { get; set; } = new();
    public string Difficulty { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> SourceChunkIds { get; set; } = new();
    public List<Domain.Entities.CodeFile>? SkeletonCode { get; set; }
    public List<Domain.Entities.CodeFile>? SolutionCode { get; set; }
    public List<Domain.Entities.TestCase>? TestCases { get; set; }
    public List<string>? Options { get; set; }
    public List<string>? CorrectAnswer { get; set; }
    public string? Explanation { get; set; }
    public string? PersistedQuestionId { get; set; }
    public string? PersistedStatus { get; set; }
    public bool PersistedIsPublic { get; set; }
}

public class QuestionReviewDecisionDto
{
    public string ReviewStatus { get; set; } = "needs_revision";
    public List<string> Issues { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
    public decimal Score { get; set; }
    public bool SchemaValid { get; set; }
    public bool ContentGrounded { get; set; }
    public bool NeedsRevision { get; set; } = true;
    public string ReviewerModel { get; set; } = string.Empty;
    public string ReviewMode { get; set; } = string.Empty;
}

public class QuestionPersistenceResultDto
{
    public List<string> FeQuestionIds { get; set; } = new();
    public List<string> PeQuestionIds { get; set; } = new();
    public bool Persisted { get; set; }
    public int PersistedCount { get; set; }
    public List<string> PersistedQuestionIds { get; set; } = new();
    public List<string> SkippedReasons { get; set; } = new();
}

public class GenerationReviewRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public string? CourseId { get; set; }
    public string? DocumentId { get; set; }
    public bool IsPublic { get; set; } = true;
    public string Subject { get; set; } = string.Empty;
    public string DifficultyMode { get; set; } = "single";
    public List<DifficultyDistributionItemDto> DifficultyProfile { get; set; } = new();
    public string Difficulty { get; set; } = "Medium";
    public string QuestionType { get; set; } = "FE";
    public int Count { get; set; } = 1;
    public string Mode { get; set; } = "SameModelDualRole";
    public int MaxAttempts { get; set; } = 2;
    public bool PersistQuestions { get; set; }
    public bool SuppressPersistenceSideEffects { get; set; }
    public string? ContextPackId { get; set; }
    public RetrievalPlanRequestDto? Retrieval { get; set; }
    public List<RetrievedChunkDto>? Chunks { get; set; }
    public AIRuntimeOverride? GeneratorOverride { get; set; }
    public AIRuntimeOverride? ReviewerOverride { get; set; }
    public bool UseActualCostOnly { get; set; }
}

public class SystemQuestionGenerationRequestDto
{
    public string CourseId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? ChapterKey { get; set; }
    public List<string> ChapterKeys { get; set; } = new();
    public string DifficultyMode { get; set; } = "single";
    public List<DifficultyDistributionItemDto> DifficultyProfile { get; set; } = new();
    public string Difficulty { get; set; } = "Medium";
    public string QuestionType { get; set; } = "FE";
    public int Count { get; set; } = 1;
    public string Mode { get; set; } = "SameModelDualRole";
    public int MaxAttempts { get; set; } = 2;
    public bool PersistQuestions { get; set; }
    public bool IsPublic { get; set; } = true;
    public List<string> TargetTopics { get; set; } = new();
    public string? RetrievalQuery { get; set; }
    public string? Language { get; set; }
    public int? MaxPackedTokens { get; set; }
    public AIRuntimeOverride? GeneratorOverride { get; set; }
    public AIRuntimeOverride? ReviewerOverride { get; set; }
}

public class ByosQuestionGenerationRequestDto
{
    public string DocumentId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? ChapterKey { get; set; }
    public List<string> ChapterKeys { get; set; } = new();
    public string DifficultyMode { get; set; } = "single";
    public List<DifficultyDistributionItemDto> DifficultyProfile { get; set; } = new();
    public string Difficulty { get; set; } = "Medium";
    public string QuestionType { get; set; } = "FE";
    public int Count { get; set; } = 1;
    public string Mode { get; set; } = "SameModelDualRole";
    public int MaxAttempts { get; set; } = 2;
    public bool PersistQuestions { get; set; }
    public bool IsPublic { get; set; } = true;
    public List<string> TargetTopics { get; set; } = new();
    public string? RetrievalQuery { get; set; }
    public string? Language { get; set; }
    public int? MaxPackedTokens { get; set; }
    public AIRuntimeOverride? GeneratorOverride { get; set; }
    public AIRuntimeOverride? ReviewerOverride { get; set; }
}

public class GenerationReviewResultDto
{
    public string? CourseId { get; set; }
    public string? DocumentId { get; set; }
    public string SourceScope { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string DifficultyMode { get; set; } = "single";
    public List<DifficultyDistributionItemDto> DifficultyProfile { get; set; } = new();
    public List<DifficultyGenerationReportDto> DifficultyReports { get; set; } = new();
    public string Difficulty { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public int AttemptsUsed { get; set; }
    public string GeneratorModel { get; set; } = string.Empty;
    public string ReviewerModel { get; set; } = string.Empty;
    public List<GeneratedQuestionCandidateDto> Questions { get; set; } = new();
    public QuestionReviewDecisionDto Review { get; set; } = new();
    public GenerationShortfallReportDto? ShortfallReport { get; set; }
    public QuestionPersistenceResultDto Persistence { get; set; } = new();
    public ContextPackDto? ContextPack { get; set; }
    public RetrievalPlanResultDto? RetrievalPlan { get; set; }
    public GenerationMetricsDto Metrics { get; set; } = new();
}

public class DifficultyDistributionItemDto
{
    public string Difficulty { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DifficultyGenerationReportDto
{
    public string Difficulty { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int GeneratedCount { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public bool NeedsRevision { get; set; }
    public int PersistedCount { get; set; }
    public GenerationShortfallReportDto? ShortfallReport { get; set; }
}

public class GenerationShortfallReportDto
{
    public int RequestedCount { get; set; }
    public int GeneratedCount { get; set; }
    public string StopReason { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public decimal CoverageRatio { get; set; }
    public int PackedTokenCount { get; set; }
    public string? DominantChapter { get; set; }
    public List<string> CoveredTopics { get; set; } = new();
    public List<string> UncoveredTopics { get; set; } = new();
    public List<string> Notes { get; set; } = new();
    public List<string> SuggestedActions { get; set; } = new();
}
