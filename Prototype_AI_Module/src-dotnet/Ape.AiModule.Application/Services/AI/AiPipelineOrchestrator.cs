using System.Text.Json;
using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Application.Services.AI;

public sealed class AiPipelineOrchestrator : IAiPipelineOrchestrator
{
    private readonly IAiProviderGateway _aiProviderGateway;
    private readonly IDocumentParser _documentParser;
    private readonly IChunkingService _chunkingService;
    private readonly IExtractionDraftStore _extractionDraftStore;
    private readonly IModuleRepository _moduleRepository;
    private readonly IQuestionSchemaService _questionSchemaService;
    private readonly IQuestionPersistenceMapper _questionPersistenceMapper;
    private readonly IGatekeeperPolicyService _gatekeeperPolicyService;
    private readonly IExtractedContentPolicyService _extractedContentPolicyService;
    private readonly IEmbeddingTaggingPolicyService _embeddingTaggingPolicyService;
    private readonly ICodeMentorPolicyService _codeMentorPolicyService;
    private readonly IRubricCatalogService _rubricCatalogService;
    private readonly IGroundTruthCatalogService _groundTruthCatalogService;
    private readonly IDifficultyAlignmentService _difficultyAlignmentService;
    private readonly IRetrievalPlannerService _retrievalPlannerService;
    private readonly IContextPackStore _contextPackStore;

    public AiPipelineOrchestrator(
        IAiProviderGateway aiProviderGateway,
        IDocumentParser documentParser,
        IChunkingService chunkingService,
        IExtractionDraftStore extractionDraftStore,
        IModuleRepository moduleRepository,
        IQuestionSchemaService questionSchemaService,
        IQuestionPersistenceMapper questionPersistenceMapper,
        IGatekeeperPolicyService gatekeeperPolicyService,
        IExtractedContentPolicyService extractedContentPolicyService,
        IEmbeddingTaggingPolicyService embeddingTaggingPolicyService,
        ICodeMentorPolicyService codeMentorPolicyService,
        IRubricCatalogService rubricCatalogService,
        IGroundTruthCatalogService groundTruthCatalogService,
        IDifficultyAlignmentService difficultyAlignmentService,
        IRetrievalPlannerService retrievalPlannerService,
        IContextPackStore contextPackStore)
    {
        _aiProviderGateway = aiProviderGateway;
        _documentParser = documentParser;
        _chunkingService = chunkingService;
        _extractionDraftStore = extractionDraftStore;
        _moduleRepository = moduleRepository;
        _questionSchemaService = questionSchemaService;
        _questionPersistenceMapper = questionPersistenceMapper;
        _gatekeeperPolicyService = gatekeeperPolicyService;
        _extractedContentPolicyService = extractedContentPolicyService;
        _embeddingTaggingPolicyService = embeddingTaggingPolicyService;
        _codeMentorPolicyService = codeMentorPolicyService;
        _rubricCatalogService = rubricCatalogService;
        _groundTruthCatalogService = groundTruthCatalogService;
        _difficultyAlignmentService = difficultyAlignmentService;
        _retrievalPlannerService = retrievalPlannerService;
        _contextPackStore = contextPackStore;
    }

    public async Task<GatekeeperResult> RunGatekeeperAsync(GatekeeperRequest request, CancellationToken cancellationToken)
    {
        var gatekeeperPolicy = await _gatekeeperPolicyService.GetPolicyAsync(cancellationToken);
        var precheckVerdict = TryBuildGatekeeperPrecheckVerdict(request, gatekeeperPolicy);
        var verdict = precheckVerdict ?? await _aiProviderGateway.EvaluateGatekeeperAsync(request, cancellationToken);
        var cost = ResolveStageCost("gatekeeper", request.Model, request.RawContent.Length, verdict.Reason.Length, "gatekeeper");
        var stageLogs = new List<PipelineStageLog>
        {
            CreateStageLog("gatekeeper", InferStageStatus(cost), cost, new Dictionary<string, string>
            {
                ["supported"] = verdict.IsSupported.ToString(),
                ["verdict"] = verdict.Verdict,
                ["domain"] = verdict.PrimaryDomain
            }, InferProvider(request.Model), request.Model)
        };
        var usageLogs = new List<AIUsageLogEntry>
        {
            CreateUsageLog(
                triggeredBy: request.UserId,
                agentId: "AI_Gatekeeper",
                pipelineRunId: BuildPipelineRunId("gatekeeper"),
                stageName: "gatekeeper",
                attemptIndex: 1,
                provider: InferProvider(request.Model),
                modelName: request.Model,
                cost: cost,
                status: verdict.IsSupported ? "supported" : "rejected",
                payloadData: new AIUsageLogPayload(
                    "gatekeeper",
                    request.Subject,
                    null,
                    null,
                    verdict.Verdict,
                    new { request.FileName, request.Language, request.RawContent },
                    verdict))
        };

        return new GatekeeperResult(verdict, stageLogs, usageLogs, cost);
    }

    public async Task<GatekeeperDebugResult> RunGatekeeperDebugAsync(GatekeeperRequest request, CancellationToken cancellationToken)
    {
        var result = await RunGatekeeperAsync(request, cancellationToken);
        var policy = await _gatekeeperPolicyService.GetPolicyAsync(cancellationToken);
        var rubric = await _rubricCatalogService.GetGatekeeperRubricAsync(cancellationToken);
        var groundTruth = await TryGetGatekeeperGroundTruthAsync(cancellationToken);

        return new GatekeeperDebugResult(
            result.Verdict,
            policy,
            rubric,
            groundTruth,
            result.StageLogs,
            result.UsageLogs,
            result.Totals);
    }

    public async Task<ExtractedContentResult> RunExtractedContentAsync(ExtractedContentRequest request, CancellationToken cancellationToken)
    {
        var extracted = await _documentParser.ParseAsync(request, cancellationToken);
        var cost = ResolveStageCost("extract-content", request.VisionModel ?? "parser", request.RawContent.Length, extracted.NormalizedMarkdown.Length, "extract-content");
        var draftEnvelope = await BuildAndStoreExtractionDraftAsync(request, extracted, cost, cancellationToken);
        var stageLogs = new List<PipelineStageLog>
        {
            CreateStageLog("extract-content", InferStageStatus(cost), cost, new Dictionary<string, string>
            {
                ["parser"] = extracted.ParserName,
                ["embedded_images"] = extracted.EmbeddedImageCount.ToString(),
                ["words"] = extracted.WordCount.ToString(),
                ["draft_id"] = draftEnvelope.Draft.DraftId,
                ["segments"] = draftEnvelope.Draft.TotalSegments.ToString()
            }, InferProvider(request.VisionModel ?? extracted.ParserName), request.VisionModel ?? extracted.ParserName)
        };
        var usageLogs = new List<AIUsageLogEntry>
        {
            CreateUsageLog(
                triggeredBy: request.UserId,
                agentId: "AI_ExtractedContent",
                pipelineRunId: BuildPipelineRunId("extract"),
                stageName: "extract_content",
                attemptIndex: 1,
                provider: InferProvider(request.VisionModel ?? extracted.ParserName),
                modelName: request.VisionModel ?? extracted.ParserName,
                cost: cost,
                status: "completed",
                payloadData: new AIUsageLogPayload(
                    "extracted_content",
                    request.Subject,
                    null,
                    null,
                    "completed",
                    new { request.FileName, request.Language, request.Mode, request.RawContent },
                    new { extracted, draftEnvelope.Draft, draftEnvelope.Contents }))
        };

        return new ExtractedContentResult(extracted, draftEnvelope.Draft, draftEnvelope.Contents, stageLogs, usageLogs, cost);
    }

    public async Task<ExtractedContentDebugResult> RunExtractedContentDebugAsync(ExtractedContentRequest request, CancellationToken cancellationToken)
    {
        var result = await RunExtractedContentAsync(request, cancellationToken);
        var policy = await _extractedContentPolicyService.GetPolicyAsync(cancellationToken);
        var rubric = await _rubricCatalogService.GetExtractedContentRubricAsync(cancellationToken);
        var groundTruth = await TryGetExtractedContentGroundTruthAsync(cancellationToken);

        return new ExtractedContentDebugResult(
            result.Extracted,
            result.Draft,
            result.Contents,
            policy,
            rubric,
            groundTruth,
            result.StageLogs,
            result.UsageLogs,
            result.Totals);
    }

    public Task<ExtractionDraftEnvelope?> GetExtractionDraftAsync(string draftId, CancellationToken cancellationToken)
        => _extractionDraftStore.GetAsync(draftId, cancellationToken);

    public async Task<ExtractionDraftReviewResult> ApproveExtractionDraftAsync(string draftId, ApproveExtractionDraftRequest request, CancellationToken cancellationToken)
    {
        var envelope = await _extractionDraftStore.ApproveAsync(draftId, request, cancellationToken);
        var cost = new TokenCostBreakdown(0, 0, 0, 0, 0, 0);
        var stageLogs = new List<PipelineStageLog>
        {
            CreateStageLog("extraction-review", RunStatus.Completed, cost, new Dictionary<string, string>
            {
                ["draft_id"] = envelope.Draft.DraftId,
                ["review_status"] = envelope.Draft.ReviewStatus,
                ["approved_segments"] = envelope.Draft.ApprovedSegments.ToString(),
                ["rejected_segments"] = envelope.Draft.RejectedSegments.ToString()
            }, "human", "human-review")
        };
        var usageLogs = new List<AIUsageLogEntry>();
        return new ExtractionDraftReviewResult(envelope.Draft, envelope.Contents, stageLogs, usageLogs, cost);
    }

