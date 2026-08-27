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

public interface IExtractionDraftStore
{
    Task<ExtractionDraftEnvelope> SaveAsync(ExtractionDraft draft, IReadOnlyList<ExtractionDraftContent> contents, CancellationToken cancellationToken);
    Task<ExtractionDraftEnvelope?> GetAsync(string draftId, CancellationToken cancellationToken);
    Task<ExtractionDraftEnvelope> ApproveAsync(string draftId, ApproveExtractionDraftRequest request, CancellationToken cancellationToken);
    Task<ExtractionDraftEnvelope> UpdateEmbeddingAsync(string draftId, int chunkCount, string pipelineRunId, CancellationToken cancellationToken);
}

public interface IChunkingService
{
    IReadOnlyList<string> BuildChunks(string normalizedMarkdown, PipelineMode mode);
}

public interface IModuleRepository
{
    Task SaveDocumentAsync(StoredDocument document, CancellationToken cancellationToken);
    Task<StoredDocument?> GetDocumentAsync(string documentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<KnowledgeChunk>> SearchKnowledgeChunksAsync(
        string? courseId,
        string? documentId,
        string subject,
        string userId,
        string sourceScope,
        CancellationToken cancellationToken);
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

public interface IContextPackStore
{
    Task<AIContextPack> SaveAsync(AIContextPack pack, CancellationToken cancellationToken);
    Task<AIContextPack?> GetAsync(string packId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContextPackSummaryResult>> ListAsync(
        string? subject,
        string? questionType,
        string? difficulty,
        string? topic,
        string? status,
        int take,
        CancellationToken cancellationToken);
    Task<AIContextPack> MarkStaleAsync(string packId, CancellationToken cancellationToken);
    Task<AIContextPack> TouchUsageAsync(string packId, CancellationToken cancellationToken);
}

public interface IRetrievalPlannerService
{
    Task<RetrievalPlanResult> PlanAsync(RetrievalPlanRequest request, CancellationToken cancellationToken);
    Task<RetrievalPlanDebugResult> PlanDebugAsync(RetrievalPlanRequest request, CancellationToken cancellationToken);
    Task<ContextPackBuildResult> BuildAsync(ContextPackBuildRequest request, CancellationToken cancellationToken);
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

public interface IAiConfigHistoryService
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, object>>> GetHistoryAsync(
        string artifactType,
        string stableId,
        CancellationToken cancellationToken);
}

public interface IAiReportSyncService
{
    Task<AiReportSyncResult> SyncAsync(
        string trigger,
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
    Task<ExtractionDraftEnvelope?> GetExtractionDraftAsync(string draftId, CancellationToken cancellationToken);
    Task<ExtractionDraftReviewResult> ApproveExtractionDraftAsync(string draftId, ApproveExtractionDraftRequest request, CancellationToken cancellationToken);
    Task<EmbeddingTaggingResult> RunEmbeddingTaggingAsync(EmbeddingTaggingRequest request, CancellationToken cancellationToken);
    Task<EmbeddingTaggingDebugResult> RunEmbeddingTaggingDebugAsync(EmbeddingTaggingRequest request, CancellationToken cancellationToken);
    Task<EmbeddingTaggingResult> RunEmbeddingTaggingFromDraftAsync(EmbeddingFromDraftRequest request, CancellationToken cancellationToken);
    Task<EmbeddingTaggingDebugResult> RunEmbeddingTaggingFromDraftDebugAsync(EmbeddingFromDraftRequest request, CancellationToken cancellationToken);
    Task<RetrievalPlanResult> RunRetrievalPlanAsync(RetrievalPlanRequest request, CancellationToken cancellationToken);
    Task<RetrievalPlanDebugResult> RunRetrievalPlanDebugAsync(RetrievalPlanRequest request, CancellationToken cancellationToken);
    Task<ContextPackBuildResult> RunContextPackBuildAsync(ContextPackBuildRequest request, CancellationToken cancellationToken);
    Task<AIContextPack?> GetContextPackAsync(string packId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContextPackSummaryResult>> ListContextPacksAsync(
        string? subject,
        string? questionType,
        string? difficulty,
        string? topic,
        string? status,
        int take,
        CancellationToken cancellationToken);
    Task<AIContextPack> MarkContextPackStaleAsync(string packId, CancellationToken cancellationToken);
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
