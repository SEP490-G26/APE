using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Application.Interfaces.AI;

public interface IAiProviderGateway
{
    Task<GatekeeperVerdict> EvaluateGatekeeperAsync(GatekeeperRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<float>> CreateEmbeddingAsync(string content, string model, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> CreateTagsAsync(string content, string subject, string language, IReadOnlyList<string> allowedTags, string model, CancellationToken cancellationToken);
    Task<string> DescribeImageAsync(string imageReference, string model, CancellationToken cancellationToken);
    Task<IReadOnlyList<GeneratedQuestion>> GenerateQuestionsAsync(QuestionGenerationRequest request, CancellationToken cancellationToken);
    Task<ReviewDecision> ReviewQuestionsAsync(QuestionReviewRequest request, CancellationToken cancellationToken);
    Task<MentorFeedback> MentorCodeAsync(CodeMentorRequest request, CancellationToken cancellationToken);
    TokenCostBreakdown EstimateCost(string stageName, string modelName, int inputSize, int outputSize);
    TokenCostBreakdown BuildCostFromUsage(string stageName, string modelName, long inputTokens, long outputTokens, decimal latencyMs);
    TokenCostBreakdown? ConsumeUsageSnapshot(string usageKey);
    NormalizedError? ConsumeErrorSnapshot(string usageKey);
}

public interface IDocumentParser
{
    Task<ExtractedContent> ParseAsync(ExtractedContentRequest request, CancellationToken cancellationToken);
}

public interface IChunkingService
{
    IReadOnlyList<string> BuildChunks(string normalizedMarkdown, PipelineMode mode);
}

public interface IModuleRepository
{
    Task SaveDocumentAsync(StoredDocument document, CancellationToken cancellationToken);
    Task<StoredDocument?> GetDocumentAsync(string documentId, CancellationToken cancellationToken);
    Task SaveGenerationAsync(string documentId, IReadOnlyList<GeneratedQuestion> questions, ReviewDecision review, CancellationToken cancellationToken);
    Task SaveFeQuestionsAsync(string generationId, IReadOnlyList<FeQuestionRecord> questions, ReviewDecision review, CancellationToken cancellationToken);
    Task SavePeQuestionsAsync(string generationId, IReadOnlyList<PeQuestionRecord> questions, ReviewDecision review, CancellationToken cancellationToken);
    Task<IReadOnlyList<QuestionDuplicateCandidate>> FindQuestionDuplicateCandidatesAsync(
        string? courseId,
        string questionType,
        string title,
        string description,
        IReadOnlyList<string> topicTags,
        CancellationToken cancellationToken);
    Task SaveMentorFeedbackAsync(string submissionId, MentorFeedback feedback, CancellationToken cancellationToken);
}

public interface IQuestionSchemaService
{
    QuestionSchemaMappingResult ValidateAndMap(
        string subject,
        string questionType,
        IReadOnlyList<GeneratedQuestion> questions,
        IReadOnlyList<ChunkContextDto> chunks);
}

public interface IQuestionPersistenceMapper
{
    IReadOnlyList<FeQuestionRecord> MapFeQuestions(
        QuestionSchemaMappingResult schemaMapping,
        string userId,
        string pipelineRunId,
        ReviewDecision review,
        string? courseId = null,
        bool isPublic = false);

    IReadOnlyList<PeQuestionRecord> MapPeQuestions(
        QuestionSchemaMappingResult schemaMapping,
        string userId,
        string pipelineRunId,
        ReviewDecision review,
        string? courseId = null,
        bool isPublic = false);
}

public interface IDifficultyAlignmentService
{
    DifficultyAlignmentResult Evaluate(
        string subject,
        string questionType,
        string requestedDifficulty,
        IReadOnlyList<GeneratedQuestion> questions);
}

public interface IRubricCatalogService
{
    Task<IReadOnlyDictionary<string, object>> GetQuestionReviewRubricAsync(
        string subject,
        string questionType,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object>> GetGatekeeperRubricAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object>> GetExtractedContentRubricAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object>> GetEmbeddingTaggingRubricAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object>> GetCodeMentorRubricAsync(
        CancellationToken cancellationToken);
}

public interface IGroundTruthCatalogService
{
    Task<IReadOnlyDictionary<string, object>> GetQuestionGenerationGroundTruthAsync(
        string subject,
        string questionType,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object>> GetGatekeeperGroundTruthAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object>> GetExtractedContentGroundTruthAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object>> GetEmbeddingTaggingGroundTruthAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object>> GetCodeMentorGroundTruthAsync(
        CancellationToken cancellationToken);
}

public interface IGatekeeperPolicyService
{
    Task<IReadOnlyDictionary<string, object>> GetPolicyAsync(CancellationToken cancellationToken);
}

public interface IExtractedContentPolicyService
{
    Task<IReadOnlyDictionary<string, object>> GetPolicyAsync(CancellationToken cancellationToken);
}

public interface IEmbeddingTaggingPolicyService
{
    Task<IReadOnlyDictionary<string, object>> GetPolicyAsync(CancellationToken cancellationToken);
}

public interface ICodeMentorPolicyService
{
    Task<IReadOnlyDictionary<string, object>> GetPolicyAsync(CancellationToken cancellationToken);
}

public interface IAiRunHistoryService
{
    Task<AiRunHistoryRecord> SaveAsync(
        string functionName,
        string routeKey,
        object request,
        object response,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiRunHistorySummary>> ListAsync(
        string? functionName,
        int take,
        CancellationToken cancellationToken);

    Task<AiRunHistoryRecord> GetAsync(
        string functionName,
        string runId,
        CancellationToken cancellationToken);
}

public interface IBenchmarkSessionService
{
    Task<BenchmarkSessionRecord> SaveAsync(
        BenchmarkSessionSaveRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BenchmarkSessionSummary>> ListAsync(
        int take,
        CancellationToken cancellationToken);

    Task<BenchmarkSessionRecord> GetAsync(
        string sessionId,
        CancellationToken cancellationToken);
}

public interface IAiPipelineOrchestrator
{
    Task<GatekeeperResult> RunGatekeeperAsync(GatekeeperRequest request, CancellationToken cancellationToken);
    Task<GatekeeperDebugResult> RunGatekeeperDebugAsync(GatekeeperRequest request, CancellationToken cancellationToken);
    Task<ExtractedContentResult> RunExtractedContentAsync(ExtractedContentRequest request, CancellationToken cancellationToken);
    Task<ExtractedContentDebugResult> RunExtractedContentDebugAsync(ExtractedContentRequest request, CancellationToken cancellationToken);
    Task<EmbeddingTaggingResult> RunEmbeddingTaggingAsync(EmbeddingTaggingRequest request, CancellationToken cancellationToken);
    Task<EmbeddingTaggingDebugResult> RunEmbeddingTaggingDebugAsync(EmbeddingTaggingRequest request, CancellationToken cancellationToken);
    Task<QuestionGenerationResult> RunQuestionGenerationAsync(QuestionGenerationRequest request, CancellationToken cancellationToken);
    Task<ReviewDecision> RunQuestionReviewAsync(QuestionReviewRequest request, CancellationToken cancellationToken);
    Task<IngestionResult> RunIngestionAsync(IngestionRequest request, CancellationToken cancellationToken);
    Task<GenerationReviewResult> RunGenerationReviewAsync(GenerationReviewRequest request, CancellationToken cancellationToken);
    Task<QuestionReviewExportResult> RunGenerationReviewExportAsync(GenerationReviewRequest request, CancellationToken cancellationToken);
    Task<QuestionRegressionRunResult> RunQuestionRegressionAsync(QuestionRegressionRunRequest request, CancellationToken cancellationToken);
    Task<CodeMentorResult> RunCodeMentorAsync(CodeMentorRequest request, CancellationToken cancellationToken);
    Task<CodeMentorDebugResult> RunCodeMentorDebugAsync(CodeMentorRequest request, CancellationToken cancellationToken);
    Task<FullPipelineResult> RunFullPipelineAsync(FullPipelineRequest request, CancellationToken cancellationToken);
}