    public async Task<EmbeddingTaggingResult> RunEmbeddingTaggingAsync(EmbeddingTaggingRequest request, CancellationToken cancellationToken)
    {
        var policy = await _embeddingTaggingPolicyService.GetPolicyAsync(cancellationToken);
        var chunkTexts = _chunkingService.BuildChunks(request.Content, request.Mode);
        var stageLogs = new List<PipelineStageLog>();
        var usageLogs = new List<AIUsageLogEntry>();
        var chunks = new List<KnowledgeChunk>();
        var embeddingCosts = new List<TokenCostBreakdown>();
        var taggingCosts = new List<TokenCostBreakdown>();
        var documentId = $"doc-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];
        var pipelineRunId = BuildPipelineRunId("embedtag");
        var sourceType = DetectSourceType(request.FileName);
        var subjectCode = NormalizeSubjectCode(request.Subject);
        var chunkingStrategy = request.Mode == PipelineMode.FullMultimodalPage
            ? TryReadString(policy, "chunk_defaults", "chunking_strategy_multimodal") ?? "page_block"
            : TryReadString(policy, "chunk_defaults", "chunking_strategy_text_only") ?? "logical_block";
        var retrievalEnabled = TryReadBool(policy, "chunk_defaults", "retrieval_enabled") ?? true;
        var defaultStatus = TryReadString(policy, "chunk_defaults", "default_status") ?? "active";
        var fallbackChunkType = TryReadString(policy, "tagging_rules", "fallback_chunk_type") ?? "paragraph";
        var fallbackSectionTitle = TryReadString(policy, "tagging_rules", "fallback_section_title") ?? "Untitled Section";

        for (var index = 0; index < chunkTexts.Count; index++)
        {
            var chunkText = chunkTexts[index];
            var embedding = await _aiProviderGateway.CreateEmbeddingAsync(chunkText, request.EmbeddingModel, cancellationToken);
            var tags = await _aiProviderGateway.CreateTagsAsync(chunkText, request.Subject, request.Language, request.AllowedTags, request.TaggingModel, cancellationToken);
            var embeddingCost = ResolveStageCost("embedding", request.EmbeddingModel, chunkText.Length, 0, "embedding");
            var taggingCost = ResolveStageCost("auto-tagging", request.TaggingModel, chunkText.Length, tags.Sum(static item => item.Length), "auto-tagging");
            embeddingCosts.Add(embeddingCost);
            taggingCosts.Add(taggingCost);
            var normalizedText = NormalizeChunkText(chunkText);
            var markdownText = chunkText;
            var sectionTitle = DetectSectionTitle(chunkText) ?? fallbackSectionTitle;
            var chunkType = DetectChunkType(chunkText, fallbackChunkType);
            var now = DateTimeOffset.UtcNow;
            var wordCount = CountWords(normalizedText);
            var tokenCount = (int)Math.Max(1, Math.Ceiling(normalizedText.Length / 4.0));
            var charCount = normalizedText.Length;

            var chunk = new KnowledgeChunk(
                $"chunk-{index + 1:D4}",
                null,
                documentId,
                request.UserId,
                null,
                null,
                index,
                chunkingStrategy,
                chunkType,
                sectionTitle,
                sourceType,
                request.FileName,
                index + 1,
                index + 1,
                request.Language,
                subjectCode,
                chunkText,
                normalizedText,
                markdownText,
                tags,
                embedding,
                InferProvider(request.EmbeddingModel),
                request.EmbeddingModel,
                embedding.Count,
                InferProvider(request.TaggingModel),
                request.TaggingModel,
                wordCount,
                tokenCount,
                charCount,
                retrievalEnabled,
                defaultStatus,
                now,
                now);

            chunks.Add(chunk);

            stageLogs.Add(CreateStageLog(
                $"chunk-{index + 1}-embedding",
                InferStageStatus(embeddingCost),
                embeddingCost,
                new Dictionary<string, string>
                {
                    ["chunk_id"] = chunk.Id,
                    ["embedding_dim"] = chunk.EmbeddingDim.ToString()
                },
                InferProvider(request.EmbeddingModel),
                request.EmbeddingModel));

            stageLogs.Add(CreateStageLog(
                $"chunk-{index + 1}-auto-tagging",
                InferStageStatus(taggingCost),
                taggingCost,
                new Dictionary<string, string>
                {
                    ["chunk_id"] = chunk.Id,
                    ["tags"] = string.Join(", ", chunk.TopicTags)
                },
                InferProvider(request.TaggingModel),
                request.TaggingModel));

            usageLogs.Add(CreateUsageLog(
                triggeredBy: request.UserId,
                agentId: "AI_Embedding",
                pipelineRunId: pipelineRunId,
                stageName: "embedding",
                attemptIndex: index + 1,
                provider: InferProvider(request.EmbeddingModel),
                modelName: request.EmbeddingModel,
                cost: embeddingCost,
                status: "embedded",
                payloadData: new AIUsageLogPayload(
                    "embedding_tagging",
                    request.Subject,
                    null,
                    null,
                    "embedded",
                    new { request.FileName, request.Language, request.Mode, chunk.Id, chunk.RawText },
                    new { chunk.Id, chunk.EmbeddingDim })));

            usageLogs.Add(CreateUsageLog(
                triggeredBy: request.UserId,
                agentId: "AI_AutoTagging",
                pipelineRunId: pipelineRunId,
                stageName: "auto_tagging",
                attemptIndex: index + 1,
                provider: InferProvider(request.TaggingModel),
                modelName: request.TaggingModel,
                cost: taggingCost,
                status: "tagged",
                payloadData: new AIUsageLogPayload(
                    "embedding_tagging",
                    request.Subject,
                    null,
                    null,
                    "tagged",
                    new { request.AllowedTags, chunk.Id, chunk.RawText },
                    new { chunk.Id, chunk.TopicTags })));
        }

        return new EmbeddingTaggingResult(
            documentId,
            null,
            chunks,
            SumCosts(embeddingCosts),
            SumCosts(taggingCosts),
            stageLogs,
            usageLogs,
            SumCosts(stageLogs.Select(static item => item.Cost)));
    }

    public async Task<EmbeddingTaggingDebugResult> RunEmbeddingTaggingDebugAsync(EmbeddingTaggingRequest request, CancellationToken cancellationToken)
    {
        var result = await RunEmbeddingTaggingAsync(request, cancellationToken);
        var policy = await _embeddingTaggingPolicyService.GetPolicyAsync(cancellationToken);
        var rubric = await _rubricCatalogService.GetEmbeddingTaggingRubricAsync(cancellationToken);
        var groundTruth = await TryGetEmbeddingTaggingGroundTruthAsync(cancellationToken);

        return new EmbeddingTaggingDebugResult(
            result.DocumentId,
            result.ExtractionDraftId,
            result.Chunks,
            policy,
            rubric,
            groundTruth,
            result.EmbeddingTotals,
            result.TaggingTotals,
            result.StageLogs,
            result.UsageLogs,
            result.Totals);
    }

    public async Task<EmbeddingTaggingResult> RunEmbeddingTaggingFromDraftAsync(EmbeddingFromDraftRequest request, CancellationToken cancellationToken)
    {
        var envelope = await _extractionDraftStore.GetAsync(request.ExtractionDraftId, cancellationToken)
            ?? throw new InvalidOperationException($"Extraction draft '{request.ExtractionDraftId}' was not found.");

        var selectedContents = envelope.Contents
            .Where(content => !request.ApprovedOnly || string.Equals(content.ReviewStatus, "approved", StringComparison.OrdinalIgnoreCase))
            .Where(content => !string.IsNullOrWhiteSpace(content.ApprovedMarkdown ?? content.CleanMarkdown))
            .OrderBy(static item => item.SegmentIndex)
            .ToList();

        if (selectedContents.Count == 0)
        {
            throw new InvalidOperationException("No approved extraction segments are available for embedding.");
        }

        var policy = await _embeddingTaggingPolicyService.GetPolicyAsync(cancellationToken);
        var stageLogs = new List<PipelineStageLog>();
        var usageLogs = new List<AIUsageLogEntry>();
        var chunks = new List<KnowledgeChunk>();
        var embeddingCosts = new List<TokenCostBreakdown>();
        var taggingCosts = new List<TokenCostBreakdown>();
        var pipelineRunId = BuildPipelineRunId("embedtag");
        var sourceType = DetectSourceType(envelope.Draft.SourceName);
        var subjectCode = NormalizeSubjectCode(envelope.Draft.SubjectCode);
        var chunkingStrategy = "logical_block";
        var retrievalEnabled = TryReadBool(policy, "chunk_defaults", "retrieval_enabled") ?? true;
        var defaultStatus = TryReadString(policy, "chunk_defaults", "default_status") ?? "active";
        var fallbackChunkType = TryReadString(policy, "tagging_rules", "fallback_chunk_type") ?? "paragraph";
        var fallbackSectionTitle = TryReadString(policy, "tagging_rules", "fallback_section_title") ?? "Untitled Section";
        var chunkIndex = 0;

        foreach (var content in selectedContents)
        {
            var contentText = content.ApprovedMarkdown ?? content.CleanMarkdown;
            var chunkTexts = _chunkingService.BuildChunks(contentText, PipelineMode.TextOnly);
            foreach (var chunkText in chunkTexts)
            {
                var embedding = await _aiProviderGateway.CreateEmbeddingAsync(chunkText, request.EmbeddingModel, cancellationToken);
                var tags = await _aiProviderGateway.CreateTagsAsync(chunkText, envelope.Draft.SubjectCode, envelope.Draft.Language, request.AllowedTags, request.TaggingModel, cancellationToken);
                var embeddingCost = ResolveStageCost("embedding", request.EmbeddingModel, chunkText.Length, 0, "embedding");
                var taggingCost = ResolveStageCost("auto-tagging", request.TaggingModel, chunkText.Length, tags.Sum(static item => item.Length), "auto-tagging");
                embeddingCosts.Add(embeddingCost);
                taggingCosts.Add(taggingCost);
                var normalizedText = NormalizeChunkText(chunkText);
                var markdownText = chunkText;
                var sectionTitle = content.SectionTitle ?? DetectSectionTitle(chunkText) ?? fallbackSectionTitle;
                var chunkType = DetectChunkType(chunkText, fallbackChunkType);
                var now = DateTimeOffset.UtcNow;
                var wordCount = CountWords(normalizedText);
                var tokenCount = (int)Math.Max(1, Math.Ceiling(normalizedText.Length / 4.0));
                var charCount = normalizedText.Length;

                var chunk = new KnowledgeChunk(
                    $"chunk-{chunkIndex + 1:D4}",
                    envelope.Draft.CourseId,
                    envelope.Draft.DocumentId,
                    envelope.Draft.UserId,
                    envelope.Draft.DraftId,
                    content.ContentId,
                    chunkIndex,
                    chunkingStrategy,
                    chunkType,
                    sectionTitle,
                    sourceType,
                    envelope.Draft.SourceName,
                    content.SourcePageFrom,
                    content.SourcePageTo,
                    envelope.Draft.Language,
                    subjectCode,
                    chunkText,
                    normalizedText,
                    markdownText,
                    tags,
                    embedding,
                    InferProvider(request.EmbeddingModel),
                    request.EmbeddingModel,
                    embedding.Count,
                    InferProvider(request.TaggingModel),
                    request.TaggingModel,
                    wordCount,
                    tokenCount,
                    charCount,
                    retrievalEnabled,
                    defaultStatus,
                    now,
                    now);

                chunks.Add(chunk);
                stageLogs.Add(CreateStageLog(
                    $"segment-{content.SegmentIndex + 1}-chunk-{chunkIndex + 1}-embedding",
                    InferStageStatus(embeddingCost),
                    embeddingCost,
                    new Dictionary<string, string>
                    {
                        ["chunk_id"] = chunk.Id,
                        ["draft_id"] = envelope.Draft.DraftId,
                        ["content_id"] = content.ContentId
                    },
                    InferProvider(request.EmbeddingModel),
                    request.EmbeddingModel));
                stageLogs.Add(CreateStageLog(
                    $"segment-{content.SegmentIndex + 1}-chunk-{chunkIndex + 1}-auto-tagging",
                    InferStageStatus(taggingCost),
                    taggingCost,
                    new Dictionary<string, string>
                    {
                        ["chunk_id"] = chunk.Id,
                        ["content_id"] = content.ContentId,
                        ["tags"] = string.Join(", ", chunk.TopicTags)
                    },
                    InferProvider(request.TaggingModel),
                    request.TaggingModel));

                usageLogs.Add(CreateUsageLog(
                    triggeredBy: request.UserId,
                    agentId: "AI_Embedding",
                    pipelineRunId: pipelineRunId,
                    stageName: "embedding",
                    attemptIndex: chunkIndex + 1,
                    provider: InferProvider(request.EmbeddingModel),
                    modelName: request.EmbeddingModel,
                    cost: embeddingCost,
                    status: "embedded",
                    payloadData: new AIUsageLogPayload(
                        "embedding_tagging",
                        envelope.Draft.SubjectCode,
                        null,
                        null,
                        "embedded",
                        new { envelope.Draft.DraftId, content.ContentId, chunk.Id, chunk.RawText },
                        new { chunk.Id, chunk.EmbeddingDim })));

                usageLogs.Add(CreateUsageLog(
                    triggeredBy: request.UserId,
                    agentId: "AI_AutoTagging",
                    pipelineRunId: pipelineRunId,
                    stageName: "auto_tagging",
                    attemptIndex: chunkIndex + 1,
                    provider: InferProvider(request.TaggingModel),
                    modelName: request.TaggingModel,
                    cost: taggingCost,
                    status: "tagged",
                    payloadData: new AIUsageLogPayload(
                        "embedding_tagging",
                        envelope.Draft.SubjectCode,
                        null,
                        null,
                        "tagged",
                        new { envelope.Draft.DraftId, content.ContentId, request.AllowedTags, chunk.Id, chunk.RawText },
                        new { chunk.Id, chunk.TopicTags })));

                chunkIndex++;
            }
        }

        await _extractionDraftStore.UpdateEmbeddingAsync(envelope.Draft.DraftId, chunks.Count, pipelineRunId, cancellationToken);
        return new EmbeddingTaggingResult(
            envelope.Draft.DocumentId,
            envelope.Draft.DraftId,
            chunks,
            SumCosts(embeddingCosts),
            SumCosts(taggingCosts),
            stageLogs,
            usageLogs,
            SumCosts(stageLogs.Select(static item => item.Cost)));
    }

