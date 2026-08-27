using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Application.DTOs.AI;

public sealed record ChunkContextDto(
    string ChunkId,
    string ContentText,
    IReadOnlyList<string> TopicTags,
    string? SectionTitle,
    string Language);

public sealed record RetrievalPlanRequest(
    string UserId,
    string? CourseId,
    string? DocumentId,
    string Subject,
    string QuestionType,
    string Difficulty,
    int RequestedQuestionCount,
    string GenerationMode,
    IReadOnlyList<string> TargetTopics,
    IReadOnlyList<string>? PreferredChapterIds,
    string SourceScope,
    string? Language,
    bool UseCachedPack,
    bool ForceRebuildPack,
    bool IncludeChunkText,
    int MaxCandidateCount,
    int MaxPackedTokens,
    string? RetrievalQuery,
    IReadOnlyList<ChunkContextDto>? SeedChunks = null);

public sealed record ContextPackBuildRequest(
    string UserId,
    string? CourseId,
    string? DocumentId,
    string Subject,
    string QuestionType,
    string Difficulty,
    string PackType,
    string PackStrategy,
    string GenerationMode,
    IReadOnlyList<string> TargetTopics,
    IReadOnlyList<string> ChunkIds,
    int MaxPackedTokens,
    bool RebuildSummary,
    string? Notes);

public sealed record GatekeeperRequest(
    string UserId,
    string FileName,
    string Subject,
    string Language,
    string RawContent,
    string Model);

public sealed record ExtractedContentRequest(
    string UserId,
    string FileName,
    string Subject,
    string Language,
    string RawContent,
    PipelineMode Mode,
    string? VisionModel,
    string? CourseId = null,
    string OwnershipType = "byos",
    string? SourceStoragePath = null,
    long? SourceSizeBytes = null,
    string? SourceChecksum = null);

public sealed record ExtractionDraftContentPatch(
    string ContentId,
    string? ApprovedMarkdown,
    string? ReviewStatus,
    string? ReviewerId);

public sealed record ApproveExtractionDraftRequest(
    string UserId,
    string? ReviewerId,
    bool ApproveAll,
    string? HumanNotes,
    IReadOnlyList<ExtractionDraftContentPatch>? Segments);

public sealed record EmbeddingTaggingRequest(
    string UserId,
    string FileName,
    string Subject,
    string Language,
    string Content,
    PipelineMode Mode,
    string EmbeddingModel,
    string TaggingModel,
    IReadOnlyList<string> AllowedTags);

public sealed record EmbeddingFromDraftRequest(
    string UserId,
    string ExtractionDraftId,
    string EmbeddingModel,
    string TaggingModel,
    IReadOnlyList<string> AllowedTags,
    bool ApprovedOnly = true);

public sealed record IngestionRequest(
    string UserId,
    string FileName,
    string Subject,
    string Language,
    string RawContent,
    PipelineMode Mode,
    string GatekeeperModel,
    string EmbeddingModel,
    string TaggingModel,
    string? VisionModel,
    IReadOnlyList<string> AllowedTags);

public sealed record QuestionGenerationRequest(
    string UserId,
    string? CourseId,
    string? DocumentId,
    bool IsPublic,
    string Subject,
    string Difficulty,
    string QuestionType,
    int Count,
    string GeneratorModel,
    string? RevisionFeedback,
    IReadOnlyList<GeneratedQuestion>? PreviousQuestions,
    IReadOnlyList<ChunkContextDto> Chunks,
    string? ContextPackId = null,
    RetrievalPlanRequest? Retrieval = null);

public sealed record QuestionReviewRequest(
    string UserId,
    string Subject,
    string QuestionType,
    string ReviewerModel,
    IReadOnlyList<GeneratedQuestion> Questions,
    IReadOnlyList<ChunkContextDto> Chunks);

public sealed record GenerationReviewRequest(
    string UserId,
    string? CourseId,
    string? DocumentId,
    bool IsPublic,
    string Subject,
    string Difficulty,
    string QuestionType,
    int Count,
    GenerationReviewMode Mode,
    string GeneratorModel,
    string ReviewerModel,
    int MaxAttempts,
    IReadOnlyList<ChunkContextDto> Chunks,
    string? ContextPackId = null,
    RetrievalPlanRequest? Retrieval = null);

public sealed record CodeMentorRequest(
    string SubmissionId,
    string UserId,
    string Subject,
    string Language,
    string Problem,
    string? Code,
    IReadOnlyList<CodeFile>? SourceFiles,
    string MentorModel);

public sealed record FullPipelineRequest(
    IngestionRequest Ingestion,
    GenerationReviewRequest GenerationReview);

public sealed record QuestionRegressionRunRequest(
    string UserId,
    string? CourseId,
    bool IsPublic,
    string Subject,
    string QuestionType,
    string GeneratorModel,
    string ReviewerModel,
    GenerationReviewMode Mode,
    int MaxAttempts,
    IReadOnlyList<ChunkContextDto> Chunks);
