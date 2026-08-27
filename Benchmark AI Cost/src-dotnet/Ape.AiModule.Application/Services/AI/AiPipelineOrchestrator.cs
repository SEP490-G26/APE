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

    public AiPipelineOrchestrator(
        IAiProviderGateway aiProviderGateway,
        IDocumentParser documentParser,
        IChunkingService chunkingService,
        IModuleRepository moduleRepository,
        IQuestionSchemaService questionSchemaService,
        IQuestionPersistenceMapper questionPersistenceMapper,
        IGatekeeperPolicyService gatekeeperPolicyService,
        IExtractedContentPolicyService extractedContentPolicyService,
        IEmbeddingTaggingPolicyService embeddingTaggingPolicyService,
        ICodeMentorPolicyService codeMentorPolicyService,
        IRubricCatalogService rubricCatalogService,
        IGroundTruthCatalogService groundTruthCatalogService,
        IDifficultyAlignmentService difficultyAlignmentService)
    {
        _aiProviderGateway = aiProviderGateway;
        _documentParser = documentParser;
        _chunkingService = chunkingService;
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
        var stageLogs = new List<PipelineStageLog>
        {
            CreateStageLog("extract-content", InferStageStatus(cost), cost, new Dictionary<string, string>
            {
                ["parser"] = extracted.ParserName,
                ["embedded_images"] = extracted.EmbeddedImageCount.ToString(),
                ["words"] = extracted.WordCount.ToString()
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
                    extracted))
        };

        return new ExtractedContentResult(extracted, stageLogs, usageLogs, cost);
    }

    public async Task<ExtractedContentDebugResult> RunExtractedContentDebugAsync(ExtractedContentRequest request, CancellationToken cancellationToken)
    {
        var result = await RunExtractedContentAsync(request, cancellationToken);
        var policy = await _extractedContentPolicyService.GetPolicyAsync(cancellationToken);
        var rubric = await _rubricCatalogService.GetExtractedContentRubricAsync(cancellationToken);
        var groundTruth = await TryGetExtractedContentGroundTruthAsync(cancellationToken);

        return new ExtractedContentDebugResult(
            result.Extracted,
            policy,
            rubric,
            groundTruth,
            result.StageLogs,
            result.UsageLogs,
            result.Totals);
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

    public async Task<QuestionGenerationResult> RunQuestionGenerationAsync(QuestionGenerationRequest request, CancellationToken cancellationToken)
    {
        var questions = await _aiProviderGateway.GenerateQuestionsAsync(request, cancellationToken);
        var schemaMapping = await EnrichSchemaMappingWithRepositoryDuplicatesAsync(
            request.CourseId,
            _questionSchemaService.ValidateAndMap(request.Subject, request.QuestionType, questions, request.Chunks),
            cancellationToken);
        var difficultyAlignment = _difficultyAlignmentService.Evaluate(request.Subject, request.QuestionType, request.Difficulty, questions);
        var mappedFeQuestions = _questionPersistenceMapper.MapFeQuestions(schemaMapping, request.UserId, BuildPipelineRunId("qgenmap"), new ReviewDecision("draft", [], [], 0, schemaMapping.IsValid, true, !schemaMapping.IsValid, request.GeneratorModel), request.CourseId, request.IsPublic);
        var mappedPeQuestions = _questionPersistenceMapper.MapPeQuestions(schemaMapping, request.UserId, BuildPipelineRunId("qgenmap"), new ReviewDecision("draft", [], [], 0, schemaMapping.IsValid, true, !schemaMapping.IsValid, request.GeneratorModel), request.CourseId, request.IsPublic);
        var cost = ResolveStageCost(
            "question-generation",
            request.GeneratorModel,
            request.Chunks.Sum(static chunk => chunk.ContentText.Length),
            JsonSerializer.Serialize(questions).Length,
            "question-generation");
        var stageLogs = new List<PipelineStageLog>
        {
            CreateStageLog("question-generation", InferStageStatus(cost), cost, new Dictionary<string, string>
            {
                ["question_type"] = request.QuestionType,
                ["difficulty"] = request.Difficulty,
                ["count"] = questions.Count.ToString(),
                ["schema_valid"] = schemaMapping.IsValid.ToString(),
                ["schema_issue_count"] = schemaMapping.Issues.Count.ToString(),
                ["difficulty_aligned"] = difficultyAlignment.IsAligned.ToString(),
                ["estimated_difficulty"] = difficultyAlignment.EstimatedDifficulty,
                ["duplicate_issue_count"] = schemaMapping.Issues.Count(issue => issue.Code == "duplicate_existing_question").ToString()
            }, InferProvider(request.GeneratorModel), request.GeneratorModel)
        };
        var usageLogs = new List<AIUsageLogEntry>
        {
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
                    new { request.CourseId, request.DocumentId, request.IsPublic, request.Count, request.RevisionFeedback, request.PreviousQuestions, request.Chunks },
                    questions))
        };

        return new QuestionGenerationResult(questions, schemaMapping, difficultyAlignment, mappedFeQuestions, mappedPeQuestions, stageLogs, usageLogs, cost);
    }

    public Task<ReviewDecision> RunQuestionReviewAsync(QuestionReviewRequest request, CancellationToken cancellationToken)
        => _aiProviderGateway.ReviewQuestionsAsync(request, cancellationToken);

    public async Task<IngestionResult> RunIngestionAsync(IngestionRequest request, CancellationToken cancellationToken)
    {
        var gatekeeperResult = await RunGatekeeperAsync(new GatekeeperRequest(request.UserId, request.FileName, request.Subject, request.Language, request.RawContent, request.GatekeeperModel), cancellationToken);
        var extractedResult = await RunExtractedContentAsync(new ExtractedContentRequest(request.UserId, request.FileName, request.Subject, request.Language, request.RawContent, request.Mode, request.VisionModel), cancellationToken);
        var embeddingResult = await RunEmbeddingTaggingAsync(new EmbeddingTaggingRequest(request.UserId, request.FileName, request.Subject, request.Language, extractedResult.Extracted.NormalizedMarkdown, request.Mode, request.EmbeddingModel, request.TaggingModel, request.AllowedTags), cancellationToken);

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
        var stageLogs = new List<PipelineStageLog>();
        var usageLogs = new List<AIUsageLogEntry>();
        IReadOnlyList<GeneratedQuestion> questions = [];
        ReviewDecision review = new("needs_revision", ["Generation did not run."], ["Retry the generation flow."], 0, false, false, true, request.ReviewerModel);
        var attemptsToUse = request.Mode == GenerationReviewMode.SingleAgent
            ? 1
            : Math.Clamp(request.MaxAttempts <= 0 ? 2 : request.MaxAttempts, 1, 2);
        string? revisionFeedback = null;
        IReadOnlyList<GeneratedQuestion>? previousQuestions = null;
        var attemptsUsed = 0;
        var pipelineRunId = $"genrev-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
        var reviewerModel = request.Mode == GenerationReviewMode.SingleAgent ? request.GeneratorModel : request.ReviewerModel;

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
                request.Chunks), cancellationToken);

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
                        request.Chunks,
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
                    request.Chunks), cancellationToken);
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
                var mergedIssues = review.Issues.Concat([difficultyIssue]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var mergedSuggestions = review.Suggestions.Concat(["Adjust the generated question so that the reasoning load matches the requested difficulty."]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                review = review with
                {
                    ReviewStatus = "needs_revision",
                    Issues = mergedIssues,
                    Suggestions = mergedSuggestions,
                    Score = Math.Min(review.Score, 0.55m),
                    NeedsRevision = true
                };
            }

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
                            request.Chunks,
                            questions
                        },
                        review)));
            }

            if (!review.NeedsRevision && review.ReviewStatus.Equals("accepted", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            revisionFeedback = string.Join("\n", review.Issues.Concat(review.Suggestions));
            previousQuestions = questions;
        }

        var finalSchemaMapping = await EnrichSchemaMappingWithRepositoryDuplicatesAsync(
            request.CourseId,
            _questionSchemaService.ValidateAndMap(request.Subject, request.QuestionType, questions, request.Chunks),
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
            SumCosts(stageLogs.Select(static item => item.Cost)));
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
                    chunk.Language)).ToList()),
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
            ["issues_count"] = feedback.Issues.Count.ToString(),
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
                    null,
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