    public async Task<EmbeddingTaggingDebugResult> RunEmbeddingTaggingFromDraftDebugAsync(EmbeddingFromDraftRequest request, CancellationToken cancellationToken)
    {
        var result = await RunEmbeddingTaggingFromDraftAsync(request, cancellationToken);
        var policy = await _embeddingTaggingPolicyService.GetPolicyAsync(cancellationToken);
        var rubric = await _rubricCatalogService.GetEmbeddingTaggingRubricAsync(cancellationToken);
        var groundTruth = await TryGetEmbeddingTaggingGroundTruthAsync(cancellationToken);

        return new EmbeddingTaggingDebugResult(
            result.DocumentId,
            result.ExtractionDraftId,
            result.Chunks,
            policy,
            rubric,
            groundTruth,
            result.EmbeddingTotals,
            result.TaggingTotals,
            result.StageLogs,
            result.UsageLogs,
            result.Totals);
    }

    public Task<RetrievalPlanResult> RunRetrievalPlanAsync(RetrievalPlanRequest request, CancellationToken cancellationToken)
        => _retrievalPlannerService.PlanAsync(request, cancellationToken);

    public Task<RetrievalPlanDebugResult> RunRetrievalPlanDebugAsync(RetrievalPlanRequest request, CancellationToken cancellationToken)
        => _retrievalPlannerService.PlanDebugAsync(request, cancellationToken);

    public Task<ContextPackBuildResult> RunContextPackBuildAsync(ContextPackBuildRequest request, CancellationToken cancellationToken)
        => _retrievalPlannerService.BuildAsync(request, cancellationToken);

    public Task<AIContextPack?> GetContextPackAsync(string packId, CancellationToken cancellationToken)
        => _contextPackStore.GetAsync(packId, cancellationToken);

    public Task<IReadOnlyList<ContextPackSummaryResult>> ListContextPacksAsync(
        string? subject,
        string? questionType,
        string? difficulty,
        string? topic,
        string? status,
        int take,
        CancellationToken cancellationToken)
        => _contextPackStore.ListAsync(subject, questionType, difficulty, topic, status, take, cancellationToken);

    public Task<AIContextPack> MarkContextPackStaleAsync(string packId, CancellationToken cancellationToken)
        => _contextPackStore.MarkStaleAsync(packId, cancellationToken);

    public async Task<QuestionGenerationResult> RunQuestionGenerationAsync(QuestionGenerationRequest request, CancellationToken cancellationToken)
    {
        var chunkResolution = await ResolveQuestionChunksAsync(
            request.UserId,
            request.Subject,
            request.QuestionType,
            request.Difficulty,
            request.Count,
            request.ContextPackId,
            request.Retrieval,
            request.Chunks,
            cancellationToken);
        var effectiveChunks = chunkResolution.Chunks;

        var effectiveRequest = request with { Chunks = effectiveChunks };
        var questions = await _aiProviderGateway.GenerateQuestionsAsync(effectiveRequest, cancellationToken);
        var schemaMapping = await EnrichSchemaMappingWithRepositoryDuplicatesAsync(
            request.CourseId,
            _questionSchemaService.ValidateAndMap(request.Subject, request.QuestionType, questions, effectiveChunks),
            cancellationToken);
        var difficultyAlignment = _difficultyAlignmentService.Evaluate(request.Subject, request.QuestionType, request.Difficulty, questions);
        var mappedFeQuestions = _questionPersistenceMapper.MapFeQuestions(schemaMapping, request.UserId, BuildPipelineRunId("qgenmap"), new ReviewDecision("draft", [], [], 0, schemaMapping.IsValid, true, !schemaMapping.IsValid, request.GeneratorModel), request.CourseId, request.IsPublic);
        var mappedPeQuestions = _questionPersistenceMapper.MapPeQuestions(schemaMapping, request.UserId, BuildPipelineRunId("qgenmap"), new ReviewDecision("draft", [], [], 0, schemaMapping.IsValid, true, !schemaMapping.IsValid, request.GeneratorModel), request.CourseId, request.IsPublic);
        var cost = ResolveStageCost(
            "question-generation",
            request.GeneratorModel,
            effectiveChunks.Sum(static chunk => chunk.ContentText.Length),
            JsonSerializer.Serialize(questions).Length,
            "question-generation");
        var stageLogs = new List<PipelineStageLog>();
        if (chunkResolution.RetrievalPlan is not null)
        {
            stageLogs.AddRange(chunkResolution.RetrievalPlan.StageLogs);
        }

        stageLogs.AddRange(
        [
            CreateStageLog("question-generation", InferStageStatus(cost), cost, new Dictionary<string, string>
            {
                ["question_type"] = request.QuestionType,
                ["difficulty"] = request.Difficulty,
                ["count"] = questions.Count.ToString(),
                ["chunk_count"] = effectiveChunks.Count.ToString(),
                ["context_pack_id"] = chunkResolution.ContextPack?.PackId ?? string.Empty,
                ["schema_valid"] = schemaMapping.IsValid.ToString(),
                ["schema_issue_count"] = schemaMapping.Issues.Count.ToString(),
                ["difficulty_aligned"] = difficultyAlignment.IsAligned.ToString(),
                ["estimated_difficulty"] = difficultyAlignment.EstimatedDifficulty,
                ["duplicate_issue_count"] = schemaMapping.Issues.Count(issue => issue.Code == "duplicate_existing_question").ToString()
            }, InferProvider(request.GeneratorModel), request.GeneratorModel)
        ]);
        var usageLogs = new List<AIUsageLogEntry>();
        if (chunkResolution.RetrievalPlan is not null)
        {
            usageLogs.AddRange(chunkResolution.RetrievalPlan.UsageLogs);
        }

        usageLogs.AddRange(
        [
            CreateUsageLog(
                triggeredBy: request.UserId,
                agentId: "AI_Generator",
                pipelineRunId: BuildPipelineRunId("qgen"),
                stageName: "question_generation",
                attemptIndex: 1,
                provider: InferProvider(request.GeneratorModel),
                modelName: request.GeneratorModel,
                cost: cost,
                status: "generated",
                payloadData: new AIUsageLogPayload(
                    "question_generation",
                    request.Subject,
                    request.QuestionType,
                    request.Difficulty,
                    "generated",
                    new { request.CourseId, request.DocumentId, request.IsPublic, request.Count, request.RevisionFeedback, request.PreviousQuestions, EffectiveChunks = effectiveChunks, ContextPackId = chunkResolution.ContextPack?.PackId, Retrieval = request.Retrieval },
                    questions))
        ]);

        return new QuestionGenerationResult(questions, schemaMapping, difficultyAlignment, mappedFeQuestions, mappedPeQuestions, stageLogs, usageLogs, cost, chunkResolution.ContextPack, chunkResolution.RetrievalPlan);
    }

    public Task<ReviewDecision> RunQuestionReviewAsync(QuestionReviewRequest request, CancellationToken cancellationToken)
        => _aiProviderGateway.ReviewQuestionsAsync(request, cancellationToken);

    public async Task<IngestionResult> RunIngestionAsync(IngestionRequest request, CancellationToken cancellationToken)
    {
        var gatekeeperResult = await RunGatekeeperAsync(new GatekeeperRequest(request.UserId, request.FileName, request.Subject, request.Language, request.RawContent, request.GatekeeperModel), cancellationToken);
        var extractedResult = await RunExtractedContentAsync(new ExtractedContentRequest(request.UserId, request.FileName, request.Subject, request.Language, request.RawContent, request.Mode, request.VisionModel), cancellationToken);
        await ApproveExtractionDraftAsync(extractedResult.Draft.DraftId, new ApproveExtractionDraftRequest(request.UserId, request.UserId, true, "Auto-approved by ingestion pipeline.", null), cancellationToken);
        var embeddingResult = await RunEmbeddingTaggingFromDraftAsync(new EmbeddingFromDraftRequest(request.UserId, extractedResult.Draft.DraftId, request.EmbeddingModel, request.TaggingModel, request.AllowedTags), cancellationToken);

        var storedDocument = new StoredDocument(
            embeddingResult.DocumentId,
            request.UserId,
            request.FileName,
            request.Subject,
            request.Language,
            DateTimeOffset.UtcNow,
            gatekeeperResult.Verdict,
            extractedResult.Extracted,
            embeddingResult.Chunks);

        await _moduleRepository.SaveDocumentAsync(storedDocument, cancellationToken);

        var logs = gatekeeperResult.StageLogs.Concat(extractedResult.StageLogs).Concat(embeddingResult.StageLogs).ToList();
        var usageLogs = gatekeeperResult.UsageLogs.Concat(extractedResult.UsageLogs).Concat(embeddingResult.UsageLogs).ToList();
        return new IngestionResult(storedDocument, logs, usageLogs, SumCosts(logs.Select(static item => item.Cost)));
    }

