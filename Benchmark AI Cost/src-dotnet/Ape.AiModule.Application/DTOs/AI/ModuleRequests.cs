using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Application.DTOs.AI;

public sealed record ChunkContextDto(
    string ChunkId,
    string ContentText,
    IReadOnlyList<string> TopicTags,
    string? SectionTitle,
    string Language);

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
    string? VisionModel);

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
    IReadOnlyList<ChunkContextDto> Chunks);

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
    IReadOnlyList<ChunkContextDto> Chunks);

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