    public async Task<GenerationReviewResult> RunGenerationReviewAsync(GenerationReviewRequest request, CancellationToken cancellationToken)
    {
        var chunkResolution = await ResolveQuestionChunksAsync(
            request.UserId,
            request.Subject,
            request.QuestionType,
            request.Difficulty,
            request.Count,
            request.ContextPackId,
            request.Retrieval,
            request.Chunks,
            cancellationToken);
        var effectiveChunks = chunkResolution.Chunks;

        var questionRubric = await _rubricCatalogService.GetQuestionReviewRubricAsync(request.Subject, request.QuestionType, cancellationToken);
        var stageLogs = new List<PipelineStageLog>();
        var usageLogs = new List<AIUsageLogEntry>();
        if (chunkResolution.RetrievalPlan is not null)
        {
            stageLogs.AddRange(chunkResolution.RetrievalPlan.StageLogs);
            usageLogs.AddRange(chunkResolution.RetrievalPlan.UsageLogs);
        }
        IReadOnlyList<GeneratedQuestion> questions = [];
        ReviewDecision review = new("needs_revision", ["Generation did not run."], ["Retry the generation flow."], 0, false, false, true, request.ReviewerModel);
        var attemptsToUse = request.Mode == GenerationReviewMode.SingleAgent
            ? 1
            : Math.Clamp(request.MaxAttempts <= 0 ? 2 : request.MaxAttempts, 1, 2);
        string? revisionFeedback = null;
        IReadOnlyList<GeneratedQuestion>? previousQuestions = null;
        var attemptsUsed = 0;
        var pipelineRunId = $"genrev-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
        var reviewerModel = request.Mode switch
        {
            GenerationReviewMode.SingleAgent => request.GeneratorModel,
            GenerationReviewMode.SameModelDualRole => request.GeneratorModel,
            _ => request.ReviewerModel
        };

        for (var attempt = 1; attempt <= attemptsToUse; attempt++)
        {
            attemptsUsed = attempt;
            var generation = await RunQuestionGenerationAsync(new QuestionGenerationRequest(
                request.UserId,
                request.CourseId,
                request.DocumentId,
                request.IsPublic,
                request.Subject,
                request.Difficulty,
                request.QuestionType,
                request.Count,
                request.GeneratorModel,
                revisionFeedback,
                previousQuestions,
                effectiveChunks,
                chunkResolution.ContextPack?.PackId), cancellationToken);

            stageLogs.AddRange(generation.StageLogs.Select(log => log with
            {
                StageName = $"{log.StageName}-attempt-{attempt}"
            }));
            usageLogs.Add(CreateUsageLog(
                triggeredBy: request.UserId,
                agentId: "AI_Generator",
                pipelineRunId: pipelineRunId,
                stageName: "question_generation",
                attemptIndex: attempt,
                provider: InferProvider(request.GeneratorModel),
                modelName: request.GeneratorModel,
                cost: generation.Totals,
                status: "generated",
                payloadData: new AIUsageLogPayload(
                    "generation_review",
                    request.Subject,
                    request.QuestionType,
                    request.Difficulty,
                    "generated",
                    new
                    {
                        request.Mode,
                        request.Count,
                        effectiveChunks,
                        chunkResolution.ContextPack?.PackId,
                        revisionFeedback,
                        previousQuestions
                    },
                    generation.Questions)));

            questions = generation.Questions;
            var schemaMapping = generation.SchemaMapping;
            var difficultyAlignment = generation.DifficultyAlignment;
            if (request.Mode == GenerationReviewMode.SingleAgent)
            {
                if (!schemaMapping.IsValid)
                {
                    review = new ReviewDecision(
                        "needs_revision",
                        schemaMapping.Issues.Select(issue => issue.Message).ToList(),
                        ["Fix schema violations before accepting question output."],
                        0.3m,
                        false,
                        true,
                        true,
                        reviewerModel);
                }
                else
                {
                    review = new ReviewDecision("accepted", [], ["Single agent mode skips reviewer."], 1.0m, true, true, false, reviewerModel);
                }
            }
            else
            {
                review = await RunQuestionReviewAsync(new QuestionReviewRequest(
                    request.UserId,
                    request.Subject,
                    request.QuestionType,
                    reviewerModel,
                    questions,
                    effectiveChunks), cancellationToken);
                if (!schemaMapping.IsValid)
                {
                    var mergedIssues = review.Issues.Concat(schemaMapping.Issues.Select(issue => issue.Message)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var mergedSuggestions = review.Suggestions.Concat(["Fix schema violations before retrying generation."]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    review = review with
                    {
                        ReviewStatus = "needs_revision",
                        Issues = mergedIssues,
                        Suggestions = mergedSuggestions,
                        Score = Math.Min(review.Score, 0.4m),
                        SchemaValid = false,
                        NeedsRevision = true
                    };
                }
            }

            if (!difficultyAlignment.IsAligned)
            {
                var difficultyIssue = $"Difficulty alignment mismatch: requested '{difficultyAlignment.RequestedDifficulty}' but estimated '{difficultyAlignment.EstimatedDifficulty}'.";
                var isSevereDifficultyMismatch = IsSevereDifficultyMismatch(difficultyAlignment.RequestedDifficulty, difficultyAlignment.EstimatedDifficulty);
                var mergedIssues = isSevereDifficultyMismatch
                    ? review.Issues.Concat([difficultyIssue]).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    : review.Issues;
                var mergedSuggestions = review.Suggestions
                    .Concat([isSevereDifficultyMismatch
                        ? "Adjust the generated question so that the reasoning load matches the requested difficulty."
                        : "Difficulty estimate is slightly above or below the requested level. Consider minor scope tuning if consistency across the bank matters."])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                review = review with
                {
                    ReviewStatus = isSevereDifficultyMismatch ? "needs_revision" : review.ReviewStatus,
                    Issues = mergedIssues,
                    Suggestions = mergedSuggestions,
                    Score = Math.Min(review.Score, isSevereDifficultyMismatch ? 0.55m : 0.82m),
                    NeedsRevision = isSevereDifficultyMismatch || review.NeedsRevision
                };
            }

            var ruleBasedReview = BuildRuleBasedQuestionReview(
                request.Subject,
                request.QuestionType,
                request.Difficulty,
                questions,
                request.Chunks,
                questionRubric,
                schemaMapping,
                difficultyAlignment);
            review = MergeReviewDecisions(review, ruleBasedReview);
            review = NormalizeReviewDecisionForMildDifficultyMismatch(review, difficultyAlignment);

            var reviewCost = request.Mode == GenerationReviewMode.SingleAgent
                ? new TokenCostBreakdown(0, 0, 0, 0, 0, 0)
                : ResolveStageCost("question-review", reviewerModel, JsonSerializer.Serialize(questions).Length, JsonSerializer.Serialize(review).Length, "question-review");
            stageLogs.Add(CreateStageLog(
                $"question-review-attempt-{attempt}",
                InferStageStatus(reviewCost),
                reviewCost,
                new Dictionary<string, string>
                {
                    ["review_status"] = review.ReviewStatus,
                    ["issues_count"] = review.Issues.Count.ToString(),
                    ["score"] = review.Score.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["schema_valid"] = review.SchemaValid.ToString(),
                    ["content_grounded"] = review.ContentGrounded.ToString()
                },
                InferProvider(reviewerModel),
                reviewerModel));
            if (request.Mode != GenerationReviewMode.SingleAgent)
            {
                usageLogs.Add(CreateUsageLog(
                    triggeredBy: request.UserId,
                    agentId: "AI_Reviewer",
                    pipelineRunId: pipelineRunId,
                    stageName: "question_review",
                    attemptIndex: attempt,
                    provider: InferProvider(reviewerModel),
                    modelName: reviewerModel,
                    cost: reviewCost,
                    status: review.ReviewStatus,
                    payloadData: new AIUsageLogPayload(
                        "generation_review",
                        request.Subject,
                        request.QuestionType,
                        request.Difficulty,
                        review.ReviewStatus,
                        new
                        {
                        request.Mode,
                        effectiveChunks,
                        questions
                    },
                        review)));
            }
            var ruleReviewCost = new TokenCostBreakdown(0, 0, 0, 0, 0, 0);
            stageLogs.Add(CreateStageLog(
                $"question-rule-review-attempt-{attempt}",
                RunStatus.Completed,
                ruleReviewCost,
                new Dictionary<string, string>
                {
                    ["review_status"] = ruleBasedReview.ReviewStatus,
                    ["issues_count"] = ruleBasedReview.Issues.Count.ToString(),
                    ["score"] = ruleBasedReview.Score.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["schema_valid"] = ruleBasedReview.SchemaValid.ToString(),
                    ["content_grounded"] = ruleBasedReview.ContentGrounded.ToString()
                },
                "system",
                "rule-based"));
            usageLogs.Add(CreateUsageLog(
                triggeredBy: request.UserId,
                agentId: "AI_RuleReviewer",
                pipelineRunId: pipelineRunId,
                stageName: "question_rule_review",
                attemptIndex: attempt,
                provider: "system",
                modelName: "rule-based",
                cost: ruleReviewCost,
                status: ruleBasedReview.ReviewStatus,
                payloadData: new AIUsageLogPayload(
                    "generation_review_rule_based",
                    request.Subject,
                    request.QuestionType,
                    request.Difficulty,
                    ruleBasedReview.ReviewStatus,
                    new
                    {
                        request.Mode,
                        effectiveChunks,
                        questions,
                        rubric = questionRubric
                    },
                    ruleBasedReview)));

            if (!review.NeedsRevision && review.ReviewStatus.Equals("accepted", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            revisionFeedback = string.Join("\n", review.Issues.Concat(review.Suggestions));
            previousQuestions = questions;
        }

        var finalSchemaMapping = await EnrichSchemaMappingWithRepositoryDuplicatesAsync(
            request.CourseId,
            _questionSchemaService.ValidateAndMap(request.Subject, request.QuestionType, questions, effectiveChunks),
            cancellationToken);
        var generationId = $"{request.QuestionType.ToLowerInvariant()}-generation:{request.UserId}:{request.Subject}:{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        await _moduleRepository.SaveGenerationAsync(generationId, questions, review, cancellationToken);
        if (finalSchemaMapping.FeQuestions.Count > 0)
        {
            var feRecords = _questionPersistenceMapper.MapFeQuestions(finalSchemaMapping, request.UserId, pipelineRunId, review, request.CourseId, request.IsPublic);
            await _moduleRepository.SaveFeQuestionsAsync(generationId, feRecords, review, cancellationToken);
        }

        if (finalSchemaMapping.PeQuestions.Count > 0)
        {
            var peRecords = _questionPersistenceMapper.MapPeQuestions(finalSchemaMapping, request.UserId, pipelineRunId, review, request.CourseId, request.IsPublic);
            await _moduleRepository.SavePeQuestionsAsync(generationId, peRecords, review, cancellationToken);
        }

        var finalFeRecords = _questionPersistenceMapper.MapFeQuestions(finalSchemaMapping, request.UserId, pipelineRunId, review, request.CourseId, request.IsPublic);
        var finalPeRecords = _questionPersistenceMapper.MapPeQuestions(finalSchemaMapping, request.UserId, pipelineRunId, review, request.CourseId, request.IsPublic);

        return new GenerationReviewResult(
            request.Subject,
            questions,
            finalSchemaMapping,
            _difficultyAlignmentService.Evaluate(request.Subject, request.QuestionType, request.Difficulty, questions),
            finalFeRecords,
            finalPeRecords,
            review,
            attemptsUsed,
            stageLogs,
            usageLogs,
            SumCosts(stageLogs.Select(static item => item.Cost)),
            chunkResolution.ContextPack,
            chunkResolution.RetrievalPlan);
    }

    public async Task<QuestionRegressionRunResult> RunQuestionRegressionAsync(QuestionRegressionRunRequest request, CancellationToken cancellationToken)
    {
        var dataset = await _groundTruthCatalogService.GetQuestionGenerationGroundTruthAsync(request.Subject, request.QuestionType, cancellationToken);
        var datasetId = dataset.TryGetValue("dataset_id", out var datasetIdValue) ? datasetIdValue?.ToString() ?? "unknown" : "unknown";
        var cases = dataset.TryGetValue("evaluation_cases", out var casesValue) && casesValue is List<object> caseObjects
            ? caseObjects
            : [];

        var results = new List<QuestionRegressionCaseResult>();
        var acceptedCases = 0;
        var rejectedCases = 0;
        var totalCosts = new List<TokenCostBreakdown>();

        foreach (var caseObject in cases.OfType<IReadOnlyDictionary<string, object>>())
        {
            var caseId = caseObject.TryGetValue("case_id", out var caseIdValue) ? caseIdValue?.ToString() ?? "unknown_case" : "unknown_case";
            var difficulty = caseObject.TryGetValue("difficulty", out var difficultyValue) ? difficultyValue?.ToString() ?? "Medium" : "Medium";

            var generationReview = await RunGenerationReviewAsync(new GenerationReviewRequest(
                request.UserId,
                request.CourseId,
                null,
                request.IsPublic,
                request.Subject,
                difficulty,
                request.QuestionType,
                1,
                request.Mode,
                request.GeneratorModel,
                request.ReviewerModel,
                request.MaxAttempts,
                request.Chunks), cancellationToken);

            var duplicateDetected = generationReview.SchemaMapping.Issues.Any(issue => issue.Code is "duplicate_question" or "duplicate_existing_question");
            var difficultyAligned = !generationReview.Review.Issues.Any(issue => issue.Contains("difficulty", StringComparison.OrdinalIgnoreCase));
            var accepted = generationReview.Review.ReviewStatus.Equals("accepted", StringComparison.OrdinalIgnoreCase) && generationReview.Review.SchemaValid;

            if (accepted)
            {
                acceptedCases++;
            }
            else
            {
                rejectedCases++;
            }

            results.Add(new QuestionRegressionCaseResult(
                caseId,
                request.Subject,
                request.QuestionType,
                difficulty,
                generationReview.Review.ReviewStatus,
                generationReview.Review.SchemaValid,
                difficultyAligned,
                duplicateDetected,
                generationReview.Review.Issues,
                generationReview.Review.Score,
                generationReview.Totals));
            totalCosts.Add(generationReview.Totals);
        }

        return new QuestionRegressionRunResult(
            datasetId,
            request.Subject,
            request.QuestionType,
            request.GeneratorModel,
            request.ReviewerModel,
            results.Count,
            acceptedCases,
            rejectedCases,
            results,
            SumCosts(totalCosts),
            DateTimeOffset.UtcNow);
    }

    public async Task<QuestionReviewExportResult> RunGenerationReviewExportAsync(GenerationReviewRequest request, CancellationToken cancellationToken)
    {
        var generationReview = await RunGenerationReviewAsync(request, cancellationToken);
        var rubric = await _rubricCatalogService.GetQuestionReviewRubricAsync(request.Subject, request.QuestionType, cancellationToken);
        var groundTruth = await TryGetGroundTruthAsync(request.Subject, request.QuestionType, cancellationToken);
        var packageId = $"qrevpkg-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];

        return new QuestionReviewExportResult(
            new QuestionReviewPackageMetadata(
                packageId,
                request.Subject,
                request.QuestionType,
                request.Difficulty,
                request.Mode,
                request.GeneratorModel,
                request.ReviewerModel,
                request.MaxAttempts,
                BuildPromptKey("generator", request.Subject, request.QuestionType),
                BuildPromptKey("reviewer", request.Subject, request.QuestionType),
                GetStringValue(rubric, "rubric_id"),
                GetStringValue(rubric, "version"),
                GetStringValue(groundTruth, "dataset_id"),
                DateTimeOffset.UtcNow),
            new GenerationReviewRequestSnapshot(
                request.UserId,
                request.CourseId,
                request.DocumentId,
                request.IsPublic,
                request.Subject,
                request.Difficulty,
                request.QuestionType,
                request.Count,
                request.Mode,
                request.GeneratorModel,
                request.ReviewerModel,
                request.MaxAttempts,
                request.Chunks.Select(static chunk => new ChunkContextSnapshot(
                    chunk.ChunkId,
                    chunk.ContentText,
                    chunk.TopicTags,
                    chunk.SectionTitle,
                    chunk.Language)).ToList(),
                generationReview.ContextPack?.PackId ?? request.ContextPackId),
            rubric,
            generationReview.Questions,
            generationReview.SchemaMapping,
            generationReview.DifficultyAlignment,
            generationReview.MappedFeQuestions,
            generationReview.MappedPeQuestions,
            generationReview.Review,
            generationReview.AttemptsUsed,
            generationReview.StageLogs,
            generationReview.UsageLogs,
            generationReview.Totals);
    }

    public async Task<CodeMentorResult> RunCodeMentorAsync(CodeMentorRequest request, CancellationToken cancellationToken)
    {
        var combinedCode = CombineSourceFiles(request.SourceFiles, request.Code);
        var feedback = await _aiProviderGateway.MentorCodeAsync(request, cancellationToken);
        var cost = ResolveStageCost("code-mentor", request.MentorModel, request.Problem.Length + combinedCode.Length, JsonSerializer.Serialize(feedback).Length, "code-mentor");
        var stageLog = CreateStageLog("code-mentor", InferStageStatus(cost), cost, new Dictionary<string, string>
        {
            ["verdict"] = feedback.Verdict,
            ["issues_count"] = feedback.ErrorAnalysis.Count.ToString(),
            ["source_files_count"] = (request.SourceFiles?.Count ?? 0).ToString()
        }, InferProvider(request.MentorModel), request.MentorModel);
        await _moduleRepository.SaveMentorFeedbackAsync(request.SubmissionId, feedback, cancellationToken);
        return new CodeMentorResult(
            request.SubmissionId,
            feedback,
            [stageLog],
            [CreateUsageLog(
                triggeredBy: request.UserId,
                agentId: "AI_CodeMentor",
                pipelineRunId: $"mentor-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40],
                stageName: "code_mentor",
                attemptIndex: 1,
                provider: InferProvider(request.MentorModel),
                modelName: request.MentorModel,
                cost: cost,
                status: feedback.Verdict,
                payloadData: new AIUsageLogPayload(
                    "code_mentor",
                    request.Subject,
                    "PE",
                    null,
                    feedback.Verdict,
                    new { request.Problem, Code = combinedCode, request.SourceFiles, request.Language },
                    feedback))],
            cost);
    }

    public async Task<CodeMentorDebugResult> RunCodeMentorDebugAsync(CodeMentorRequest request, CancellationToken cancellationToken)
    {
        var result = await RunCodeMentorAsync(request, cancellationToken);
        var policy = await _codeMentorPolicyService.GetPolicyAsync(cancellationToken);
        var rubric = await _rubricCatalogService.GetCodeMentorRubricAsync(cancellationToken);
        var groundTruth = await TryGetCodeMentorGroundTruthAsync(cancellationToken);

        return new CodeMentorDebugResult(
            result.SubmissionId,
            result.Feedback,
            policy,
            rubric,
            groundTruth,
            result.StageLogs,
            result.UsageLogs,
            result.Totals);
    }

    public async Task<FullPipelineResult> RunFullPipelineAsync(FullPipelineRequest request, CancellationToken cancellationToken)
    {
        var ingestion = await RunIngestionAsync(request.Ingestion, cancellationToken);
        var generationReview = await RunGenerationReviewAsync(request.GenerationReview with
        {
            Chunks = ingestion.Document.Chunks.Select(static chunk => new ChunkContextDto(chunk.Id, chunk.NormalizedText, chunk.TopicTags, chunk.SectionTitle, chunk.Language)).ToList()
        }, cancellationToken);

        var usageLogs = ingestion.UsageLogs.Concat(generationReview.UsageLogs).ToList();
        return new FullPipelineResult(ingestion, generationReview, usageLogs, SumCosts([ingestion.Totals, generationReview.Totals]));
    }

    private async Task<ResolvedChunkSet> ResolveQuestionChunksAsync(
        string userId,
        string subject,
        string questionType,
        string difficulty,
        int requestedQuestionCount,
        string? contextPackId,
        RetrievalPlanRequest? retrievalRequest,
        IReadOnlyList<ChunkContextDto> chunks,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(contextPackId))
        {
            var pack = await _contextPackStore.TouchUsageAsync(contextPackId, cancellationToken);
            return new ResolvedChunkSet(
                pack.Chunks.Select(static chunk => new ChunkContextDto(chunk.ChunkId, chunk.ContentText, chunk.TopicTags, chunk.SectionTitle, chunk.Language)).ToList(),
                pack,
                null);
        }

        if (chunks is { Count: > 0 })
        {
            return new ResolvedChunkSet(chunks, null, null);
        }

        if (retrievalRequest is not null)
        {
            var normalizedRequest = retrievalRequest with
            {
                UserId = userId,
                Subject = subject,
                QuestionType = questionType,
                Difficulty = difficulty,
                RequestedQuestionCount = requestedQuestionCount
            };
            var plan = await _retrievalPlannerService.PlanAsync(normalizedRequest, cancellationToken);
            return new ResolvedChunkSet(
                plan.ContextPack.Chunks.Select(static chunk => new ChunkContextDto(chunk.ChunkId, chunk.ContentText, chunk.TopicTags, chunk.SectionTitle, chunk.Language)).ToList(),
                plan.ContextPack,
                plan);
        }

        throw new InvalidOperationException("Question generation requires at least one chunk, a valid contextPackId, or a retrieval request.");
    }

    private sealed record ResolvedChunkSet(
        IReadOnlyList<ChunkContextDto> Chunks,
        AIContextPack? ContextPack,
        RetrievalPlanResult? RetrievalPlan);

    private static ReviewDecision MergeReviewDecisions(ReviewDecision baseReview, ReviewDecision ruleBasedReview)
    {
        var issues = baseReview.Issues
            .Concat(ruleBasedReview.Issues.Select(static issue => $"[RuleBased] {issue}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var suggestions = baseReview.Suggestions
            .Concat(ruleBasedReview.Suggestions.Select(static item => $"[RuleBased] {item}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var schemaValid = baseReview.SchemaValid && ruleBasedReview.SchemaValid;
        var contentGrounded = baseReview.ContentGrounded && ruleBasedReview.ContentGrounded;
        var needsRevision = baseReview.NeedsRevision || ruleBasedReview.NeedsRevision;
        var accepted = !needsRevision && schemaValid && contentGrounded
            && baseReview.ReviewStatus.Equals("accepted", StringComparison.OrdinalIgnoreCase)
            && ruleBasedReview.ReviewStatus.Equals("accepted", StringComparison.OrdinalIgnoreCase);

        return baseReview with
        {
            ReviewStatus = accepted ? "accepted" : "needs_revision",
            Issues = issues,
            Suggestions = suggestions,
            Score = Math.Min(baseReview.Score, ruleBasedReview.Score),
            SchemaValid = schemaValid,
            ContentGrounded = contentGrounded,
            NeedsRevision = needsRevision
        };
    }

    private static ReviewDecision BuildRuleBasedQuestionReview(
        string subject,
        string questionType,
        string requestedDifficulty,
        IReadOnlyList<GeneratedQuestion> questions,
        IReadOnlyList<ChunkContextDto> chunks,
        IReadOnlyDictionary<string, object> rubric,
        QuestionSchemaMappingResult schemaMapping,
        DifficultyAlignmentResult difficultyAlignment)
    {
        var issues = new List<string>();
        var suggestions = new List<string>();
        var expectedType = questionType.Trim().Equals("PE", StringComparison.OrdinalIgnoreCase) ? "PE" : "FE";
        var chunkById = chunks.ToDictionary(static chunk => chunk.ChunkId, StringComparer.OrdinalIgnoreCase);
        var normalizedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedDescriptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (questions.Count == 0)
        {
            issues.Add("No questions were produced.");
        }

        if (!schemaMapping.IsValid)
        {
            issues.AddRange(schemaMapping.Issues.Select(static item => item.Message));
        }

        for (var index = 0; index < questions.Count; index++)
        {
            var question = questions[index];
            var prefix = $"Question {index + 1}";
            var titleKey = NormalizeComparableText(question.Title);
            var descriptionKey = NormalizeComparableText(question.Description);
            if (!normalizedTitles.Add(titleKey))
            {
                issues.Add($"{prefix}: duplicated title detected.");
            }

            if (!string.IsNullOrWhiteSpace(descriptionKey) && !normalizedDescriptions.Add(descriptionKey))
            {
                issues.Add($"{prefix}: duplicated description detected.");
            }

            if (!string.Equals(question.Type, expectedType, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"{prefix}: question type '{question.Type}' does not match requested type '{expectedType}'.");
            }

            if (LooksLikePlaceholder(question.Title) || LooksLikePlaceholder(question.Description))
            {
                issues.Add($"{prefix}: title or description still looks template-based instead of chunk-based.");
            }

            if (ContainsMetaSourceReference(question.Title) || ContainsMetaSourceReference(question.Description))
            {
                issues.Add($"{prefix}: question wording refers to the source container such as slide/page/document/chunk instead of being self-contained.");
                suggestions.Add($"{prefix}: rewrite the wording so the learner can answer without knowing where the content was stored.");
            }

            var sourceChunkIds = question.SourceChunkIds?
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
            if (sourceChunkIds.Count == 0)
            {
                issues.Add($"{prefix}: source_chunk_ids must not be empty.");
                continue;
            }

            var sourceChunks = sourceChunkIds
                .Where(chunkById.ContainsKey)
                .Select(id => chunkById[id])
                .ToList();
            if (sourceChunks.Count != sourceChunkIds.Count)
            {
                issues.Add($"{prefix}: some source_chunk_ids do not exist in the provided chunk set.");
            }

            var sourceText = string.Join("\n", sourceChunks.Select(static chunk => $"{chunk.SectionTitle} {chunk.ContentText}".Trim()));
            var sourceTags = sourceChunks.SelectMany(static chunk => chunk.TopicTags).Where(static tag => !string.IsNullOrWhiteSpace(tag)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var questionTags = question.TopicTags.Where(static tag => !string.IsNullOrWhiteSpace(tag)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (questionTags.Count > 0 && sourceTags.Count > 0 && !questionTags.Overlaps(sourceTags))
            {
                suggestions.Add($"{prefix}: topic_tags do not overlap with the cited chunk tags. Consider aligning tags more closely to the cited chunks.");
            }

            var groundingSignal = MeasureGroundingSignal(question, expectedType, sourceText, sourceTags);
            if (!groundingSignal.IsGrounded)
            {
                issues.Add($"{prefix}: weak grounding to the cited chunks. Shared chunk-specific keywords were too low.");
                suggestions.Add($"{prefix}: anchor the item to explicit terms, behaviors, or examples that appear in the cited chunks.");
            }

            if (ContainsSubjectLeakage(subject, question, sourceText))
            {
                issues.Add($"{prefix}: content appears to drift into another subject or language ecosystem not supported by the cited chunks.");
            }

            if (expectedType == "FE")
            {
                ValidateFeQuestion(question, prefix, issues, suggestions);
            }
            else
            {
                ValidatePeQuestion(subject, question, prefix, issues, suggestions, rubric);
            }
        }

        if (!difficultyAlignment.IsAligned)
        {
            var difficultyMessage = $"Difficulty mismatch: requested '{requestedDifficulty}' but rule-based alignment estimated '{difficultyAlignment.EstimatedDifficulty}'.";
            if (IsSevereDifficultyMismatch(difficultyAlignment.RequestedDifficulty, difficultyAlignment.EstimatedDifficulty))
            {
                issues.Add(difficultyMessage);
            }
            else
            {
                suggestions.Add(difficultyMessage);
            }
        }

        if (issues.Count == 0 && TryReadArrayStrings(rubric, "acceptance_rules").Count == 0)
        {
            suggestions.Add("No rubric acceptance rules were loaded; rely on schema, grounding, and difficulty checks.");
        }

        var score = Math.Max(0.05m, Math.Min(1.0m, 1.0m - (issues.Count * 0.12m) - (suggestions.Count * 0.03m)));
        var hasSchemaErrors = schemaMapping.Issues.Any(issue => issue.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
        var accepted = issues.Count == 0;
        return new ReviewDecision(
            accepted ? "accepted" : "needs_revision",
            issues,
            suggestions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            score,
            !hasSchemaErrors,
            !issues.Any(issue => issue.Contains("weak grounding", StringComparison.OrdinalIgnoreCase)
                                 || issue.Contains("drift into another subject", StringComparison.OrdinalIgnoreCase)
                                 || issue.Contains("source container", StringComparison.OrdinalIgnoreCase)),
            !accepted,
            "rule-based");
    }

    private static bool IsSevereDifficultyMismatch(string requestedDifficulty, string estimatedDifficulty)
        => Math.Abs(GetDifficultyRank(requestedDifficulty) - GetDifficultyRank(estimatedDifficulty)) > 1;

    private static ReviewDecision NormalizeReviewDecisionForMildDifficultyMismatch(
        ReviewDecision review,
        DifficultyAlignmentResult difficultyAlignment)
    {
        if (review.Issues.Count == 0
            || !review.SchemaValid
            || !review.ContentGrounded
            || IsSevereDifficultyMismatch(difficultyAlignment.RequestedDifficulty, difficultyAlignment.EstimatedDifficulty))
        {
            return review;
        }

        static bool IsDifficultyIssue(string issue)
            => issue.Contains("Difficulty alignment mismatch", StringComparison.OrdinalIgnoreCase)
               || issue.Contains("Difficulty mismatch:", StringComparison.OrdinalIgnoreCase);

        if (review.Issues.Any(issue => !IsDifficultyIssue(issue)))
        {
            return review;
        }

        var suggestions = review.Suggestions
            .Concat(review.Issues.Select(static issue => $"{issue} Treat as a tuning warning, not a hard failure."))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return review with
        {
            ReviewStatus = "accepted",
            Issues = [],
            Suggestions = suggestions,
            Score = Math.Max(review.Score, 0.84m),
            NeedsRevision = false
        };
    }

    private static int GetDifficultyRank(string? difficulty)
        => difficulty?.Trim().ToLowerInvariant() switch
        {
            "easy" => 1,
            "hard" => 3,
            _ => 2
        };

    private static void ValidateFeQuestion(GeneratedQuestion question, string prefix, List<string> issues, List<string> suggestions)
    {
        var options = question.Options?.Where(static item => !string.IsNullOrWhiteSpace(item)).ToList() ?? [];
        if (options.Count != 4)
        {
            issues.Add($"{prefix}: FE must contain exactly 4 options.");
        }

        var normalizedBodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validLabels = new HashSet<string>(["A", "B", "C", "D"], StringComparer.OrdinalIgnoreCase);
        foreach (var option in options)
        {
            var parts = option.Split('.', 2, StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || !validLabels.Contains(parts[0]))
            {
                issues.Add($"{prefix}: FE options must use labels A-D.");
                continue;
            }

            if (!normalizedBodies.Add(NormalizeComparableText(parts[1])))
            {
                issues.Add($"{prefix}: FE contains duplicated option bodies.");
            }

            if (LooksLikePlaceholder(option))
            {
                issues.Add($"{prefix}: FE contains placeholder option text.");
            }

            if (ContainsMetaSourceReference(option))
            {
                issues.Add($"{prefix}: FE options should not mention slide/page/document/chunk references.");
            }
        }

        var answers = question.CorrectAnswer?.Where(static item => !string.IsNullOrWhiteSpace(item)).ToList() ?? [];
        if (answers.Count != 1)
        {
            issues.Add($"{prefix}: FE must contain exactly one correct answer label.");
        }
        else if (!validLabels.Contains(answers[0]))
        {
            issues.Add($"{prefix}: FE correct_answer must contain labels only, for example A or B.");
        }

        if (string.IsNullOrWhiteSpace(question.Explanation) || question.Explanation.Trim().Length < 24)
        {
            issues.Add($"{prefix}: FE explanation is too short to justify the answer.");
            suggestions.Add($"{prefix}: explain why the correct option is supported by the chunk and why the distractors are wrong.");
        }
        else if (ContainsMetaSourceReference(question.Explanation))
        {
            issues.Add($"{prefix}: FE explanation should be self-contained and must not refer to slides/pages/documents/chunks.");
        }
    }

    private static void ValidatePeQuestion(
        string subject,
        GeneratedQuestion question,
        string prefix,
        List<string> issues,
        List<string> suggestions,
        IReadOnlyDictionary<string, object> rubric)
    {
        var skeleton = question.SkeletonCode?.Where(static item => !string.IsNullOrWhiteSpace(item.FileName) && !string.IsNullOrWhiteSpace(item.Content)).ToList() ?? [];
        var solution = question.SolutionCode?.Where(static item => !string.IsNullOrWhiteSpace(item.FileName) && !string.IsNullOrWhiteSpace(item.Content)).ToList() ?? [];
        var testCases = question.TestCases?.ToList() ?? [];
        var description = question.Description?.Trim() ?? string.Empty;
        var title = question.Title?.Trim() ?? string.Empty;
        var descriptionWordCount = description.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var visibleTests = testCases.Count(static item => !item.IsHidden);
        var hiddenTests = testCases.Count(static item => item.IsHidden);
        var distinctExpectedOutputs = testCases
            .Select(static item => item.ExpectedOutput?.Trim() ?? string.Empty)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (skeleton.Count == 0)
        {
            issues.Add($"{prefix}: PE must contain skeleton_code.");
        }

        if (solution.Count == 0)
        {
            issues.Add($"{prefix}: PE must contain solution_code.");
        }

        if (testCases.Count < 2)
        {
            issues.Add($"{prefix}: PE should contain at least 2 test cases.");
        }

        if (visibleTests == 0)
        {
            issues.Add($"{prefix}: PE should contain at least one visible test case.");
        }

        if (!HasExpectedFileExtension(subject, skeleton) || !HasExpectedFileExtension(subject, solution))
        {
            issues.Add($"{prefix}: code file extensions do not match the subject language expectations.");
        }

        if (skeleton.Count > 0 && solution.Count > 0)
        {
            var combinedSkeleton = string.Join("\n", skeleton.Select(static item => item.Content));
            var combinedSolution = string.Join("\n", solution.Select(static item => item.Content));
            if (NormalizeComparableText(combinedSkeleton) == NormalizeComparableText(combinedSolution))
            {
                issues.Add($"{prefix}: skeleton_code and solution_code are effectively identical.");
            }
        }

        if (descriptionWordCount < 22)
        {
            issues.Add($"{prefix}: PE description is too thin to be a meaningful assessment item.");
            AddRubricQualityHints(rubric, suggestions);
        }

        if (!ContainsAny(description, ["input", "output", "return", "print", "write a program", "implement", "complete the method", "write a method", "read"]))
        {
            suggestions.Add($"{prefix}: make the PE description more explicit about the required program behavior.");
        }

        if (testCases.Count < 3)
        {
            suggestions.Add($"{prefix}: add broader test coverage so the task evaluates more than one narrow sample.");
        }

        if (hiddenTests == 0)
        {
            suggestions.Add($"{prefix}: add at least one hidden test to reduce lucky hardcoded solutions.");
        }

        if (visibleTests == 1 && !ContainsAny(description, ["example", "for example", "sample input", "sample output"]))
        {
            suggestions.Add($"{prefix}: include an example in the statement or add another visible test to make expected behavior easier to understand.");
        }

        if (testCases.Count >= 3 && distinctExpectedOutputs <= 1)
        {
            suggestions.Add($"{prefix}: current test outputs are too repetitive; add stronger variation so the tests better distinguish correct logic.");
        }

        if (LooksLikeLowValuePeDrill(subject, title, description, testCases))
        {
            suggestions.Add($"{prefix}: PE is currently close to a syntax drill. Reframe it as a small but meaningful task that still stays grounded in the chunks.");
            AddRubricQualityHints(rubric, suggestions);
        }
    }

    private static bool HasExpectedFileExtension(string subject, IReadOnlyList<CodeFile> files)
    {
        if (files.Count == 0)
        {
            return false;
        }

        return subject.Trim().ToUpperInvariant() switch
        {
            "C" => files.All(static file => file.FileName.EndsWith(".c", StringComparison.OrdinalIgnoreCase) || file.FileName.EndsWith(".h", StringComparison.OrdinalIgnoreCase)),
            _ => files.All(static file => file.FileName.EndsWith(".java", StringComparison.OrdinalIgnoreCase))
        };
    }

    private static (bool IsGrounded, int SharedKeywordCount) MeasureGroundingSignal(
        GeneratedQuestion question,
        string expectedType,
        string sourceText,
        IReadOnlySet<string> sourceTags)
    {
        var questionText = expectedType == "FE"
            ? string.Join(" ", new[]
            {
                question.Title,
                question.Description,
                string.Join(" ", question.Options ?? []),
                question.Explanation ?? string.Empty,
                string.Join(" ", question.TopicTags)
            })
            : string.Join(" ", new[]
            {
                question.Title,
                question.Description,
                string.Join(" ", question.TopicTags),
                string.Join(" ", question.TestCases?.Select(static item => $"{item.Input} {item.ExpectedOutput}") ?? [])
            });

        var questionKeywords = ExtractKeywords(questionText);
        var sourceKeywords = ExtractKeywords($"{sourceText} {string.Join(" ", sourceTags)}");
        var shared = questionKeywords.Intersect(sourceKeywords, StringComparer.OrdinalIgnoreCase).Count();
        var minimumShared = expectedType == "FE" ? 2 : 3;
        var topicOverlap = question.TopicTags.Any(tag => sourceTags.Contains(tag));
        return (topicOverlap || shared >= minimumShared, shared);
    }

    private static bool ContainsSubjectLeakage(string subject, GeneratedQuestion question, string sourceText)
    {
        var combined = $"{question.Title} {question.Description} {string.Join(" ", question.Options ?? [])} {string.Join(" ", question.TopicTags)}".ToLowerInvariant();
        var sourceLower = sourceText.ToLowerInvariant();
        return subject.Trim().ToUpperInvariant() switch
        {
            "C" => ContainsUnexpectedAny(combined, sourceLower, ["class", "object", "extends", "implements", "system.out", "println", "arraylist", "linkedlist"]),
            "JAVA_OOP" => ContainsUnexpectedAny(combined, sourceLower, ["#include", "printf", "scanf", "malloc", "free", "pointer arithmetic", "->"]),
            "DSA_JAVA" => ContainsUnexpectedAny(combined, sourceLower, ["#include", "printf", "scanf", "malloc", "free", "pointer arithmetic", "struct "]),
            _ => false
        };
    }

    private static bool ContainsUnexpectedAny(string candidateText, string sourceText, IReadOnlyList<string> tokens)
        => tokens.Any(token => candidateText.Contains(token, StringComparison.OrdinalIgnoreCase)
                               && !sourceText.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string text, IReadOnlyList<string> patterns)
        => patterns.Any(pattern => text.Contains(pattern, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeLowValuePeDrill(string subject, string title, string description, IReadOnlyList<QuestionTestCase> testCases)
    {
        var combined = $"{title} {description}";
        var shortDirectStatement = description.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 42;
        var syntaxHeavy = ContainsAny(combined, ["operator", "keyword", "modifier", "specifier", "sizeof", "const", "remainder", "quotient", "increment", "decrement", "getter", "setter"]);
        var lowBehaviorFraming = !ContainsAny(combined, ["given", "manage", "store", "calculate", "determine", "validate", "track", "update", "find", "count", "check", "class", "object", "method", "array", "list", "student", "book", "library", "score", "rectangle", "account", "inventory", "number"]);
        var weakTests = testCases.Count <= 2;

        if (subject.Equals("JAVA_OOP", StringComparison.OrdinalIgnoreCase))
        {
            return shortDirectStatement && syntaxHeavy && weakTests;
        }

        return shortDirectStatement && (syntaxHeavy || lowBehaviorFraming) && weakTests;
    }

    private static void AddRubricQualityHints(IReadOnlyDictionary<string, object> rubric, List<string> suggestions)
    {
        foreach (var hint in TryReadArrayStrings(rubric, "quality_regeneration_hints"))
        {
            suggestions.Add(hint);
        }
    }

    private static bool LooksLikePlaceholder(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(value)
            || value.StartsWith("FE Question", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("PE Question", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Question ", StringComparison.OrdinalIgnoreCase)
            || value.Contains("based on the chunk content", StringComparison.OrdinalIgnoreCase)
            || value.Contains("distractor option", StringComparison.OrdinalIgnoreCase)
            || value.Contains("correct statement about", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsMetaSourceReference(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] patterns =
        [
            "according to the slide",
            "according to the slides",
            "according to the page",
            "according to the pages",
            "according to the document",
            "according to the chapter",
            "according to the chunk",
            "from the slide",
            "from the slides",
            "from the page",
            "from the document",
            "from the chunk",
            "in the slide",
            "in the slides",
            "in the page",
            "in the document",
            "in the chunk",
            "in the text above",
            "the slide says",
            "the document says"
        ];

        return patterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeComparableText(string? value)
    {
        var raw = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return string.Concat(raw.Select(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' '))
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static HashSet<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "that", "with", "from", "this", "which", "into", "about", "there", "their", "would",
            "should", "could", "while", "using", "where", "what", "when", "your", "have", "has", "had",
            "into", "than", "them", "then", "true", "false", "null", "void", "main", "program", "question",
            "write", "based", "chunk", "content", "best", "select", "correct", "option", "answer", "explain"
        };

        var normalized = new string(text.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ').ToArray());
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3 && !stopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> TryReadArrayStrings(IReadOnlyDictionary<string, object> source, string key)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        return value switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.Array => element.EnumerateArray().Select(static item => item.ToString()).Where(static item => !string.IsNullOrWhiteSpace(item)).ToList(),
            IEnumerable<object> items => items.Select(static item => item?.ToString() ?? string.Empty).Where(static item => !string.IsNullOrWhiteSpace(item)).ToList(),
            _ => []
        };
    }

    private static TokenCostBreakdown SumCosts(IEnumerable<TokenCostBreakdown> items)
    {
        string? usageSource = null;
        long? cacheInputTokens = null;
        long? cacheReadTokens = null;
        long? reasoningTokens = null;
        var usageSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        NormalizedError? firstError = null;
        long inputTokens = 0;
        long outputTokens = 0;
        decimal inputCost = 0;
        decimal outputCost = 0;
        decimal totalCost = 0;
        decimal latency = 0;

        foreach (var item in items)
        {
            inputTokens += item.InputTokens;
            outputTokens += item.OutputTokens;
            inputCost += item.InputCostUsd;
            outputCost += item.OutputCostUsd;
            totalCost += item.TotalCostUsd;
            latency += item.LatencyMs;
            if (item.UsageCapture is not null && !string.IsNullOrWhiteSpace(item.UsageCapture.UsageSource))
            {
                usageSources.Add(item.UsageCapture.UsageSource);
                cacheInputTokens = SumNullable(cacheInputTokens, item.UsageCapture.CacheInputTokens);
                cacheReadTokens = SumNullable(cacheReadTokens, item.UsageCapture.CacheReadTokens);
                reasoningTokens = SumNullable(reasoningTokens, item.UsageCapture.ReasoningTokens);
            }

            firstError ??= item.Error;
        }

        if (usageSources.Count == 1)
        {
            usageSource = usageSources.First();
        }
        else if (usageSources.Count > 1)
        {
            usageSource = "mixed";
        }

        return new TokenCostBreakdown(
            inputTokens,
            outputTokens,
            inputCost,
            outputCost,
            totalCost,
            latency,
            usageSource is null ? null : new UsageCapture(usageSource, cacheInputTokens, cacheReadTokens, reasoningTokens, null),
            firstError);
    }

    private static string InferProvider(string model)
    {
        var lowered = model.ToLowerInvariant();
        return lowered switch
        {
            var value when value.Contains("gpt") || value.Contains("text-embedding") => "openai",
            var value when value.Contains("gemini") => "gemini",
            var value when value.Contains("cohere") || value.Contains("embed") || value.Contains("command") => "cohere",
            _ => "custom"
        };
    }

    private static AIUsageLogEntry CreateUsageLog(
        string? triggeredBy,
        string agentId,
        string pipelineRunId,
        string stageName,
        int attemptIndex,
        string provider,
        string modelName,
        TokenCostBreakdown cost,
        string status,
        object payloadData)
    {
        var creditsDeducted = decimal.Round(cost.TotalCostUsd * 1000m, 4);
        var modelFields = BuildModelFields(provider, modelName, stageName);
        return new AIUsageLogEntry(
            $"usage-{Guid.NewGuid():N}"[..26],
            triggeredBy,
            agentId,
            pipelineRunId,
            stageName,
            attemptIndex,
            provider,
            modelName,
            creditsDeducted,
            cost.InputTokens,
            cost.OutputTokens,
            cost.InputTokens + cost.OutputTokens,
            cost.InputCostUsd,
            cost.OutputCostUsd,
            cost.TotalCostUsd,
            cost.UsageCapture?.UsageSource ?? "estimated",
            status,
            DateTimeOffset.UtcNow,
            payloadData,
            modelFields,
            cost.Error,
            cost.UsageCapture?.RawUsageJson);
    }

    private static NormalizedModelFields BuildModelFields(string provider, string modelName, string stageName)
    {
        var normalizedStage = stageName.Trim().ToLowerInvariant();
        if (normalizedStage.Contains("question-generation", StringComparison.Ordinal) || normalizedStage.Contains("question_generation", StringComparison.Ordinal))
        {
            return new NormalizedModelFields(provider, null, modelName, null, null, null, null, null);
        }

        if (normalizedStage.Contains("question-review", StringComparison.Ordinal) || normalizedStage.Contains("question_review", StringComparison.Ordinal))
        {
            return new NormalizedModelFields(provider, null, null, modelName, null, null, null, null);
        }

        if (normalizedStage.Contains("auto-tagging", StringComparison.Ordinal) || normalizedStage.Contains("auto_tagging", StringComparison.Ordinal))
        {
            return new NormalizedModelFields(provider, null, null, null, null, modelName, null, null);
        }

        if (normalizedStage.Contains("extract-content", StringComparison.Ordinal) || normalizedStage.Contains("extract_content", StringComparison.Ordinal))
        {
            return new NormalizedModelFields(provider, null, null, null, null, null, modelName, null);
        }

        if (normalizedStage.Contains("code-mentor", StringComparison.Ordinal) || normalizedStage.Contains("code_mentor", StringComparison.Ordinal))
        {
            return new NormalizedModelFields(provider, null, null, null, null, null, null, modelName);
        }

        return normalizedStage switch
        {
            "embedding" => new NormalizedModelFields(provider, null, null, null, modelName, null, null, null),
            _ => new NormalizedModelFields(provider, modelName, null, null, null, null, null, null)
        };
    }

    private TokenCostBreakdown ResolveStageCost(string stageName, string modelName, int inputSize, int outputSize, string usageKey)
    {
        var cost = _aiProviderGateway.ConsumeUsageSnapshot(usageKey)
            ?? _aiProviderGateway.EstimateCost(stageName, modelName, inputSize, outputSize);
        var error = _aiProviderGateway.ConsumeErrorSnapshot(usageKey);
        return error is null || cost.Error is not null ? cost : cost with { Error = error };
    }

    private static RunStatus InferStageStatus(TokenCostBreakdown cost)
        => cost.Error is null ? RunStatus.Completed : RunStatus.Failed;

    private static PipelineStageLog CreateStageLog(
        string stageName,
        RunStatus status,
        TokenCostBreakdown cost,
        IReadOnlyDictionary<string, string> metadata,
        string provider,
        string modelName)
        => new(
            stageName,
            status,
            cost,
            metadata,
            BuildModelFields(provider, modelName, stageName),
            cost.Error);

    private static long? SumNullable(long? current, long? next)
    {
        if (current is null)
        {
            return next;
        }

        if (next is null)
        {
            return current;
        }

        return current.Value + next.Value;
    }

    private static string BuildPipelineRunId(string prefix)
        => $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];

    private async Task<IReadOnlyDictionary<string, object>?> TryGetGatekeeperGroundTruthAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _groundTruthCatalogService.GetGatekeeperGroundTruthAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyDictionary<string, object>?> TryGetExtractedContentGroundTruthAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _groundTruthCatalogService.GetExtractedContentGroundTruthAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyDictionary<string, object>?> TryGetEmbeddingTaggingGroundTruthAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _groundTruthCatalogService.GetEmbeddingTaggingGroundTruthAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyDictionary<string, object>?> TryGetCodeMentorGroundTruthAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _groundTruthCatalogService.GetCodeMentorGroundTruthAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static GatekeeperVerdict? TryBuildGatekeeperPrecheckVerdict(
        GatekeeperRequest request,
        IReadOnlyDictionary<string, object> gatekeeperPolicy)
    {
        var trimmed = request.RawContent.Trim();
        var minWordCount = TryReadLong(gatekeeperPolicy, "precheck_rules", "min_word_count") ?? 12;
        var emptyCode = TryReadString(gatekeeperPolicy, "precheck_rules", "empty_content_rejection_code") ?? "empty_content";
        var shortCode = TryReadString(gatekeeperPolicy, "precheck_rules", "insufficient_content_rejection_code") ?? "insufficient_content";
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new GatekeeperVerdict(
                false,
                "ambiguous",
                "UNKNOWN",
                [],
                0.05m,
                "The input content is empty, so the system cannot determine whether the document belongs to the supported whitelist.",
                emptyCode,
                [],
                request.Model);
        }

        var wordCount = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount < minWordCount)
        {
            return new GatekeeperVerdict(
                false,
                "ambiguous",
                "UNKNOWN",
                [],
                0.20m,
                "The extracted text is too short to classify the document reliably against the supported whitelist.",
                shortCode,
                [],
                request.Model);
        }

        return null;
    }

    private static long? TryReadLong(IReadOnlyDictionary<string, object> root, string objectKey, string valueKey)
    {
        if (!root.TryGetValue(objectKey, out var nested) || nested is not IReadOnlyDictionary<string, object> nestedDictionary)
        {
            return null;
        }

        if (!nestedDictionary.TryGetValue(valueKey, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            long l => l,
            int i => i,
            decimal d => (long)d,
            string s when long.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private static string? TryReadString(IReadOnlyDictionary<string, object> root, string objectKey, string valueKey)
    {
        if (!root.TryGetValue(objectKey, out var nested) || nested is not IReadOnlyDictionary<string, object> nestedDictionary)
        {
            return null;
        }

        return nestedDictionary.TryGetValue(valueKey, out var value) ? value?.ToString() : null;
    }

    private static bool? TryReadBool(IReadOnlyDictionary<string, object> root, string objectKey, string valueKey)
    {
        if (!root.TryGetValue(objectKey, out var nested) || nested is not IReadOnlyDictionary<string, object> nestedDictionary)
        {
            return null;
        }

        if (!nestedDictionary.TryGetValue(valueKey, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private static string DetectSourceType(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "pdf",
            ".doc" or ".docx" => "docx",
            ".ppt" or ".pptx" => "pptx",
            ".txt" => "txt",
            ".png" or ".jpg" or ".jpeg" or ".webp" => "image",
            _ => "txt"
        };

    private static string NormalizeSubjectCode(string subject)
    {
        var lowered = subject.Trim().ToLowerInvariant();
        if (lowered.Contains("java") && lowered.Contains("oop"))
        {
            return "JAVA_OOP";
        }

        if (lowered.Contains("dsa") || lowered.Contains("data structure") || lowered.Contains("algorithm"))
        {
            return "DSA_OOP";
        }

        return "C_BASIC";
    }

    private static string NormalizeChunkText(string value)
        => value.Replace("\r\n", "\n").Replace("\r", "\n").Trim();

    private static int CountWords(string value)
        => value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string? DetectSectionTitle(string markdownText)
    {
        var line = markdownText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static item => item.Trim())
            .FirstOrDefault(static item => item.StartsWith("#", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        return line.TrimStart('#', ' ');
    }

    private static string DetectChunkType(string chunkText, string fallbackChunkType)
    {
        var normalized = chunkText.Trim();
        if (normalized.Contains("```", StringComparison.Ordinal) || normalized.Contains("#include", StringComparison.OrdinalIgnoreCase) || normalized.Contains("public class", StringComparison.OrdinalIgnoreCase))
        {
            return "code";
        }

        if (normalized.Contains("| ---", StringComparison.Ordinal) || normalized.Contains("\t", StringComparison.Ordinal))
        {
            return "table";
        }

        if (normalized.Contains("Vision Extracted From Image", StringComparison.OrdinalIgnoreCase))
        {
            return "image_desc";
        }

        if (normalized.StartsWith("#", StringComparison.Ordinal))
        {
            return "concept";
        }

        return fallbackChunkType;
    }

    private async Task<ExtractionDraftEnvelope> BuildAndStoreExtractionDraftAsync(
        ExtractedContentRequest request,
        ExtractedContent extracted,
        TokenCostBreakdown cost,
        CancellationToken cancellationToken)
    {
        var documentId = $"doc-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];
        var draftId = $"draft-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
        var now = DateTimeOffset.UtcNow;
        var subjectCode = NormalizeSubjectCode(request.Subject);
        var sourceType = DetectSourceType(request.FileName);
        var sourceChecksum = request.SourceChecksum ?? ComputeStableHash(request.RawContent);
        var segments = _chunkingService.BuildChunks(extracted.NormalizedMarkdown, request.Mode);
        var totalInputTokens = cost.InputTokens;
        var totalOutputTokens = cost.OutputTokens;
        var totalChars = Math.Max(1, segments.Sum(static item => item.Length));
        var contents = new List<ExtractionDraftContent>();

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var ratio = (decimal)segment.Length / totalChars;
            var inputTokens = (long)Math.Max(0, Math.Round(totalInputTokens * ratio, MidpointRounding.AwayFromZero));
            var outputTokens = (long)Math.Max(0, Math.Round(totalOutputTokens * ratio, MidpointRounding.AwayFromZero));
            var segmentCost = Math.Round(cost.TotalCostUsd * ratio, 6);
            var segmentLatency = Math.Round(cost.LatencyMs * ratio, 2);
            var normalized = NormalizeChunkText(segment);
            var contentId = $"{draftId}-seg-{index + 1:D4}";

            contents.Add(new ExtractionDraftContent(
                contentId,
                draftId,
                documentId,
                request.UserId,
                request.CourseId,
                index,
                request.Mode == PipelineMode.FullMultimodalPage ? "page_block" : "logical_block",
                DetectSectionTitle(segment),
                index + 1,
                index + 1,
                request.Mode == PipelineMode.FullMultimodalPage ? $"page_{index + 1}" : $"segment_{index + 1}",
                segment,
                normalized,
                null,
                ExtractImageReferences(segment),
                BuildVisionExpansions(segment, request.VisionModel),
                ExtractCleanupWarnings(segment, extracted.Warnings),
                "needs_review",
                null,
                null,
                0,
                CountWords(normalized),
                (int)Math.Max(1, Math.Ceiling(normalized.Length / 4.0)),
                normalized.Length,
                inputTokens,
                outputTokens,
                segmentCost,
                segmentLatency,
                now,
                now));
        }

        var draft = new ExtractionDraft(
            draftId,
            request.CourseId,
            documentId,
            request.UserId,
            sourceType,
            request.FileName,
            request.SourceStoragePath,
            request.SourceSizeBytes ?? request.RawContent.Length,
            sourceChecksum,
            request.Language,
            subjectCode,
            string.IsNullOrWhiteSpace(request.CourseId) ? request.OwnershipType : "system_seeded",
            extracted.ParserName,
            "v1",
            extracted.ExtractionMode,
            string.IsNullOrWhiteSpace(request.VisionModel) ? null : InferProvider(request.VisionModel),
            request.VisionModel,
            contents.Count,
            contents.Count,
            0,
            0,
            extracted.DetectedImageReferences,
            extracted.EmbeddedImageCount,
            extracted.Warnings,
            null,
            BuildPreview(extracted.RawText),
            BuildPreview(extracted.NormalizedMarkdown),
            null,
            "needs_review",
            null,
            null,
            0,
            totalInputTokens,
            totalOutputTokens,
            cost.TotalCostUsd,
            cost.LatencyMs,
            "pending",
            0,
            null,
            new ExtractionPricingBasisSnapshot(
                request.SourceSizeBytes ?? request.RawContent.Length,
                contents.Count,
                extracted.WordCount,
                extracted.EmbeddedImageCount,
                totalInputTokens,
                totalOutputTokens,
                cost.TotalCostUsd),
            false,
            0,
            null,
            now,
            now);

        return await _extractionDraftStore.SaveAsync(draft, contents, cancellationToken);
    }

    private static IReadOnlyList<ExtractionVisionExpansion> BuildVisionExpansions(string segment, string? visionModel)
    {
        var imageRefs = ExtractImageReferences(segment);
        return imageRefs.Select((imageRef, index) => new ExtractionVisionExpansion(
            imageRef,
            "inline_after_marker",
            index + 1,
            $"### Vision Extracted From Image `{imageRef}`",
            string.IsNullOrWhiteSpace(visionModel) ? null : InferProvider(visionModel),
            visionModel,
            0,
            0,
            0,
            0,
            string.IsNullOrWhiteSpace(visionModel) ? "skipped" : "completed")).ToList();
    }

    private static IReadOnlyList<string> ExtractImageReferences(string content)
    {
        return System.Text.RegularExpressions.Regex.Matches(content, @"\[(image|img|figure):([^\]]+)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(match => match.Groups[2].Value.Trim())
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> ExtractCleanupWarnings(string segment, IReadOnlyList<string> rootWarnings)
    {
        var warnings = new HashSet<string>(rootWarnings, StringComparer.OrdinalIgnoreCase);
        if (segment.Length < 40)
        {
            warnings.Add("Segment is short and may not carry enough learning context.");
        }

        return warnings.ToList();
    }

    private static string ComputeStableHash(string content)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static string? BuildPreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var normalized = NormalizeChunkText(content);
        return normalized.Length <= 400 ? normalized : normalized[..400];
    }

    private static string BuildPromptKey(string role, string subject, string questionType)
        => $"{role}:{subject}:{questionType}".ToLowerInvariant();

    private static string? GetStringValue(IReadOnlyDictionary<string, object>? data, string key)
    {
        if (data is null || !data.TryGetValue(key, out var value))
        {
            return null;
        }

        return value?.ToString();
    }

    private static string CombineSourceFiles(IReadOnlyList<CodeFile>? sourceFiles, string? fallbackCode)
    {
        if (sourceFiles is null || sourceFiles.Count == 0)
        {
            return fallbackCode ?? string.Empty;
        }

        return string.Join(
            "\n\n",
            sourceFiles.Select(static file =>
                $"// FILE: {file.FileName}\n{file.Content}".Trim()));
    }

    private async Task<IReadOnlyDictionary<string, object>?> TryGetGroundTruthAsync(
        string subject,
        string questionType,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _groundTruthCatalogService.GetQuestionGenerationGroundTruthAsync(subject, questionType, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<QuestionSchemaMappingResult> EnrichSchemaMappingWithRepositoryDuplicatesAsync(
        string? courseId,
        QuestionSchemaMappingResult schemaMapping,
        CancellationToken cancellationToken)
    {
        var duplicateIssues = new List<QuestionValidationIssue>();

        if (schemaMapping.QuestionType.Equals("FE", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var question in schemaMapping.FeQuestions)
            {
                var candidates = await _moduleRepository.FindQuestionDuplicateCandidatesAsync(
                    courseId,
                    "FE",
                    question.Title,
                    question.Description,
                    question.TopicTags,
                    cancellationToken);
                foreach (var candidate in candidates)
                {
                    duplicateIssues.Add(new QuestionValidationIssue(
                        "error",
                        "duplicate_existing_question",
                        $"Possible duplicate with existing FE question '{candidate.Title}' (id={candidate.QuestionId}, similarity={candidate.SimilarityScore}).",
                        question.Title));
                }
            }
        }
        else
        {
            foreach (var question in schemaMapping.PeQuestions)
            {
                var candidates = await _moduleRepository.FindQuestionDuplicateCandidatesAsync(
                    courseId,
                    "PE",
                    question.Title,
                    question.Description,
                    question.TopicTags,
                    cancellationToken);
                foreach (var candidate in candidates)
                {
                    duplicateIssues.Add(new QuestionValidationIssue(
                        "error",
                        "duplicate_existing_question",
                        $"Possible duplicate with existing PE question '{candidate.Title}' (id={candidate.QuestionId}, similarity={candidate.SimilarityScore}).",
                        question.Title));
                }
            }
        }

        if (duplicateIssues.Count == 0)
        {
            return schemaMapping;
        }

        var mergedIssues = schemaMapping.Issues.Concat(duplicateIssues).ToList();
        return schemaMapping with
        {
            IsValid = false,
            Issues = mergedIssues
        };
    }
}
