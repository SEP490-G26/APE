/**
 * QuestionGenerationReviewService.cs
 * The core orchestration engine for Step 4 of the APE AI Pipeline:
 * Multi-Agent Question Generation, Static Rule Review, Rubric Evaluation, and Self-Correction.
 * 
 * Multi-Agent Workflow:
 * 1. RAG Context Packing: Selects top relevant context chunks for the target chapter/topic.
 * 2. Question Generator Agent (OpenAI / DeepSeek fallback): Generates questions based on prompt & context.
 * 3. Parser & Normalizer: Cleans and structures the raw LLM JSON response.
 * 4. Rule-Based Review Engine: Performs deterministic checks (OOP compliance, demo code replication, test case schemas).
 * 5. Reviewer Agent (OpenAI Mini / DeepSeek Flash fallback): Evaluates pedagogical quality against rubric JSON.
 * 6. Self-Repair & Attempt Loop: If issues are found, triggers Repair Agent or re-runs generation (up to 3 attempts)
 *    injecting review feedback into the next attempt.
 */

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Options;
using Domain.Constants;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.Services;

/// <summary>
/// Service implementing multi-agent iterative question generation, validation, and self-correction.
/// </summary>
public class QuestionGenerationReviewService : IQuestionGenerationReviewService
{
    private const int CLanguageId = 50;
    private const int JavaLanguageId = 62;
    private const int MaxDifficultyBuckets = 3;
    private const int MaxPromptChunks = 6;
    private const int MaxPromptChunksPeMediumHard = 3;
    private const int MaxChunkContentCharacters = 700;
    private const int MaxChunkContentCharactersPeMediumHard = 520;
    private const int MaxChunkSelectionReasons = 4;
    private const int MaxReviewCodeCharacters = 1800;
    private const int MaxPreviousQuestionCodeCharacters = 500;
    private const int MaxReviewTestCases = 3;
    private const int MaxPreviousQuestionTestCases = 2;
    private const int MaxPeRetryFeedbackItems = 6;
    private const int MaxPeRetryFeedbackCharacters = 900;
    private const int MixedEasyMinimumPackedTokens = 40;
    private const int MixedMediumMinimumPackedTokens = 120;
    private const int MixedHardMinimumPackedTokens = 260;
    private const decimal MixedMediumMinimumCoverageRatio = 0.5m;
    private const decimal MixedHardMinimumCoverageRatio = 0.85m;
    private const double FeSemanticDuplicateThreshold = 0.94d;
    private const double PeSemanticDuplicateThreshold = 0.935d;
    private const double FeStructuralDuplicateThreshold = 0.72d;
    private const double PeStructuralDuplicateThreshold = 0.68d;
    private const int MaxSemanticComparisonEntries = 24;
    private static readonly HashSet<string> DuplicateKeywordStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "of", "for", "to", "in", "on", "with", "and", "or", "by",
        "is", "are", "be", "from", "that", "this", "these", "those", "using", "use",
        "given", "write", "complete", "implement", "create", "build", "question", "program",
        "code", "method", "methods", "class", "classes", "object", "objects", "main",
        "print", "return", "value", "values", "output", "input", "result", "results",
        "following", "correct", "best", "option", "options", "statement", "choose",
        "select", "which", "what", "why", "how", "when", "where", "does", "do", "can"
    };

    private readonly IAIExecutionService _aiExecutionService;
    private readonly IAIFeatureRoutingService _routingService;
    private readonly IAIPromptService _promptService;
    private readonly IAIArtifactCatalogService _artifactCatalogService;
    private readonly IAIContextPackRepository _contextPackRepository;
    private readonly IRetrievalPlannerService _retrievalPlannerService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IFEQuestionRepository _feQuestionRepository;
    private readonly IPEQuestionRepository _peQuestionRepository;
    private readonly IAIUsageLogRepository _aiUsageLogRepository;
    private readonly IAIVndBillingService _vndBillingService;
    private readonly ILogger<QuestionGenerationReviewService> _logger;

    public QuestionGenerationReviewService(
        IAIExecutionService aiExecutionService,
        IAIFeatureRoutingService routingService,
        IAIPromptService promptService,
        IAIArtifactCatalogService artifactCatalogService,
        IAIContextPackRepository contextPackRepository,
        IRetrievalPlannerService retrievalPlannerService,
        IDocumentRepository documentRepository,
        IFEQuestionRepository feQuestionRepository,
        IPEQuestionRepository peQuestionRepository,
        IAIUsageLogRepository aiUsageLogRepository,
        IAIVndBillingService vndBillingService,
        ILogger<QuestionGenerationReviewService> logger)
    {
        _aiExecutionService = aiExecutionService;
        _routingService = routingService;
        _promptService = promptService;
        _artifactCatalogService = artifactCatalogService;
        _contextPackRepository = contextPackRepository;
        _retrievalPlannerService = retrievalPlannerService;
        _documentRepository = documentRepository;
        _feQuestionRepository = feQuestionRepository;
        _peQuestionRepository = peQuestionRepository;
        _aiUsageLogRepository = aiUsageLogRepository;
        _vndBillingService = vndBillingService;
        _logger = logger;
    }

    public async Task<GenerationReviewResultDto> RunAsync(GenerationReviewRequestDto request, CancellationToken cancellationToken = default)
    {
        NormalizeRequest(request);
        ValidateRequest(request);

        if (IsMixedDifficultyRequest(request))
        {
            return await RunMixedDifficultyAsync(request, cancellationToken);
        }

        return await RunSingleDifficultyAsync(request, cancellationToken);
    }

    private async Task<GenerationReviewResultDto> RunSingleDifficultyAsync(GenerationReviewRequestDto request, CancellationToken cancellationToken)
    {
        if (!ShouldUseBatchedExecution(request))
        {
            return await RunSingleDifficultyCoreAsync(request, cancellationToken);
        }

        var shouldPersistSideEffects = !request.SuppressPersistenceSideEffects;
        var totalStopwatch = Stopwatch.StartNew();
        var stageTimings = new List<string>();
        var stageStopwatch = Stopwatch.StartNew();

        if (shouldPersistSideEffects && !request.UseActualCostOnly)
        {
            await _vndBillingService.EnsureMinimumBalanceAsync(request.UserId, cancellationToken);
        }
        stageTimings.Add($"ensure_balance_ms={stageStopwatch.ElapsedMilliseconds}");

        stageStopwatch.Restart();
        var chunkResolution = await ResolveChunksAsync(request, cancellationToken);
        stageTimings.Add($"resolve_chunks_ms={stageStopwatch.ElapsedMilliseconds}");
        if (chunkResolution.Chunks.Count == 0)
        {
            throw new InvalidOperationException("Question generation requires at least one chunk or a valid context pack.");
        }

        var acceptedQuestions = new List<GeneratedQuestionCandidateDto>();
        var duplicateTracker = new RequestDuplicateTracker();
        var batchResults = new List<GenerationReviewResultDto>();
        var requestedCount = request.Count;
        var initialBatchPlans = BuildBatchPlans(request, request.Difficulty, request.Count);
        var refillBudget = ResolveRefillBudget(request);
        var refillAttemptsUsed = 0;
        var stalledRefillRounds = 0;

        foreach (var batchPlan in initialBatchPlans)
        {
            if (acceptedQuestions.Count >= requestedCount)
            {
                break;
            }

            var batchRequest = CreatePlannedBatchRequest(request, batchPlan, chunkResolution);
            var batchResult = await RunSingleDifficultyCoreAsync(batchRequest, cancellationToken, acceptedQuestions);
            batchResults.Add(batchResult);
            await AccumulateBatchOutcomeAsync(batchResult, acceptedQuestions, duplicateTracker, request.QuestionType, cancellationToken);
        }

        while (acceptedQuestions.Count < requestedCount && refillAttemptsUsed < refillBudget)
        {
            refillAttemptsUsed += 1;
            var remaining = requestedCount - acceptedQuestions.Count;
            var refillPlan = new GenerationBatchPlan(request.Difficulty, Math.Min(ResolveBatchSize(request), remaining), true);
            var refillRequest = CreatePlannedBatchRequest(request, refillPlan, chunkResolution);
            var refillGuidance = BuildRefillGuidance(request, acceptedQuestions, batchResults, remaining);
            var refillResult = await RunSingleDifficultyCoreAsync(refillRequest, cancellationToken, acceptedQuestions, refillGuidance);
            batchResults.Add(refillResult);
            var acceptedBefore = acceptedQuestions.Count;
            await AccumulateBatchOutcomeAsync(refillResult, acceptedQuestions, duplicateTracker, request.QuestionType, cancellationToken);
            if (acceptedQuestions.Count == acceptedBefore)
            {
                stalledRefillRounds += 1;
                if (ShouldStopRefillAfterNoProgress(refillResult) || stalledRefillRounds >= 2)
                {
                    break;
                }
            }
            else
            {
                stalledRefillRounds = 0;
            }
        }

        stageTimings.Add($"batch_execution_ms={totalStopwatch.ElapsedMilliseconds}");

        var aggregatedReview = AggregateBatchResultsForSingleDifficulty(batchResults, requestedCount, acceptedQuestions.Count);
        var persistence = await PersistQuestionsAsync(request, acceptedQuestions, aggregatedReview, chunkResolution.Chunks, cancellationToken);
        var shortfallReport = BuildShortfallReport(request, chunkResolution, acceptedQuestions.Count, aggregatedReview);
        var aggregatedMetrics = AggregateMetrics(batchResults);

        if (shouldPersistSideEffects)
        {
            var totalReportedCostUsd = batchResults.Sum(item => item.Metrics.TotalReportedCostUsd);
            var charge = request.UseActualCostOnly
                ? await _vndBillingService.RecordUsageAsync(
                    new AIVndChargeRequest
                    {
                        UserId = request.UserId,
                        FeatureKey = AICreditFeatureKeys.QuestionGenerationReview,
                        FeatureName = "question generation",
                        SourceEntityType = "GenerationRun",
                        SourceEntityId = $"gr-batched-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..36],
                        CreatedBy = request.UserId,
                        ReportedCostUsd = totalReportedCostUsd,
                        UsageLogIds = new List<string>()
                    },
                    cancellationToken)
                : await _vndBillingService.ChargeAsync(
                    new AIVndChargeRequest
                    {
                        UserId = request.UserId,
                        FeatureKey = AICreditFeatureKeys.QuestionGenerationReview,
                        FeatureName = "question generation",
                        SourceEntityType = "GenerationRun",
                        SourceEntityId = $"gr-batched-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..36],
                        CreatedBy = request.UserId,
                        ReportedCostUsd = totalReportedCostUsd,
                        UsageLogIds = new List<string>()
                    },
                    cancellationToken);

            aggregatedMetrics.UsdToVndRate = charge.UsdToVndRate;
            aggregatedMetrics.ChargedVnd = charge.ChargedVnd;
            aggregatedMetrics.ActualDeductedVnd = charge.ActualDeductedVnd;
            aggregatedMetrics.AbsorbedVnd = charge.AbsorbedVnd;
            aggregatedMetrics.RemainingBalanceVnd = charge.BalanceAfterVnd;
        }

        aggregatedMetrics.PersistedFeQuestionIds = persistence.FeQuestionIds;
        aggregatedMetrics.PersistedPeQuestionIds = persistence.PeQuestionIds;

        totalStopwatch.Stop();
        _logger.LogInformation(
            "Question generation/review completed with batched execution. Scope={Scope}, DocumentId={DocumentId}, CourseId={CourseId}, Subject={Subject}, QuestionType={QuestionType}, Requested={Requested}, Accepted={Accepted}, BatchCount={BatchCount}, RefillAttempts={RefillAttempts}, TotalMs={TotalMs}",
            request.Retrieval?.SourceScope ?? (string.IsNullOrWhiteSpace(request.DocumentId) ? "SYSTEM" : "BYOS"),
            request.DocumentId,
            request.CourseId,
            request.Subject,
            request.QuestionType,
            requestedCount,
            acceptedQuestions.Count,
            batchResults.Count,
            refillAttemptsUsed,
            totalStopwatch.ElapsedMilliseconds);

        return new GenerationReviewResultDto
        {
            CourseId = request.CourseId,
            DocumentId = request.DocumentId,
            SourceScope = request.Retrieval?.SourceScope
                ?? chunkResolution.ContextPack?.SourceScope
                ?? (string.IsNullOrWhiteSpace(request.DocumentId) ? "SYSTEM" : "BYOS"),
            Subject = request.Subject,
            QuestionType = request.QuestionType,
            DifficultyMode = "single",
            DifficultyProfile = BuildSingleDifficultyProfile(request),
            DifficultyReports = new List<DifficultyGenerationReportDto>
            {
                BuildDifficultyReport(request.Difficulty, request.Count, acceptedQuestions.Count, aggregatedReview, shortfallReport, persistence)
            },
            Difficulty = request.Difficulty,
            Mode = request.Mode,
            AttemptsUsed = batchResults.Sum(item => item.AttemptsUsed),
            GeneratorModel = batchResults.Select(item => item.GeneratorModel).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? string.Empty,
            ReviewerModel = batchResults.Select(item => item.ReviewerModel).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? string.Empty,
            Questions = acceptedQuestions.Take(requestedCount).ToList(),
            Review = aggregatedReview,
            ShortfallReport = shortfallReport,
            Persistence = persistence,
            ContextPack = chunkResolution.ContextPack,
            RetrievalPlan = chunkResolution.RetrievalPlan,
            Metrics = aggregatedMetrics
        };
    }

    private async Task<GenerationReviewResultDto> RunSingleDifficultyCoreAsync(
        GenerationReviewRequestDto request,
        CancellationToken cancellationToken,
        IReadOnlyList<GeneratedQuestionCandidateDto>? seededPreviousQuestions = null,
        string? seededRevisionFeedback = null)
    {
        var shouldPersistSideEffects = !request.SuppressPersistenceSideEffects;
        var totalStopwatch = Stopwatch.StartNew();
        var stageStopwatch = Stopwatch.StartNew();
        var stageTimings = new List<string>();
        if (shouldPersistSideEffects && !request.UseActualCostOnly)
        {
            await _vndBillingService.EnsureMinimumBalanceAsync(request.UserId, cancellationToken);
        }
        stageTimings.Add($"ensure_balance_ms={stageStopwatch.ElapsedMilliseconds}");
        var generationRunId = $"gr-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..36];
        stageStopwatch.Restart();
        var chunkResolution = await ResolveChunksAsync(request, cancellationToken);
        stageTimings.Add($"resolve_chunks_ms={stageStopwatch.ElapsedMilliseconds}");
        if (chunkResolution.Chunks.Count == 0)
        {
            throw new InvalidOperationException("Question generation requires at least one chunk or a valid context pack.");
        }

        stageStopwatch.Restart();
        var generatorResolved = _routingService.ResolveText(AIFeatureNames.QuestionGeneration, request.GeneratorOverride);
        var reviewerResolved = ResolveReviewerOptions(request, generatorResolved);
        var questionRubricArtifact = await LoadRubricArtifactAsync(request.Subject, request.QuestionType, cancellationToken);
        var questionRubric = questionRubricArtifact.Content;
        var reviewPolicyArtifact = await _artifactCatalogService.GetArtifactAsync("question-generation-review-policy.json", cancellationToken);
        var reviewPolicy = ParseQuestionGenerationReviewPolicy(reviewPolicyArtifact?.Content);
        var generatorPrompt = await _artifactCatalogService.GetPromptAsync("question_generation", cancellationToken)
            ?? throw new InvalidOperationException("Question generation prompt is not configured.");
        var reviewerPrompt = await _artifactCatalogService.GetPromptAsync("question_review", cancellationToken)
            ?? throw new InvalidOperationException("Question review prompt is not configured.");
        var repairPrompt = await _artifactCatalogService.GetPromptAsync("question_generation_repair", cancellationToken);
        stageTimings.Add($"load_artifacts_ms={stageStopwatch.ElapsedMilliseconds}");

        stageStopwatch.Restart();
        var shortCircuit = EvaluateSourcePoorShortCircuit(request, chunkResolution, reviewPolicy);
        shortCircuit ??= EvaluateGenericQueuePeScopeShortCircuit(request, chunkResolution);
        shortCircuit ??= EvaluateJavaInheritancePeScopeShortCircuit(request, chunkResolution);
        shortCircuit ??= EvaluateDifficultyAdequacyShortCircuit(request, chunkResolution);
        stageTimings.Add($"short_circuit_eval_ms={stageStopwatch.ElapsedMilliseconds}");
        if (shortCircuit is not null)
        {
            var emptyPersistence = new QuestionPersistenceResultDto { Persisted = false };
            if (shouldPersistSideEffects)
            {
                await UpdateContextPackAuditAsync(chunkResolution, generationRunId, shortCircuit, emptyPersistence, cancellationToken);
            }
            var shortfallReport = BuildShortfallReport(request, chunkResolution, 0, shortCircuit);
            if (shortfallReport is not null)
            {
                _logger.LogInformation(
                    "Generation shortfall detected. RunId={RunId}, Scope={Scope}, Requested={Requested}, Generated={Generated}, StopReason={StopReason}, Coverage={CoverageRatio}, PackedTokens={PackedTokenCount}, DominantChapter={DominantChapter}, UncoveredTopics={UncoveredTopics}",
                    generationRunId,
                    request.Retrieval?.SourceScope ?? (string.IsNullOrWhiteSpace(request.DocumentId) ? "SYSTEM" : "BYOS"),
                    shortfallReport.RequestedCount,
                    shortfallReport.GeneratedCount,
                    shortfallReport.StopReason,
                    shortfallReport.CoverageRatio,
                    shortfallReport.PackedTokenCount,
                    shortfallReport.DominantChapter,
                    string.Join(", ", shortfallReport.UncoveredTopics));
            }
            return new GenerationReviewResultDto
            {
                CourseId = request.CourseId,
                DocumentId = request.DocumentId,
                SourceScope = request.Retrieval?.SourceScope
                    ?? chunkResolution.ContextPack?.SourceScope
                    ?? (string.IsNullOrWhiteSpace(request.DocumentId) ? "SYSTEM" : "BYOS"),
                Subject = request.Subject,
                QuestionType = request.QuestionType,
                DifficultyMode = "single",
                DifficultyProfile = BuildSingleDifficultyProfile(request),
                DifficultyReports = new List<DifficultyGenerationReportDto>
                {
                    BuildDifficultyReport(request.Difficulty, request.Count, 0, shortCircuit, shortfallReport, emptyPersistence)
                },
                Difficulty = request.Difficulty,
                Mode = request.Mode,
                AttemptsUsed = 0,
                GeneratorModel = generatorResolved.Model,
                ReviewerModel = reviewerResolved.Model,
                Questions = new List<GeneratedQuestionCandidateDto>(),
                Review = shortCircuit,
                ShortfallReport = shortfallReport,
                Persistence = emptyPersistence,
                ContextPack = chunkResolution.ContextPack,
                RetrievalPlan = chunkResolution.RetrievalPlan,
                Metrics = new GenerationMetricsDto
                {
                    GeneratorProvider = generatorResolved.Provider,
                    GeneratorConfiguredModel = generatorResolved.Model,
                    GeneratorEffectiveModel = generatorResolved.Model,
                    GeneratorModelFamily = AIUsageAccounting.NormalizeModelFamily(generatorResolved.Model),
                    ReviewerProvider = reviewerResolved.Provider,
                    ReviewerConfiguredModel = reviewerResolved.Model,
                    ReviewerEffectiveModel = reviewerResolved.Model,
                    ReviewerModelFamily = AIUsageAccounting.NormalizeModelFamily(reviewerResolved.Model),
                    GenerationRunId = generationRunId,
                    ChunkCount = chunkResolution.Chunks.Count,
                    ContextPackId = chunkResolution.ContextPack?.PackId ?? string.Empty,
                    TotalReportedCostUsd = 0m,
                    UsdToVndRate = 0m,
                    ChargedVnd = 0,
                    ActualDeductedVnd = 0,
                    AbsorbedVnd = 0,
                    RemainingBalanceVnd = 0,
                    GenerationUsageSource = "short_circuit_pre_generation",
                    ReviewUsageSource = "short_circuit_pre_generation",
                    GenerationCostSource = "short_circuit_pre_generation",
                    ReviewCostSource = "short_circuit_pre_generation",
                    GeneratorPromptKey = generatorPrompt.Key,
                    GeneratorPromptVersion = generatorPrompt.Version,
                    ReviewerPromptKey = reviewerPrompt.Key,
                    ReviewerPromptVersion = reviewerPrompt.Version,
                    RubricKey = questionRubricArtifact.ArtifactKey,
                    RubricVersion = questionRubricArtifact.Version
                }
            };
        }

        var attempts = ResolveAttemptCount(request);

        List<GeneratedQuestionCandidateDto> latestQuestions = new();
        var acceptedQuestions = new List<GeneratedQuestionCandidateDto>();
        var duplicateTracker = new RequestDuplicateTracker();
        SeedAcceptedQuestionKeys(
            seededPreviousQuestions,
            duplicateTracker.AcceptedFingerprints,
            duplicateTracker.AcceptedTitles,
            duplicateTracker.AcceptedStructuralSignatures);
        await SeedSemanticDuplicateEntriesAsync(seededPreviousQuestions, duplicateTracker.AcceptedSemanticEntries, cancellationToken);
        await SeedHistoricalScopeDuplicatesAsync(request, duplicateTracker, cancellationToken);
        var attemptReviews = new List<QuestionReviewDecisionDto>();
        QuestionReviewDecisionDto latestReview = new()
        {
            ReviewStatus = "needs_revision",
            NeedsRevision = true,
            ReviewerModel = reviewerResolved.Model,
            ReviewMode = request.Mode,
            Issues = new List<string> { "Generation was not executed." },
            Suggestions = new List<string> { "Retry the pipeline." }
        };
        string? revisionFeedback = seededRevisionFeedback;
        List<GeneratedQuestionCandidateDto>? previousQuestions = seededPreviousQuestions?.Select(CloneQuestionCandidate).ToList();
        var attemptsUsed = 0;
        var usageLogIds = new List<string>();
        AITextResponse? lastGenerationResponse = null;
        AITextResponse? lastReviewResponse = null;
        long totalGenerationMs = 0;
        long totalRepairMs = 0;
        long totalReviewMs = 0;
        long totalRuleReviewMs = 0;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            attemptsUsed = attempt;
            var remainingNeeded = Math.Max(0, request.Count - acceptedQuestions.Count);
            if (remainingNeeded == 0)
            {
                break;
            }

            var attemptRequest = CloneRequest(request);
            attemptRequest.Count = remainingNeeded;
            if (attemptRequest.Retrieval is not null)
            {
                attemptRequest.Retrieval.RequestedQuestionCount = remainingNeeded;
                attemptRequest.Retrieval.Difficulty = attemptRequest.Difficulty;
            }

            stageStopwatch.Restart();
            var generationPromptFootprint = EstimateGenerationPromptFootprint(
                attemptRequest,
                chunkResolution.Chunks,
                questionRubric,
                reviewPolicy,
                revisionFeedback,
                previousQuestions);
            var generationResponse = await GenerateQuestionsAsync(
                attemptRequest,
                chunkResolution.Chunks,
                generatorResolved,
                generatorPrompt,
                questionRubric,
                reviewPolicy,
                revisionFeedback,
                previousQuestions,
                cancellationToken);
            totalGenerationMs += stageStopwatch.ElapsedMilliseconds;
            lastGenerationResponse = generationResponse;
            _logger.LogInformation(
                "Generation attempt completed. RunId={RunId}, Attempt={Attempt}, Requested={Requested}, PromptChars={PromptChars}, ChunkCount={ChunkCount}, PreviousQuestions={PreviousQuestions}, InputTokens={InputTokens}, OutputTokens={OutputTokens}, DurationMs={DurationMs}",
                generationRunId,
                attempt,
                attemptRequest.Count,
                generationPromptFootprint.TotalChars,
                generationPromptFootprint.ChunkCount,
                generationPromptFootprint.PreviousQuestionCount,
                generationResponse.InputTokens,
                generationResponse.OutputTokens,
                stageStopwatch.ElapsedMilliseconds);
            if (shouldPersistSideEffects)
            {
                usageLogIds.Add(await LogUsageAsync(
                    request,
                    AIAgentCatalog.QuestionGeneratorAgentId,
                    "question_generation",
                    generatorResolved,
                    generationResponse,
                    attempt,
                    chunkResolution,
                    new Dictionary<string, object?>
                    {
                        ["revision_feedback"] = revisionFeedback,
                        ["previous_question_count"] = previousQuestions?.Count ?? 0,
                        ["generation_run_id"] = generationRunId
                    },
                    generatorPrompt,
                    reviewPolicyArtifact,
                    questionRubricArtifact,
                    cancellationToken));
            }

            var parsedQuestions = ParseQuestions(generationResponse.Content);
            if (parsedQuestions.Count == 0 && repairPrompt is not null)
            {
                if (CanAttemptRepair(request.QuestionType, chunkResolution.Chunks))
                {
                    stageStopwatch.Restart();
                    var repairPromptFootprint = EstimateRepairPromptFootprint(
                        request,
                        chunkResolution.Chunks,
                        questionRubric,
                        reviewPolicy,
                        generationResponse.Content);
                    var repairResult = await AttemptRepairAsync(
                        request,
                        chunkResolution.Chunks,
                        generatorResolved,
                        repairPrompt,
                        questionRubric,
                        reviewPolicy,
                        generationResponse.Content,
                        cancellationToken);
                    totalRepairMs += stageStopwatch.ElapsedMilliseconds;
                    parsedQuestions = repairResult.Questions;
                    _logger.LogWarning(
                        "Generation repair executed. RunId={RunId}, Attempt={Attempt}, PromptChars={PromptChars}, ChunkCount={ChunkCount}, BrokenResponseChars={BrokenResponseChars}, ParsedQuestions={ParsedQuestions}, InputTokens={InputTokens}, OutputTokens={OutputTokens}, DurationMs={DurationMs}",
                        generationRunId,
                        attempt,
                        repairPromptFootprint.TotalChars,
                        repairPromptFootprint.ChunkCount,
                        repairPromptFootprint.BrokenResponseChars,
                        parsedQuestions.Count,
                        repairResult.Response.InputTokens,
                        repairResult.Response.OutputTokens,
                        stageStopwatch.ElapsedMilliseconds);
                    if (shouldPersistSideEffects)
                    {
                        usageLogIds.Add(await LogUsageAsync(
                            request,
                            AIAgentCatalog.QuestionGeneratorAgentId,
                            "question_generation_repair",
                            generatorResolved,
                            repairResult.Response,
                            attempt,
                            chunkResolution,
                            new Dictionary<string, object?>
                            {
                                ["broken_response_present"] = true,
                                ["generation_run_id"] = generationRunId
                            },
                            repairPrompt,
                            reviewPolicyArtifact,
                            questionRubricArtifact,
                            cancellationToken));
                    }
                }
            }

            latestQuestions = NormalizeQuestions(
                parsedQuestions,
                attemptRequest.QuestionType,
                chunkResolution.Chunks,
                chunkResolution.Chunks.Select(chunk => chunk.ChunkId));
            _logger.LogInformation(
                "Generation normalization completed. RunId={RunId}, Attempt={Attempt}, ParsedQuestions={ParsedQuestions}, NormalizedQuestions={NormalizedQuestions}, RemainingNeededBeforeAccept={RemainingNeeded}",
                generationRunId,
                attempt,
                parsedQuestions.Count,
                latestQuestions.Count,
                remainingNeeded);

            stageStopwatch.Restart();
            var ruleReview = await BuildRuleBasedReviewAsync(
                attemptRequest,
                latestQuestions,
                chunkResolution.Chunks,
                previousQuestions,
                reviewPolicy,
                reviewerResolved.Model,
                cancellationToken);
            totalRuleReviewMs += stageStopwatch.ElapsedMilliseconds;
            _logger.LogInformation(
                "Rule review completed. RunId={RunId}, Attempt={Attempt}, Issues={IssueCount}, NeedsRevision={NeedsRevision}, SchemaValid={SchemaValid}, Grounded={Grounded}, DurationMs={DurationMs}",
                generationRunId,
                attempt,
                ruleReview.Issues.Count,
                ruleReview.NeedsRevision,
                ruleReview.SchemaValid,
                ruleReview.ContentGrounded,
                stageStopwatch.ElapsedMilliseconds);

            if (string.Equals(request.Mode, "SingleAgent", StringComparison.OrdinalIgnoreCase))
            {
                latestReview = ruleReview;
            }
            else
            {
                stageStopwatch.Restart();
                var reviewPromptFootprint = EstimateReviewPromptFootprint(
                    attemptRequest,
                    latestQuestions,
                    chunkResolution.Chunks,
                    questionRubric,
                    reviewPolicy);
                var llmReviewResponse = await ReviewQuestionsAsync(
                    attemptRequest,
                    latestQuestions,
                    chunkResolution.Chunks,
                    reviewerResolved,
                    reviewerPrompt,
                    questionRubric,
                    reviewPolicy,
                    cancellationToken);
                totalReviewMs += stageStopwatch.ElapsedMilliseconds;
                lastReviewResponse = llmReviewResponse;
                _logger.LogInformation(
                    "LLM review completed. RunId={RunId}, Attempt={Attempt}, Questions={Questions}, PromptChars={PromptChars}, ChunkCount={ChunkCount}, InputTokens={InputTokens}, OutputTokens={OutputTokens}, DurationMs={DurationMs}",
                    generationRunId,
                    attempt,
                    latestQuestions.Count,
                    reviewPromptFootprint.TotalChars,
                    reviewPromptFootprint.ChunkCount,
                    llmReviewResponse.InputTokens,
                    llmReviewResponse.OutputTokens,
                    stageStopwatch.ElapsedMilliseconds);
                if (shouldPersistSideEffects)
                {
                    usageLogIds.Add(await LogUsageAsync(
                        request,
                        AIAgentCatalog.ReviewerAgentId,
                        "question_review",
                        reviewerResolved,
                        llmReviewResponse,
                        attempt,
                        chunkResolution,
                        new Dictionary<string, object?>
                        {
                            ["generated_question_count"] = latestQuestions.Count,
                            ["generation_run_id"] = generationRunId
                        },
                        reviewerPrompt,
                        reviewPolicyArtifact,
                        questionRubricArtifact,
                        cancellationToken));
                }
                var llmReview = ParseReview(llmReviewResponse.Content, reviewerResolved.Model, request.Mode);
                var reviewerParseFailure = IsReviewerParseFailure(llmReview);
                latestReview = reviewerParseFailure
                    ? ruleReview
                    : MergeReview(ruleReview, llmReview);

                var acceptedFromAttempt = CollectAcceptedAttemptQuestions(
                    latestQuestions,
                    ruleReview,
                    reviewerParseFailure ? null : llmReview,
                    duplicateTracker.AcceptedFingerprints,
                    duplicateTracker.AcceptedTitles);
                acceptedFromAttempt = await FilterSemanticNearDuplicatesAsync(
                    acceptedFromAttempt,
                    duplicateTracker,
                    request.QuestionType,
                    cancellationToken);
                if (acceptedFromAttempt.Count > 0)
                {
                    acceptedQuestions.AddRange(acceptedFromAttempt);
                }
            }

            if (string.Equals(request.Mode, "SingleAgent", StringComparison.OrdinalIgnoreCase))
            {
                var acceptedFromAttempt = CollectAcceptedAttemptQuestions(
                    latestQuestions,
                    ruleReview,
                    null,
                    duplicateTracker.AcceptedFingerprints,
                    duplicateTracker.AcceptedTitles);
                acceptedFromAttempt = await FilterSemanticNearDuplicatesAsync(
                    acceptedFromAttempt,
                    duplicateTracker,
                    request.QuestionType,
                    cancellationToken);
                if (acceptedFromAttempt.Count > 0)
                {
                    acceptedQuestions.AddRange(acceptedFromAttempt);
                }
            }

            attemptReviews.Add(latestReview);
            _logger.LogInformation(
                "Attempt aggregation completed. RunId={RunId}, Attempt={Attempt}, AcceptedSoFar={AcceptedSoFar}, LatestReviewStatus={ReviewStatus}, LatestIssues={IssueCount}",
                generationRunId,
                attempt,
                acceptedQuestions.Count,
                latestReview.ReviewStatus,
                latestReview.Issues.Count);
            if (string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase) &&
                latestReview.NeedsRevision &&
                latestReview.Issues.Count > 0)
            {
                _logger.LogInformation(
                    "PE attempt issues sample. RunId={RunId}, Attempt={Attempt}, IssueSample={IssueSample}",
                    generationRunId,
                    attempt,
                    string.Join(" | ", latestReview.Issues.Take(3)));
            }

            if (acceptedQuestions.Count >= request.Count)
            {
                break;
            }

            revisionFeedback = BuildRevisionFeedback(
                request,
                latestReview,
                acceptedQuestions.Count,
                request.Count - acceptedQuestions.Count,
                chunkResolution.Chunks,
                reviewPolicy);
            previousQuestions = (seededPreviousQuestions ?? Array.Empty<GeneratedQuestionCandidateDto>())
                .Concat(acceptedQuestions)
                .Concat(latestQuestions)
                .Select(CloneQuestionCandidate)
                .ToList();
        }

        if (attemptReviews.Count > 0)
        {
            latestReview = AggregateAttemptReviewsForCore(attemptReviews, request.Count, latestQuestions.Count);
        }

        if (acceptedQuestions.Count > 0)
        {
            latestQuestions = acceptedQuestions
                .Take(request.Count)
                .Select(CloneQuestionCandidate)
                .ToList();
        }
        else if (!string.Equals(latestReview.ReviewStatus, "accepted", StringComparison.OrdinalIgnoreCase) ||
                 latestReview.NeedsRevision)
        {
            latestQuestions = new List<GeneratedQuestionCandidateDto>();
        }

        stageTimings.Add($"generation_attempts_ms={totalGenerationMs}");
        stageTimings.Add($"repair_attempts_ms={totalRepairMs}");
        stageTimings.Add($"rule_review_ms={totalRuleReviewMs}");
        stageTimings.Add($"llm_review_ms={totalReviewMs}");

        stageStopwatch.Restart();
        var persistence = await PersistQuestionsAsync(request, latestQuestions, latestReview, chunkResolution.Chunks, cancellationToken);
        stageTimings.Add($"persist_questions_ms={stageStopwatch.ElapsedMilliseconds}");
        stageStopwatch.Restart();
        if (shouldPersistSideEffects)
        {
            await UpdateContextPackAuditAsync(chunkResolution, generationRunId, latestReview, persistence, cancellationToken);
        }
        stageTimings.Add($"context_pack_audit_ms={stageStopwatch.ElapsedMilliseconds}");
        var generationUsage = lastGenerationResponse is null
            ? null
            : AIUsageAccounting.ForText(
                lastGenerationResponse.Provider ?? generatorResolved.Provider,
                generatorResolved.Model,
                lastGenerationResponse.Model,
                lastGenerationResponse.InputTokens,
                lastGenerationResponse.OutputTokens,
                lastGenerationResponse.TotalTokens,
                lastGenerationResponse.ReportedCostUsd,
                lastGenerationResponse.UsageSource,
                lastGenerationResponse.CostSource);
        var reviewUsage = lastReviewResponse is null
            ? null
            : AIUsageAccounting.ForText(
                lastReviewResponse.Provider ?? reviewerResolved.Provider,
                reviewerResolved.Model,
                lastReviewResponse.Model,
                lastReviewResponse.InputTokens,
                lastReviewResponse.OutputTokens,
                lastReviewResponse.TotalTokens,
                lastReviewResponse.ReportedCostUsd,
                lastReviewResponse.UsageSource,
                lastReviewResponse.CostSource);
        var generationCostUsd = generationUsage?.CostUsd ?? 0m;
        var reviewCostUsd = reviewUsage?.CostUsd ?? 0m;
        var totalReportedCostUsd = generationCostUsd + reviewCostUsd;
        stageStopwatch.Restart();
        var charge = shouldPersistSideEffects
            ? request.UseActualCostOnly
                ? await _vndBillingService.RecordUsageAsync(
                    new AIVndChargeRequest
                    {
                        UserId = request.UserId,
                        FeatureKey = AICreditFeatureKeys.QuestionGenerationReview,
                        FeatureName = "question generation",
                        SourceEntityType = "GenerationRun",
                        SourceEntityId = generationRunId,
                        CreatedBy = request.UserId,
                        ReportedCostUsd = totalReportedCostUsd,
                        UsageLogIds = usageLogIds,
                        UsageSnapshot = new Dictionary<string, object?>
                        {
                            ["generation_tokens"] = generationUsage?.TotalTokens ?? 0,
                            ["review_tokens"] = reviewUsage?.TotalTokens ?? 0,
                            ["generation_cost_usd"] = generationCostUsd,
                            ["review_cost_usd"] = reviewCostUsd,
                            ["generation_usage_source"] = generationUsage?.UsageSource,
                            ["review_usage_source"] = reviewUsage?.UsageSource,
                            ["generation_cost_source"] = generationUsage?.CostSource,
                            ["review_cost_source"] = reviewUsage?.CostSource
                        }
                    },
                    cancellationToken)
                : await _vndBillingService.ChargeAsync(
                new AIVndChargeRequest
                {
                    UserId = request.UserId,
                    FeatureKey = AICreditFeatureKeys.QuestionGenerationReview,
                    FeatureName = "question generation",
                    SourceEntityType = "GenerationRun",
                    SourceEntityId = generationRunId,
                    CreatedBy = request.UserId,
                    ReportedCostUsd = totalReportedCostUsd,
                    UsageLogIds = usageLogIds,
                    UsageSnapshot = new Dictionary<string, object?>
                    {
                        ["generation_tokens"] = generationUsage?.TotalTokens ?? 0,
                        ["review_tokens"] = reviewUsage?.TotalTokens ?? 0,
                        ["generation_cost_usd"] = generationCostUsd,
                        ["review_cost_usd"] = reviewCostUsd,
                        ["generation_usage_source"] = generationUsage?.UsageSource,
                        ["review_usage_source"] = reviewUsage?.UsageSource,
                        ["generation_cost_source"] = generationUsage?.CostSource,
                        ["review_cost_source"] = reviewUsage?.CostSource
                    }
                },
                cancellationToken)
            : new AIVndChargeResult(
                string.Empty,
                totalReportedCostUsd,
                0m,
                0m,
                0,
                0,
                0,
                0,
                0,
                0,
                0);
        stageTimings.Add($"billing_ms={stageStopwatch.ElapsedMilliseconds}");
        totalStopwatch.Stop();
        _logger.LogInformation(
            "Question generation/review completed. RunId={RunId}, Scope={Scope}, DocumentId={DocumentId}, CourseId={CourseId}, Subject={Subject}, QuestionType={QuestionType}, ChunkCount={ChunkCount}, AttemptsUsed={AttemptsUsed}, TotalMs={TotalMs}, StageTimings={StageTimings}",
            generationRunId,
            request.Retrieval?.SourceScope ?? (string.IsNullOrWhiteSpace(request.DocumentId) ? "SYSTEM" : "BYOS"),
            request.DocumentId,
            request.CourseId,
            request.Subject,
            request.QuestionType,
            chunkResolution.Chunks.Count,
            attemptsUsed,
            totalStopwatch.ElapsedMilliseconds,
            string.Join(", ", stageTimings));
        var finalShortfallReport = BuildShortfallReport(request, chunkResolution, latestQuestions.Count, latestReview);
        if (finalShortfallReport is not null)
        {
            _logger.LogInformation(
                "Generation shortfall detected. RunId={RunId}, Scope={Scope}, Requested={Requested}, Generated={Generated}, StopReason={StopReason}, Coverage={CoverageRatio}, PackedTokens={PackedTokenCount}, DominantChapter={DominantChapter}, UncoveredTopics={UncoveredTopics}",
                generationRunId,
                request.Retrieval?.SourceScope ?? (string.IsNullOrWhiteSpace(request.DocumentId) ? "SYSTEM" : "BYOS"),
                finalShortfallReport.RequestedCount,
                finalShortfallReport.GeneratedCount,
                finalShortfallReport.StopReason,
                finalShortfallReport.CoverageRatio,
                finalShortfallReport.PackedTokenCount,
                finalShortfallReport.DominantChapter,
                string.Join(", ", finalShortfallReport.UncoveredTopics));
        }
        return new GenerationReviewResultDto
        {
            CourseId = request.CourseId,
            DocumentId = request.DocumentId,
            SourceScope = request.Retrieval?.SourceScope
                ?? chunkResolution.ContextPack?.SourceScope
                ?? (string.IsNullOrWhiteSpace(request.DocumentId) ? "SYSTEM" : "BYOS"),
            Subject = request.Subject,
            QuestionType = request.QuestionType,
            DifficultyMode = "single",
            DifficultyProfile = BuildSingleDifficultyProfile(request),
            DifficultyReports = new List<DifficultyGenerationReportDto>
            {
                BuildDifficultyReport(request.Difficulty, request.Count, latestQuestions.Count, latestReview, finalShortfallReport, persistence)
            },
            Difficulty = request.Difficulty,
            Mode = request.Mode,
            AttemptsUsed = attemptsUsed,
            GeneratorModel = lastGenerationResponse?.Model ?? generatorResolved.Model,
            ReviewerModel = lastReviewResponse?.Model ?? latestReview.ReviewerModel ?? reviewerResolved.Model,
            Questions = latestQuestions,
            Review = latestReview,
            ShortfallReport = finalShortfallReport,
            Persistence = persistence,
            ContextPack = chunkResolution.ContextPack,
            RetrievalPlan = chunkResolution.RetrievalPlan,
            Metrics = new GenerationMetricsDto
            {
                GeneratorProvider = generatorResolved.Provider,
                GeneratorConfiguredModel = generatorResolved.Model,
                GeneratorEffectiveModel = generationUsage?.EffectiveModel ?? generatorResolved.Model,
                GeneratorModelFamily = generationUsage?.ModelFamily ?? AIUsageAccounting.NormalizeModelFamily(generatorResolved.Model),
                ReviewerProvider = reviewerResolved.Provider,
                ReviewerConfiguredModel = reviewerResolved.Model,
                ReviewerEffectiveModel = reviewUsage?.EffectiveModel ?? reviewerResolved.Model,
                ReviewerModelFamily = reviewUsage?.ModelFamily ?? AIUsageAccounting.NormalizeModelFamily(reviewerResolved.Model),
                GenerationInputTokens = generationUsage?.InputTokens ?? 0,
                GenerationOutputTokens = generationUsage?.OutputTokens ?? 0,
                ReviewInputTokens = reviewUsage?.InputTokens ?? 0,
                ReviewOutputTokens = reviewUsage?.OutputTokens ?? 0,
                GenerationRunId = generationRunId,
                ChunkCount = chunkResolution.Chunks.Count,
                ContextPackId = chunkResolution.ContextPack?.PackId ?? string.Empty,
                TotalReportedCostUsd = totalReportedCostUsd,
                UsdToVndRate = charge.UsdToVndRate,
                ChargedVnd = charge.ChargedVnd,
                ActualDeductedVnd = charge.ActualDeductedVnd,
                AbsorbedVnd = charge.AbsorbedVnd,
                RemainingBalanceVnd = charge.BalanceAfterVnd,
                GenerationUsageSource = generationUsage?.UsageSource ?? "missing_from_provider_response",
                ReviewUsageSource = reviewUsage?.UsageSource ?? "missing_from_provider_response",
                GenerationCostSource = generationUsage?.CostSource ?? "estimated_catalog",
                ReviewCostSource = reviewUsage?.CostSource ?? "estimated_catalog",
                PersistedFeQuestionIds = persistence.FeQuestionIds,
                PersistedPeQuestionIds = persistence.PeQuestionIds,
                GeneratorPromptKey = generatorPrompt.Key,
                GeneratorPromptVersion = generatorPrompt.Version,
                ReviewerPromptKey = reviewerPrompt.Key,
                ReviewerPromptVersion = reviewerPrompt.Version,
                RubricKey = questionRubricArtifact.ArtifactKey,
                RubricVersion = questionRubricArtifact.Version
            }
        };
    }

    private async Task<GenerationReviewResultDto> RunMixedDifficultyAsync(GenerationReviewRequestDto request, CancellationToken cancellationToken)
    {
        var difficultyProfile = NormalizeDifficultyProfile(request.DifficultyProfile);
        if (difficultyProfile.Count == 0)
        {
            throw new InvalidOperationException("Mixed-difficulty generation requires a non-empty difficultyProfile.");
        }

        var sourceCount = difficultyProfile.Sum(item => item.Count);
        var baseSourceRequest = CloneRequest(request);
        var resolvedSource = await ResolveChunksAsync(baseSourceRequest, cancellationToken);
        if (resolvedSource.Chunks.Count == 0)
        {
            throw new InvalidOperationException("Question generation requires at least one chunk or a valid context pack.");
        }

        var scopeAssessment = AssessMixedDifficultyScope(request, resolvedSource);
        var bucketResults = new List<GenerationReviewResultDto>();
        foreach (var bucket in difficultyProfile)
        {
            if (!CanAttemptMixedDifficultyBucket(bucket.Difficulty, scopeAssessment))
            {
                bucketResults.Add(BuildSkippedMixedBucketResult(request, bucket, resolvedSource, scopeAssessment));
                continue;
            }

            var bucketRequest = CreateBucketRequest(request, bucket, resolvedSource);
            var bucketResult = await RunSingleDifficultyAsync(bucketRequest, cancellationToken);
            bucketResults.Add(bucketResult);
        }

        var aggregatedQuestions = bucketResults
            .SelectMany(result => result.Questions)
            .ToList();
        var difficultyReports = bucketResults
            .Select((result, index) => BuildDifficultyReport(
                difficultyProfile[index].Difficulty,
                difficultyProfile[index].Count,
                result.Questions.Count,
                result.Review,
                result.ShortfallReport,
                result.Persistence))
            .ToList();
        var aggregatedMetrics = AggregateMetrics(bucketResults);
        var aggregatedPersistence = AggregatePersistence(bucketResults);
        var aggregatedReview = AggregateReview(bucketResults);
        var aggregatedShortfall = BuildMixedShortfallReport(request, resolvedSource, difficultyReports, aggregatedReview);

        return new GenerationReviewResultDto
        {
            CourseId = request.CourseId,
            DocumentId = request.DocumentId,
            SourceScope = request.Retrieval?.SourceScope
                ?? resolvedSource.ContextPack?.SourceScope
                ?? (string.IsNullOrWhiteSpace(request.DocumentId) ? "SYSTEM" : "BYOS"),
            Subject = request.Subject,
            QuestionType = request.QuestionType,
            DifficultyMode = "mixed",
            DifficultyProfile = difficultyProfile,
            DifficultyReports = difficultyReports,
            Difficulty = "Mixed",
            Mode = request.Mode,
            AttemptsUsed = bucketResults.Sum(result => result.AttemptsUsed),
            GeneratorModel = bucketResults.Select(result => result.GeneratorModel).FirstOrDefault(model => !string.IsNullOrWhiteSpace(model)) ?? string.Empty,
            ReviewerModel = bucketResults.Select(result => result.ReviewerModel).FirstOrDefault(model => !string.IsNullOrWhiteSpace(model)) ?? string.Empty,
            Questions = aggregatedQuestions,
            Review = aggregatedReview,
            ShortfallReport = aggregatedShortfall,
            Persistence = aggregatedPersistence,
            ContextPack = resolvedSource.ContextPack ?? bucketResults.FirstOrDefault()?.ContextPack,
            RetrievalPlan = resolvedSource.RetrievalPlan ?? bucketResults.FirstOrDefault()?.RetrievalPlan,
            Metrics = aggregatedMetrics
        };
    }

    private async Task<ChunkResolutionResult> ResolveChunksAsync(GenerationReviewRequestDto request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ContextPackId))
        {
            var pack = await _contextPackRepository.GetByIdAsync(request.ContextPackId)
                ?? throw new KeyNotFoundException("Context pack not found.");
            if (!string.Equals(pack.UserId, request.UserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("You do not have permission to use this context pack.");
            }
            return new ChunkResolutionResult(
                pack.Chunks.Select(chunk => new RetrievedChunkDto
                {
                    ChunkId = chunk.ChunkId,
                    DocumentId = pack.RetrievedChunks.FirstOrDefault(item => item.ChunkId == chunk.ChunkId)?.DocumentId ?? pack.DocumentId,
                    SectionTitle = chunk.Title,
                    ContentText = chunk.PackedText,
                    TokenCount = chunk.TokenCount,
                    SelectionRole = chunk.SelectionRole,
                    TopicTags = pack.RetrievedChunks.FirstOrDefault(item => item.ChunkId == chunk.ChunkId)?.TopicTags ?? new List<string>()
                }).ToList(),
                MapContextPack(pack),
                null);
        }

        if (request.Retrieval is not null)
        {
            request.Retrieval.UserId = request.UserId;
            var plan = await _retrievalPlannerService.PlanAsync(request.Retrieval, cancellationToken);
            return new ChunkResolutionResult(plan.SelectedChunks, plan.ContextPack, plan);
        }

        if (request.Chunks is { Count: > 0 })
        {
            var sanitizedChunks = request.Chunks.Select(chunk => new RetrievedChunkDto
            {
                ChunkId = chunk.ChunkId,
                DocumentId = chunk.DocumentId,
                ChapterKey = chunk.ChapterKey,
                SectionTitle = chunk.SectionTitle,
                TopicTags = chunk.TopicTags,
                TokenCount = chunk.TokenCount,
                SelectionRole = chunk.SelectionRole,
                SelectionReasons = chunk.SelectionReasons,
                TopicScore = chunk.TopicScore,
                LexicalScore = chunk.LexicalScore,
                SectionScore = chunk.SectionScore,
                FinalScore = chunk.FinalScore,
                ContentText = AIContentSanitizer.SanitizeChunkContent(chunk.ContentText)
            }).ToList();
            return new ChunkResolutionResult(sanitizedChunks, null, null);
        }

        return new ChunkResolutionResult(new List<RetrievedChunkDto>(), null, null);
    }

    private static QuestionReviewDecisionDto? EvaluateSourcePoorShortCircuit(
        GenerationReviewRequestDto request,
        ChunkResolutionResult chunkResolution,
        QuestionGenerationReviewRuntimePolicy? policy)
    {
        var chunks = chunkResolution.Chunks;
        if (chunks.Count == 0)
        {
            return null;
        }

        var normalizedSubject = NormalizeSubject(request.Subject);
        var requestTopics = (request.Retrieval?.TargetTopics ?? new List<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();
        var matchingRule = (policy?.ShortCircuitRules ?? new List<ShortCircuitRulePolicy>())
            .FirstOrDefault(rule => MatchesReviewRule(rule.Subject, rule.QuestionType, normalizedSubject, request.QuestionType) &&
                                    MatchesTopicPatterns(requestTopics, rule.RequiredTopicsAny, TopicMatchMode.Any));
        if (matchingRule is null)
        {
            return null;
        }

        if (matchingRule.MaxChunkCount > 0 && chunks.Count > matchingRule.MaxChunkCount)
        {
            return null;
        }

        var combinedText = string.Join("\n", chunks.Select(item => item.ContentText ?? string.Empty)).ToLowerInvariant();
        var topicTags = chunks.SelectMany(item => item.TopicTags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var chapterKeys = chunks.Select(item => item.ChapterKey ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sectionTitles = chunks.Select(item => item.SectionTitle ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();

        var hasRequiredTopicSignals = matchingRule.RequiredTopicSignalsAny.Count == 0 ||
                                      topicTags.Any(item => matchingRule.RequiredTopicSignalsAny.Any(signal => item.Contains(signal, StringComparison.OrdinalIgnoreCase)));
        var hasRequiredTopicSignalGroups = matchingRule.RequiredTopicSignalsAllGroups.Count == 0 ||
                                           matchingRule.RequiredTopicSignalsAllGroups.All(group =>
                                               group.Any(signal => topicTags.Any(item => item.Contains(signal, StringComparison.OrdinalIgnoreCase))));
        var minimumThinChunkCount = matchingRule.MinThinChunkCount > 0
            ? Math.Min(chunks.Count, matchingRule.MinThinChunkCount)
            : (chunks.Count <= 2 ? chunks.Count : Math.Max(2, chunks.Count - 1));
        var thinChunkThreshold = matchingRule.ThinChunkTokenThreshold > 0 ? matchingRule.ThinChunkTokenThreshold : 130;
        var hasOnlyThinTheoryChunks = chunks.Count(item => item.TokenCount <= thinChunkThreshold) >= minimumThinChunkCount;
        var hasLimitedChapterSpread = matchingRule.MaxChapterSpread <= 0 || chapterKeys.Count <= matchingRule.MaxChapterSpread;
        var lacksBehaviorRichSignals = string.IsNullOrWhiteSpace(matchingRule.BehaviorRichSignalRegex) ||
                                       !Regex.IsMatch(combinedText, matchingRule.BehaviorRichSignalRegex, RegexOptions.IgnoreCase);
        var hasConfiguredDemoSignal = matchingRule.DemoPhraseGroups.Count == 0 ||
                                      matchingRule.DemoPhraseGroups.Any(group => group.All(term => combinedText.Contains(term, StringComparison.OrdinalIgnoreCase)));
        var theorySectionCount = sectionTitles.Count(title =>
            matchingRule.TheorySectionTitles.Any(expected => string.Equals(title, expected, StringComparison.OrdinalIgnoreCase)) ||
            matchingRule.TheorySectionContains.Any(expected => title.Contains(expected, StringComparison.OrdinalIgnoreCase)));
        var looksLikeTheoryOnlyPolymorphismPack =
            sectionTitles.Count > 0 &&
            theorySectionCount == sectionTitles.Count &&
            !matchingRule.ExcludedTextTerms.Any(term => combinedText.Contains(term, StringComparison.OrdinalIgnoreCase));

        var looksLikeThinPolymorphismDemoSource =
            hasRequiredTopicSignals &&
            hasRequiredTopicSignalGroups &&
            hasOnlyThinTheoryChunks &&
            hasLimitedChapterSpread &&
            lacksBehaviorRichSignals &&
            (hasConfiguredDemoSignal || looksLikeTheoryOnlyPolymorphismPack);

        if (!looksLikeThinPolymorphismDemoSource)
        {
            return null;
        }

        return new QuestionReviewDecisionDto
        {
            ReviewStatus = string.IsNullOrWhiteSpace(matchingRule.ReviewStatus) ? "needs_revision" : matchingRule.ReviewStatus,
            NeedsRevision = true,
            SchemaValid = false,
            ContentGrounded = false,
            Score = matchingRule.Score <= 0m ? 0.25m : matchingRule.Score,
            ReviewerModel = "short_circuit_pre_generation",
            ReviewMode = "ShortCircuit",
            Issues = matchingRule.Issues.Count > 0 ? matchingRule.Issues : new List<string> { "Generation was skipped because the retrieved source context is too weak for production-quality PE generation." },
            Suggestions = matchingRule.Suggestions.Count > 0 ? matchingRule.Suggestions : new List<string> { "Retrieve a richer chapter or support chunk set before generating again." }
        };
    }

    private static QuestionReviewDecisionDto? EvaluateDifficultyAdequacyShortCircuit(
        GenerationReviewRequestDto request,
        ChunkResolutionResult chunkResolution)
    {
        var contextPack = chunkResolution.ContextPack;
        var packedTokenCount = contextPack?.TokenCount
            ?? chunkResolution.Chunks.Sum(chunk => Math.Max(chunk.TokenCount, EstimateChunkTokenCount(chunk.ContentText)));
        var coverageRatio = contextPack?.TopicCoverageRatio
            ?? 1m;
        var uncoveredTopics = contextPack?.UncoveredTopics?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList()
            ?? new List<string>();
        var chunkCount = chunkResolution.Chunks.Count;
        var difficulty = request.Difficulty?.Trim() ?? string.Empty;
        var questionType = request.QuestionType?.Trim() ?? string.Empty;

        if (string.Equals(questionType, "FE", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(difficulty, "Medium", StringComparison.OrdinalIgnoreCase) &&
                request.Count >= 5 &&
                (packedTokenCount < 140 || chunkCount < 2))
            {
                return BuildDifficultyAdequacyShortCircuit(
                    difficulty,
                    packedTokenCount,
                    coverageRatio,
                    uncoveredTopics,
                    "Generation was skipped because the selected FE Medium scope is too thin for the requested count.",
                    "Reduce the requested count or broaden the selected chapter/topic scope before generating FE Medium questions.");
            }

            if (string.Equals(difficulty, "Hard", StringComparison.OrdinalIgnoreCase) &&
                (packedTokenCount < 260 || coverageRatio < 1m || chunkCount < 3))
            {
                return BuildDifficultyAdequacyShortCircuit(
                    difficulty,
                    packedTokenCount,
                    coverageRatio,
                    uncoveredTopics,
                    "Generation was skipped because the selected FE Hard scope is too thin for reliable grounded output.",
                    "Broaden the selected scope before generating FE Hard questions.");
            }
        }

        if (string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(difficulty, "Medium", StringComparison.OrdinalIgnoreCase) &&
                (packedTokenCount < 120 || chunkCount < 2))
            {
                return BuildDifficultyAdequacyShortCircuit(
                    difficulty,
                    packedTokenCount,
                    coverageRatio,
                    uncoveredTopics,
                    "Generation was skipped because the selected PE Medium scope is too thin for a grounded programming task.",
                    "Broaden the selected chapter/topic scope before generating PE Medium questions.");
            }

            if (string.Equals(difficulty, "Hard", StringComparison.OrdinalIgnoreCase) &&
                (packedTokenCount < 220 || coverageRatio < 1m || chunkCount < 3))
            {
                return BuildDifficultyAdequacyShortCircuit(
                    difficulty,
                    packedTokenCount,
                    coverageRatio,
                    uncoveredTopics,
                    "Generation was skipped because the selected PE Hard scope is too thin for a grounded programming task.",
                    "Broaden the selected chapter/topic scope before generating PE Hard questions.");
            }
        }

        return null;
    }

    private static QuestionReviewDecisionDto? EvaluateGenericQueuePeScopeShortCircuit(
        GenerationReviewRequestDto request,
        ChunkResolutionResult chunkResolution)
    {
        if (!string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(NormalizeSubject(request.Subject), AISubjectDomainMapper.DsaJava, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var topics = request.Retrieval?.TargetTopics
            ?.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList() ?? new List<string>();
        if (topics.Count == 0 ||
            !topics.Any(topic => Regex.IsMatch(topic, @"(?i)\bqueues?\b")) ||
            topics.Any(topic => Regex.IsMatch(topic, @"(?i)\b(priority queue|priority queues|deque|double-?ended queue|round robin|scheduler|scheduling|applications?)\b")))
        {
            return null;
        }

        var chunks = chunkResolution.Chunks;
        if (chunks.Count == 0)
        {
            return null;
        }

        var combined = string.Join("\n", chunks.Select(chunk =>
            $"{chunk.SectionTitle} {chunk.ChapterKey} {string.Join(" ", chunk.TopicTags)} {chunk.ContentText}")).ToLowerInvariant();

        var hasSpecializedVariantSignal = Regex.IsMatch(
            combined,
            @"(?i)\b(priority queue|priority queues|deque|double-?ended queue|round robin|scheduler|scheduling|applications? of queues?)\b");
        var hasCoreQueueImplementationSignal = Regex.IsMatch(
            combined,
            @"(?i)\b(array-based queue|circular queue|queue interface|queue adt|fifo|first in first out|front element|rear element|front index|rear index|circular fashion|first and last)\b");
        if (hasCoreQueueImplementationSignal)
        {
            return null;
        }

        var onlyVariantSignals = hasSpecializedVariantSignal &&
                               !Regex.IsMatch(
                                   combined,
                                   @"(?i)\b(array-based queue|circular queue|queue interface|queue implementation|queue adt|fifo|first in first out|front element|rear element|front index|rear index|circular fashion|first and last)\b");
        if (!onlyVariantSignals)
        {
            return null;
        }

        return new QuestionReviewDecisionDto
        {
            ReviewStatus = "needs_revision",
            NeedsRevision = true,
            SchemaValid = false,
            ContentGrounded = false,
            Score = 0.32m,
            ReviewerModel = "short_circuit_pre_generation",
            ReviewMode = "ShortCircuit",
            Issues = new List<string>
            {
                "The selected PE queue scope is too thin or too specialized for a grounded generic queue task.",
                "Retrieved evidence is dominated by specialized variants such as priority queues, deque behavior, or queue applications rather than learner-facing core queue implementation behavior."
            },
            Suggestions = new List<string>
            {
                "Broaden the queue scope to include array-based queue or core enqueue/dequeue implementation material before generating again.",
                "If you want a specialized task, select explicit topics such as priority queues, deque, or round-robin scheduling instead of generic queues."
            }
        };
    }

    private static QuestionReviewDecisionDto? EvaluateJavaInheritancePeScopeShortCircuit(
        GenerationReviewRequestDto request,
        ChunkResolutionResult chunkResolution)
    {
        if (!string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(NormalizeSubject(request.Subject), "JAVA_OOP", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var topics = request.Retrieval?.TargetTopics ?? new List<string>();
        var topicText = string.Join(" ", topics);
        if (!Regex.IsMatch(topicText, @"\binheritance\b|\bmethod overriding\b|\bsuper\b|\bpolymorphism\b", RegexOptions.IgnoreCase))
        {
            return null;
        }

        var chunks = SelectPromptChunksForRequest(request, chunkResolution.Chunks);
        var packedTokenCount = chunkResolution.ContextPack?.TokenCount
            ?? chunks.Sum(chunk => Math.Max(chunk.TokenCount, EstimateChunkTokenCount(chunk.ContentText)));
        var combinedText = string.Join("\n", chunks.Select(chunk => $"{chunk.SectionTitle}\n{chunk.ContentText}"));
        var distinctSectionTitles = chunks
            .Select(chunk => chunk.SectionTitle?.Trim())
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var theoryOnlyTitles = new[]
        {
            "Inheritance",
            "Constructors Are Not Inherited",
            "Hiding a Method"
        };
        var mostlyTheoryOnly =
            distinctSectionTitles.Count > 0 &&
            distinctSectionTitles.All(title =>
                theoryOnlyTitles.Contains(title, StringComparer.OrdinalIgnoreCase) ||
                title!.StartsWith("Inheritance", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(title, @"\b(subclass|superclass|overrid|super|constructor)\b", RegexOptions.IgnoreCase));
        var lectureDemoSignals = new[]
        {
            "displayDiscount",
            "discount",
            "person",
            "student",
            "item",
            "vase",
            "painting",
            "super",
            "constructor",
            "method overriding",
            "constructors are not inherited"
        };
        var lectureDemoSignalCount = lectureDemoSignals.Count(signal =>
            combinedText.Contains(signal, StringComparison.OrdinalIgnoreCase));
        var anchoredToLectureDemoTerms = lectureDemoSignalCount >= 3;
        var lacksBehaviorRichSignals =
            !Regex.IsMatch(combinedText, @"\b(update|modify|transfer|schedule|reserve|process|manage|calculate total|state transition|inventory|balance|cart|scoreboard|history)\b", RegexOptions.IgnoreCase);
        var conceptOnlySignalCount = CountConceptOnlyJavaInheritanceSignals(combinedText);
        var hasOnlyShortPromptPack = chunks.Count <= 4 && packedTokenCount < 650;

        if (!(hasOnlyShortPromptPack &&
              lacksBehaviorRichSignals &&
              (mostlyTheoryOnly || conceptOnlySignalCount >= 3) &&
              (anchoredToLectureDemoTerms || conceptOnlySignalCount >= 4)))
        {
            return null;
        }

        return new QuestionReviewDecisionDto
        {
            ReviewStatus = "needs_revision",
            NeedsRevision = true,
            SchemaValid = true,
            ContentGrounded = false,
            Score = 0.2m,
            ReviewerModel = "java_inheritance_pe_preflight",
            ReviewMode = "ShortCircuit",
            Issues = new List<string>
            {
                "Generation was skipped because the selected JAVA_OOP PE inheritance scope is still theory-heavy and too close to a lecture-demo pack for a reliable grounded coding task.",
                $"The retrieved scope only provides {packedTokenCount} packed tokens and is dominated by theory-style inheritance/constructor/override notes rather than behavior-rich implementation evidence.",
                "This scope is likely to force template PE outputs around superclass/subclass demos, super calls, or trivial arithmetic/string wrappers."
            },
            Suggestions = new List<string>
            {
                "Broaden the selected scope with richer implementation-oriented chunks before generating JAVA_OOP PE from inheritance topics.",
                "Prefer a chapter/topic set that includes explicit object behavior, state changes, or runnable class examples instead of pure theory notes.",
                "Keep the generation grounded: stop here rather than forcing a weak lecture-demo PE item."
            }
        };
    }

    private static int CountConceptOnlyJavaInheritanceSignals(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var patterns = new[]
        {
            @"\bconstructors?\s+are\s+not\s+inherited\b",
            @"\bmethod\s+overriding\b",
            @"\bsuperclass\b",
            @"\bsubclass\b",
            @"\buse\s+of\s+super\b",
            @"\bcall\s+to\s+super\b",
            @"\binheritance\b",
            @"\bdisplaydiscount\b",
            @"\bhiding\s+a\s+method\b"
        };

        return patterns.Count(pattern => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase));
    }

    private static QuestionReviewDecisionDto BuildDifficultyAdequacyShortCircuit(
        string difficulty,
        int packedTokenCount,
        decimal coverageRatio,
        IReadOnlyList<string> uncoveredTopics,
        string issue,
        string suggestion)
    {
        var issues = new List<string> { issue };
        if (packedTokenCount > 0)
        {
            issues.Add($"The retrieval pack only contains {packedTokenCount} packed tokens, which is below the reliability threshold for {difficulty} generation.");
        }

        if (coverageRatio < 1m)
        {
            issues.Add($"Topic coverage ratio is {coverageRatio:0.####}, so the selected scope does not fully cover the requested learning targets.");
        }

        if (uncoveredTopics.Count > 0)
        {
            issues.Add($"Some requested topics are not covered by the retrieved pack: {string.Join(", ", uncoveredTopics)}.");
        }

        return new QuestionReviewDecisionDto
        {
            ReviewStatus = "needs_revision",
            NeedsRevision = true,
            SchemaValid = true,
            ContentGrounded = false,
            Score = 0.2m,
            ReviewerModel = "difficulty_scope_preflight",
            ReviewMode = "ShortCircuit",
            Issues = issues,
            Suggestions = new List<string> { suggestion }
        };
    }

    private static GenerationShortfallReportDto? BuildShortfallReport(
        GenerationReviewRequestDto request,
        ChunkResolutionResult chunkResolution,
        int generatedCount,
        QuestionReviewDecisionDto? review)
    {
        var requestedCount = Math.Max(request.Count, 0);
        var contextPack = chunkResolution.ContextPack;
        var coverageRatio = contextPack?.TopicCoverageRatio ?? 0m;
        var packedTokenCount = contextPack?.TokenCount ?? 0;
        var coveredTopics = contextPack?.CoveredTopics?.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            ?? new List<string>();
        var uncoveredTopics = contextPack?.UncoveredTopics?.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            ?? new List<string>();
        var dominantChapter = contextPack?.DominantChapterKey;
        var hasCountShortfall = generatedCount < requestedCount;
        var needsRevision = review?.NeedsRevision == true;
        var unhealthyCoverage = coverageRatio > 0m && coverageRatio < 0.67m;
        var thinContext = packedTokenCount > 0 && packedTokenCount < 180;
        var uncoveredScope = uncoveredTopics.Count > 0;

        if (!hasCountShortfall && !needsRevision && !unhealthyCoverage && !thinContext && !uncoveredScope)
        {
            return null;
        }

        string stopReason;
        if (generatedCount == 0)
        {
            stopReason = "no_grounded_questions_generated";
        }
        else if (hasCountShortfall && needsRevision)
        {
            stopReason = "partial_acceptance_after_review";
        }
        else if (hasCountShortfall && uncoveredScope)
        {
            stopReason = "requested_count_exceeds_grounded_capacity";
        }
        else if (hasCountShortfall && thinContext)
        {
            stopReason = "insufficient_grounded_chunks";
        }
        else if (needsRevision)
        {
            stopReason = "review_rejected_low_groundedness";
        }
        else
        {
            stopReason = "accepted_with_scope_warning";
        }

        var summary = hasCountShortfall
            ? $"Requested {requestedCount} question(s), but only {generatedCount} grounded question(s) could be produced from the selected scope."
            : "The generation completed successfully, but the selected scope is still fairly narrow. Showing a quality warning so the user understands the context limits.";

        var notes = new List<string>();
        var suggestedActions = new List<string>();

        if (hasCountShortfall)
        {
            notes.Add($"Accepted {generatedCount} out of the requested {requestedCount} question(s) after review and refill handling.");
        }

        if (coverageRatio > 0m)
        {
            notes.Add($"Topic coverage ratio is {coverageRatio:0.####}, which indicates the selected chapter/topic scope does not fully cover the requested learning targets.");
        }

        if (packedTokenCount > 0)
        {
            notes.Add($"The context pack only carried {packedTokenCount} packed tokens, which limits how many distinct grounded tasks can be produced safely.");
        }

        if (uncoveredTopics.Count > 0)
        {
            notes.Add($"Some requested topics were not covered by the retrieved pack: {string.Join(", ", uncoveredTopics)}.");
        }

        if (!string.IsNullOrWhiteSpace(dominantChapter))
        {
            notes.Add($"Most retrieved evidence came from {dominantChapter}, so cross-topic coverage may be narrower than the requested generation scope.");
        }

        if (review?.NeedsRevision == true && review.Issues.Count > 0)
        {
            notes.Add($"Reviewer rejected part of the output because: {review.Issues[0]}");
        }

        suggestedActions.Add("Expand the selected scope with a richer chapter set or more directly relevant topics before requesting the same count again.");

        if (uncoveredTopics.Count > 0)
        {
            suggestedActions.Add($"Include source material that explicitly covers: {string.Join(", ", uncoveredTopics)}.");
        }

        if (string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.Difficulty, "Medium", StringComparison.OrdinalIgnoreCase) &&
            thinContext)
        {
            suggestedActions.Add("For PE Medium generation, either broaden the scope or reduce the requested count/difficulty when the available source evidence is thin.");
        }

        if (generatedCount == 0)
        {
            suggestedActions.Add("Keep the generation grounded: stop and adjust scope instead of forcing unsupported questions.");
        }
        else if (stopReason == "partial_acceptance_after_review")
        {
            suggestedActions.Add("Review the rejected items and regenerate only the missing slots with stronger reasoning demand and less overlap.");
        }

        return new GenerationShortfallReportDto
        {
            RequestedCount = requestedCount,
            GeneratedCount = generatedCount,
            StopReason = stopReason,
            Summary = summary,
            CoverageRatio = coverageRatio,
            PackedTokenCount = packedTokenCount,
            DominantChapter = dominantChapter,
            CoveredTopics = coveredTopics,
            UncoveredTopics = uncoveredTopics,
            Notes = notes,
            SuggestedActions = suggestedActions
        };
    }

    private static bool IsMixedDifficultyRequest(GenerationReviewRequestDto request)
        => request.DifficultyProfile.Count > 1;

    private static List<DifficultyDistributionItemDto> NormalizeDifficultyProfile(IEnumerable<DifficultyDistributionItemDto>? profile)
        => (profile ?? Array.Empty<DifficultyDistributionItemDto>())
            .Where(item => item is not null && item.Count > 0 && !string.IsNullOrWhiteSpace(item.Difficulty))
            .GroupBy(item => item.Difficulty.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new DifficultyDistributionItemDto
            {
                Difficulty = group.Key,
                Count = group.Sum(item => item.Count)
            })
            .ToList();

    private static void ValidateMixedCountLimits(
        string questionType,
        IReadOnlyList<DifficultyDistributionItemDto> difficultyProfile,
        int totalCount)
    {
        if (string.Equals(questionType, "FE", StringComparison.OrdinalIgnoreCase))
        {
            if (totalCount < 1 || totalCount > 50)
            {
                throw new InvalidOperationException("FE mixed generation requires total count from 10 to 50.");
            }

            return;
        }

        if (string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            if (totalCount < 1 || totalCount > 5)
            {
                throw new InvalidOperationException("PE mixed generation requires total count from 1 to 5.");
            }
        }
    }

    private static List<DifficultyDistributionItemDto> BuildSingleDifficultyProfile(GenerationReviewRequestDto request)
        => new()
        {
            new DifficultyDistributionItemDto
            {
                Difficulty = request.Difficulty,
                Count = request.Count
            }
        };

    private static bool ShouldUseBatchedExecution(GenerationReviewRequestDto request)
        => request.Count > 1;

    private static int ResolveBatchSize(GenerationReviewRequestDto request)
    {
        if (string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return IsPeMediumHardRequest(request) ? 1 : 2;
        }

        return 10;
    }

    private static int ResolveRefillBudget(GenerationReviewRequestDto request)
    {
        var batchSize = ResolveBatchSize(request);
        var estimatedBatchCount = Math.Max(1, (int)Math.Ceiling((double)Math.Max(1, request.Count) / batchSize));
        var baseBudget = string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase) ? 3 : 4;
        var hardCap = string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase) ? 5 : 6;
        return Math.Clamp(Math.Max(baseBudget, estimatedBatchCount), 1, hardCap);
    }

    private static List<GenerationBatchPlan> BuildBatchPlans(GenerationReviewRequestDto request, string difficulty, int requestedCount)
    {
        var batchSize = ResolveBatchSize(request);
        var remaining = Math.Max(0, requestedCount);
        var plans = new List<GenerationBatchPlan>();
        while (remaining > 0)
        {
            var count = Math.Min(batchSize, remaining);
            plans.Add(new GenerationBatchPlan(difficulty, count, false));
            remaining -= count;
        }

        return plans;
    }

    private static MixedDifficultyScopeAssessment AssessMixedDifficultyScope(
        GenerationReviewRequestDto request,
        ChunkResolutionResult resolvedSource)
    {
        var contextPack = resolvedSource.ContextPack;
        var requestedTopics = request.Retrieval?.TargetTopics
            ?? contextPack?.TargetTopics
            ?? new List<string>();
        var coveredTopics = (contextPack?.CoveredTopics ?? new List<string>())
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var uncoveredTopics = (contextPack?.UncoveredTopics ?? new List<string>())
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (coveredTopics.Count == 0 && requestedTopics.Count > 0)
        {
            coveredTopics = requestedTopics
                .Where(topic => resolvedSource.Chunks.Any(chunk => chunk.TopicTags.Contains(topic, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            uncoveredTopics = requestedTopics
                .Where(topic => !coveredTopics.Contains(topic, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var coverageRatio = contextPack?.TopicCoverageRatio
            ?? (requestedTopics.Count == 0
                ? 1m
                : Math.Round((decimal)coveredTopics.Count / requestedTopics.Count, 4));
        var packedTokenCount = contextPack?.TokenCount
            ?? resolvedSource.Chunks.Sum(chunk => Math.Max(chunk.TokenCount, EstimateChunkTokenCount(chunk.ContentText)));
        var chunkCount = resolvedSource.Chunks.Count;
        var distinctChapterCount = resolvedSource.Chunks
            .Select(chunk => chunk.ChapterKey ?? string.Empty)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var requestedChapterCount = (contextPack?.RequestedChapterKeys ?? request.Retrieval?.ChapterKeys ?? new List<string>())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var reasons = new List<string>();
        var suggestedActions = new List<string>();
        var maxDifficulty = "Hard";

        if (packedTokenCount < MixedHardMinimumPackedTokens ||
            coverageRatio < MixedHardMinimumCoverageRatio ||
            chunkCount < 3 ||
            (requestedChapterCount > 1 && distinctChapterCount < Math.Min(2, requestedChapterCount)))
        {
            maxDifficulty = "Medium";
        }

        if (packedTokenCount < MixedMediumMinimumPackedTokens ||
            coverageRatio < MixedMediumMinimumCoverageRatio ||
            chunkCount < 2)
        {
            maxDifficulty = "Easy";
        }

        if (packedTokenCount < MixedEasyMinimumPackedTokens || chunkCount == 0)
        {
            maxDifficulty = "None";
        }

        if (coverageRatio < 1m)
        {
            reasons.Add($"Topic coverage ratio is {coverageRatio:0.####}, so the selected scope does not fully cover the requested targets.");
        }

        if (packedTokenCount < MixedMediumMinimumPackedTokens)
        {
            reasons.Add($"The retrieval pack only contains {packedTokenCount} packed tokens, which is too thin for reliable mixed Medium/Hard generation.");
        }
        else if (packedTokenCount < MixedHardMinimumPackedTokens)
        {
            reasons.Add($"The retrieval pack contains {packedTokenCount} packed tokens, which is still thin for reliable Hard generation.");
        }

        if (requestedChapterCount > 1 && distinctChapterCount < requestedChapterCount)
        {
            reasons.Add("Retrieved evidence collapsed into fewer chapters than the user selected, so bucket diversity would be unstable.");
        }

        if (maxDifficulty is "Easy" or "None")
        {
            suggestedActions.Add("Broaden the selected chapter/topic scope before requesting Medium or Hard buckets in the same run.");
        }
        else if (maxDifficulty == "Medium")
        {
            suggestedActions.Add("Keep Easy/Medium mixed requests on this scope, but avoid Hard until retrieval covers more chapters or richer chunk evidence.");
        }

        return new MixedDifficultyScopeAssessment(
            maxDifficulty,
            packedTokenCount,
            coverageRatio,
            distinctChapterCount,
            coveredTopics,
            uncoveredTopics,
            reasons,
            suggestedActions);
    }

    private static bool CanAttemptMixedDifficultyBucket(string difficulty, MixedDifficultyScopeAssessment assessment)
        => GetDifficultyRank(difficulty) <= GetDifficultyRank(assessment.MaxAllowedDifficulty);

    private static int GetDifficultyRank(string difficulty)
        => difficulty.Trim().ToLowerInvariant() switch
        {
            "none" => 0,
            "easy" => 1,
            "medium" => 2,
            "hard" => 3,
            _ => 4
        };

    private static GenerationReviewResultDto BuildSkippedMixedBucketResult(
        GenerationReviewRequestDto request,
        DifficultyDistributionItemDto bucket,
        ChunkResolutionResult resolvedSource,
        MixedDifficultyScopeAssessment assessment)
    {
        var review = new QuestionReviewDecisionDto
        {
            ReviewStatus = "needs_revision",
            NeedsRevision = true,
            SchemaValid = true,
            ContentGrounded = false,
            Score = 0.2m,
            ReviewerModel = "mixed_scope_preflight",
            ReviewMode = "MixedDifficultyPreflight",
            Issues = new List<string>
            {
                $"Skipping {bucket.Difficulty} bucket because the retrieved scope is only strong enough for {assessment.MaxAllowedDifficulty} difficulty in this mixed request."
            },
            Suggestions = assessment.SuggestedActions.Count > 0
                ? assessment.SuggestedActions.ToList()
                : new List<string> { "Broaden the selected scope before retrying this difficulty bucket." }
        };

        var shortfall = new GenerationShortfallReportDto
        {
            RequestedCount = bucket.Count,
            GeneratedCount = 0,
            StopReason = "preflight_scope_too_thin_for_requested_difficulty",
            Summary = $"Skipped {bucket.Difficulty} generation because the selected scope is too thin to support that bucket reliably.",
            CoverageRatio = assessment.CoverageRatio,
            PackedTokenCount = assessment.PackedTokenCount,
            DominantChapter = resolvedSource.ContextPack?.DominantChapterKey,
            CoveredTopics = assessment.CoveredTopics.ToList(),
            UncoveredTopics = assessment.UncoveredTopics.ToList(),
            Notes = assessment.Reasons.Count > 0
                ? assessment.Reasons.ToList()
                : new List<string> { "Mixed-difficulty preflight rejected this bucket before generation to avoid fabricated or weak output." },
            SuggestedActions = review.Suggestions.ToList()
        };

        return new GenerationReviewResultDto
        {
            CourseId = request.CourseId,
            DocumentId = request.DocumentId,
            SourceScope = request.Retrieval?.SourceScope
                ?? resolvedSource.ContextPack?.SourceScope
                ?? (string.IsNullOrWhiteSpace(request.DocumentId) ? "SYSTEM" : "BYOS"),
            Subject = request.Subject,
            QuestionType = request.QuestionType,
            DifficultyMode = "single",
            DifficultyProfile = new List<DifficultyDistributionItemDto>
            {
                new() { Difficulty = bucket.Difficulty, Count = bucket.Count }
            },
            Difficulty = bucket.Difficulty,
            Mode = request.Mode,
            AttemptsUsed = 0,
            GeneratorModel = string.Empty,
            ReviewerModel = review.ReviewerModel,
            Questions = new List<GeneratedQuestionCandidateDto>(),
            Review = review,
            ShortfallReport = shortfall,
            Persistence = new QuestionPersistenceResultDto(),
            ContextPack = resolvedSource.ContextPack,
            RetrievalPlan = resolvedSource.RetrievalPlan,
            Metrics = new GenerationMetricsDto
            {
                ContextPackId = resolvedSource.ContextPack?.PackId ?? string.Empty,
                ChunkCount = resolvedSource.Chunks.Count
            }
        };
    }

    private static int EstimateChunkTokenCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, NormalizeWhitespace(text).Length / 4);
    }

    private static DifficultyGenerationReportDto BuildDifficultyReport(
        string difficulty,
        int requestedCount,
        int generatedCount,
        QuestionReviewDecisionDto review,
        GenerationShortfallReportDto? shortfallReport,
        QuestionPersistenceResultDto persistence)
        => new()
        {
            Difficulty = difficulty,
            RequestedCount = requestedCount,
            GeneratedCount = generatedCount,
            ReviewStatus = review.ReviewStatus,
            NeedsRevision = review.NeedsRevision,
            PersistedCount = persistence.PersistedCount,
            ShortfallReport = shortfallReport
        };

    private static GenerationReviewRequestDto CloneRequest(GenerationReviewRequestDto request)
        => new()
        {
            UserId = request.UserId,
            CourseId = request.CourseId,
            DocumentId = request.DocumentId,
            IsPublic = request.IsPublic,
            Subject = request.Subject,
            DifficultyMode = request.DifficultyMode,
            DifficultyProfile = request.DifficultyProfile
                .Select(item => new DifficultyDistributionItemDto
                {
                    Difficulty = item.Difficulty,
                    Count = item.Count
                })
                .ToList(),
            Difficulty = request.Difficulty,
            QuestionType = request.QuestionType,
            Count = request.Count,
            Mode = request.Mode,
            MaxAttempts = request.MaxAttempts,
            PersistQuestions = request.PersistQuestions,
            SuppressPersistenceSideEffects = request.SuppressPersistenceSideEffects,
            ContextPackId = request.ContextPackId,
            Retrieval = request.Retrieval is null
                ? null
                : new RetrievalPlanRequestDto
                {
                    UserId = request.Retrieval.UserId,
                    CourseId = request.Retrieval.CourseId,
                    DocumentId = request.Retrieval.DocumentId,
                    ChapterKey = request.Retrieval.ChapterKey,
                    ChapterKeys = request.Retrieval.ChapterKeys.ToList(),
                    Subject = request.Retrieval.Subject,
                    QuestionType = request.Retrieval.QuestionType,
                    Difficulty = request.Retrieval.Difficulty,
                    RequestedQuestionCount = request.Retrieval.RequestedQuestionCount,
                    GenerationMode = request.Retrieval.GenerationMode,
                    TargetTopics = request.Retrieval.TargetTopics.ToList(),
                    SourceScope = request.Retrieval.SourceScope,
                    Language = request.Retrieval.Language,
                    MaxPackedTokens = request.Retrieval.MaxPackedTokens,
                    RetrievalQuery = request.Retrieval.RetrievalQuery,
                    AllowExtendedScope = request.Retrieval.AllowExtendedScope
                },
            Chunks = request.Chunks?.Select(CloneChunk).ToList(),
            GeneratorOverride = request.GeneratorOverride,
            ReviewerOverride = request.ReviewerOverride
        };

    private static RetrievedChunkDto CloneChunk(RetrievedChunkDto chunk)
        => new()
        {
            ChunkId = chunk.ChunkId,
            ChunkIndex = chunk.ChunkIndex,
            SectionTitle = chunk.SectionTitle,
            ContentText = chunk.ContentText,
            ChapterKey = chunk.ChapterKey,
            SourcePageFrom = chunk.SourcePageFrom,
            SourcePageTo = chunk.SourcePageTo,
            TopicTags = chunk.TopicTags.ToList(),
            TokenCount = chunk.TokenCount,
            TopicScore = chunk.TopicScore,
            LexicalScore = chunk.LexicalScore,
            SectionScore = chunk.SectionScore,
            FinalScore = chunk.FinalScore,
            SelectionRole = chunk.SelectionRole,
            SelectionReasons = chunk.SelectionReasons.ToList()
        };

    private static GeneratedQuestionCandidateDto CloneQuestionCandidate(GeneratedQuestionCandidateDto question)
        => new()
        {
            Type = question.Type,
            TopicTags = question.TopicTags.ToList(),
            Difficulty = question.Difficulty,
            Title = question.Title,
            Description = question.Description,
            SourceChunkIds = question.SourceChunkIds.ToList(),
            SkeletonCode = question.SkeletonCode?.Select(file => new CodeFile
            {
                Filename = file.Filename,
                Content = file.Content,
                IsReadOnly = file.IsReadOnly
            }).ToList(),
            SolutionCode = question.SolutionCode?.Select(file => new CodeFile
            {
                Filename = file.Filename,
                Content = file.Content,
                IsReadOnly = file.IsReadOnly
            }).ToList(),
            TestCases = question.TestCases?.Select(test => new TestCase
            {
                Input = test.Input,
                ExpectedOutput = test.ExpectedOutput,
                IsHidden = test.IsHidden
            }).ToList(),
            Options = question.Options?.ToList(),
            CorrectAnswer = question.CorrectAnswer?.ToList(),
            Explanation = question.Explanation,
            PersistedQuestionId = question.PersistedQuestionId,
            PersistedStatus = question.PersistedStatus,
            PersistedIsPublic = question.PersistedIsPublic
        };

    private GenerationReviewRequestDto CreateBucketRequest(
        GenerationReviewRequestDto sourceRequest,
        DifficultyDistributionItemDto bucket,
        ChunkResolutionResult resolvedSource)
    {
        var bucketRequest = CloneRequest(sourceRequest);
        bucketRequest.DifficultyMode = "single";
        bucketRequest.DifficultyProfile = new List<DifficultyDistributionItemDto>();
        bucketRequest.Difficulty = bucket.Difficulty;
        bucketRequest.Count = bucket.Count;

        if (!string.IsNullOrWhiteSpace(resolvedSource.ContextPack?.PackId))
        {
            bucketRequest.ContextPackId = resolvedSource.ContextPack.PackId;
            bucketRequest.Retrieval = null;
            bucketRequest.Chunks = null;
        }
        else
        {
            bucketRequest.ContextPackId = null;
            bucketRequest.Retrieval = null;
            bucketRequest.Chunks = resolvedSource.Chunks.Select(CloneChunk).ToList();
        }

        if (bucketRequest.Retrieval is not null)
        {
            bucketRequest.Retrieval.Difficulty = bucket.Difficulty;
            bucketRequest.Retrieval.RequestedQuestionCount = bucket.Count;
        }

        return bucketRequest;
    }

    private GenerationReviewRequestDto CreatePlannedBatchRequest(
        GenerationReviewRequestDto sourceRequest,
        GenerationBatchPlan batchPlan,
        ChunkResolutionResult resolvedSource)
    {
        var batchRequest = CloneRequest(sourceRequest);
        batchRequest.DifficultyMode = "single";
        batchRequest.DifficultyProfile = new List<DifficultyDistributionItemDto>();
        batchRequest.Difficulty = batchPlan.Difficulty;
        batchRequest.Count = batchPlan.Count;
        batchRequest.PersistQuestions = false;
        batchRequest.SuppressPersistenceSideEffects = true;
        batchRequest.ContextPackId = null;
        batchRequest.Retrieval = null;
        batchRequest.Chunks = resolvedSource.Chunks.Select(CloneChunk).ToList();
        return batchRequest;
    }

    private async Task AccumulateBatchOutcomeAsync(
        GenerationReviewResultDto batchResult,
        List<GeneratedQuestionCandidateDto> acceptedQuestions,
        RequestDuplicateTracker duplicateTracker,
        string questionType,
        CancellationToken cancellationToken)
    {
        var lexicalAccepted = new List<GeneratedQuestionCandidateDto>();
        for (var index = 0; index < batchResult.Questions.Count; index++)
        {
            var question = batchResult.Questions[index];
            var fingerprint = QuestionFingerprinting.Build(question);
            var normalizedTitle = QuestionFingerprinting.NormalizeLooseText(question.Title);
            var rawTitle = question.Title?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                if (duplicateTracker.AcceptedFingerprints.Contains(fingerprint) || duplicateTracker.RejectedFingerprints.Contains(fingerprint))
                {
                    continue;
                }

                duplicateTracker.AcceptedFingerprints.Add(fingerprint);
            }

            if (!string.IsNullOrWhiteSpace(normalizedTitle))
            {
                if (duplicateTracker.AcceptedTitles.Contains(normalizedTitle) || (!string.IsNullOrWhiteSpace(rawTitle) && duplicateTracker.AcceptedTitles.Contains(rawTitle)))
                {
                    if (!string.IsNullOrWhiteSpace(fingerprint))
                    {
                        duplicateTracker.RejectedFingerprints.Add(fingerprint);
                    }

                    continue;
                }

                duplicateTracker.AcceptedTitles.Add(normalizedTitle);
                if (!string.IsNullOrWhiteSpace(rawTitle))
                {
                    duplicateTracker.AcceptedTitles.Add(rawTitle);
                }
            }

            lexicalAccepted.Add(question);
        }

        var filteredAccepted = await FilterSemanticNearDuplicatesAsync(
            lexicalAccepted,
            duplicateTracker,
            questionType,
            cancellationToken);
        acceptedQuestions.AddRange(filteredAccepted);
    }

    private static bool ShouldStopRefillAfterNoProgress(GenerationReviewResultDto refillResult)
    {
        var stopReason = refillResult.ShortfallReport?.StopReason ?? string.Empty;
        if (string.IsNullOrWhiteSpace(stopReason))
        {
            return false;
        }

        return stopReason.Contains("scope", StringComparison.OrdinalIgnoreCase) ||
               stopReason.Contains("thin", StringComparison.OrdinalIgnoreCase) ||
               stopReason.Contains("no_grounded", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedSemanticDuplicateEntriesAsync(
        IReadOnlyList<GeneratedQuestionCandidateDto>? questions,
        List<SemanticQuestionEntry> targetEntries,
        CancellationToken cancellationToken)
    {
        if (questions is null || questions.Count == 0)
        {
            return;
        }

        var entries = questions
            .Select(CreateSemanticQuestionEntry)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Text))
            .ToList();
        if (entries.Count == 0)
        {
            return;
        }

        await EnsureSemanticEmbeddingsAsync(entries, cancellationToken);
        targetEntries.AddRange(entries);
    }

    private async Task SeedHistoricalScopeDuplicatesAsync(
        GenerationReviewRequestDto request,
        RequestDuplicateTracker duplicateTracker,
        CancellationToken cancellationToken)
    {
        var historical = await LoadHistoricalScopeCandidatesAsync(request, cancellationToken);
        if (historical.Count == 0)
        {
            return;
        }

        SeedAcceptedQuestionKeys(
            historical,
            duplicateTracker.AcceptedFingerprints,
            duplicateTracker.AcceptedTitles,
            duplicateTracker.AcceptedStructuralSignatures);
        await SeedSemanticDuplicateEntriesAsync(historical, duplicateTracker.AcceptedSemanticEntries, cancellationToken);
    }

    private async Task<List<GeneratedQuestionCandidateDto>> LoadHistoricalScopeCandidatesAsync(
        GenerationReviewRequestDto request,
        CancellationToken cancellationToken)
    {
        const int candidatePoolLimit = 16;

        var sourceScope = ResolveQuestionSourceScope(request);
        var normalizedTopics = (request.Retrieval?.TargetTopics ?? new List<string>())
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Select(topic => topic.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.Equals(request.QuestionType, "FE", StringComparison.OrdinalIgnoreCase))
        {
            List<FEQuestion> items;
            if (string.Equals(sourceScope, "BYOS", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(request.UserId))
            {
                var owned = await _feQuestionRepository.ListOwnedByStudentAsync(
                    request.UserId,
                    request.CourseId,
                    request.Difficulty,
                    null,
                    normalizedTopics.Count > 0 ? normalizedTopics : null,
                    null,
                    1,
                    candidatePoolLimit);
                items = owned.Items;
            }
            else
            {
                var listed = await _feQuestionRepository.ListAsync("Active", request.CourseId, request.Difficulty, 1, candidatePoolLimit);
                items = listed.Items;
            }

            return items
                .Where(question => MatchesHistoricalScope(question.SourceScope, sourceScope, question.OwnerUserId, request.UserId, question.CourseId, request.CourseId))
                .Where(question => MatchesHistoricalTopics(question.TopicTags, normalizedTopics))
                .Select(ConvertToGeneratedCandidate)
                .ToList();
        }
        else
        {
            List<PEQuestion> items;
            if (string.Equals(sourceScope, "BYOS", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(request.UserId))
            {
                var owned = await _peQuestionRepository.ListOwnedByStudentAsync(
                    request.UserId,
                    request.CourseId,
                    request.Difficulty,
                    null,
                    normalizedTopics.Count > 0 ? normalizedTopics : null,
                    null,
                    1,
                    candidatePoolLimit,
                    cancellationToken);
                items = owned.Items;
            }
            else
            {
                var listed = await _peQuestionRepository.ListAsync("Active", request.CourseId, request.Difficulty, 1, candidatePoolLimit, cancellationToken);
                items = listed.Items;
            }

            return items
                .Where(question => MatchesHistoricalScope(question.SourceScope, sourceScope, question.OwnerUserId, request.UserId, question.CourseId, request.CourseId))
                .Where(question => MatchesHistoricalTopics(question.TopicTags, normalizedTopics))
                .Select(ConvertToGeneratedCandidate)
                .ToList();
        }
    }

    private async Task<List<GeneratedQuestionCandidateDto>> FilterSemanticNearDuplicatesAsync(
        IReadOnlyList<GeneratedQuestionCandidateDto> candidates,
        RequestDuplicateTracker duplicateTracker,
        string questionType,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return new List<GeneratedQuestionCandidateDto>();
        }

        var entries = candidates
            .Select(CreateSemanticQuestionEntry)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Text))
            .ToList();
        if (entries.Count == 0)
        {
            return candidates.Select(CloneQuestionCandidate).ToList();
        }

        await EnsureSemanticEmbeddingsAsync(entries, cancellationToken);
        var referenceEntries = duplicateTracker.AcceptedSemanticEntries
            .Concat(duplicateTracker.RejectedSemanticEntries)
            .Where(entry => string.Equals(entry.QuestionType, questionType, StringComparison.OrdinalIgnoreCase))
            .TakeLast(MaxSemanticComparisonEntries)
            .ToList();
        await EnsureSemanticEmbeddingsAsync(referenceEntries, cancellationToken);

        var threshold = string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase)
            ? PeSemanticDuplicateThreshold
            : FeSemanticDuplicateThreshold;
        var structuralThreshold = string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase)
            ? PeStructuralDuplicateThreshold
            : FeStructuralDuplicateThreshold;

        var accepted = new List<GeneratedQuestionCandidateDto>();
        foreach (var entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.StructuralSignature))
            {
                if (duplicateTracker.AcceptedStructuralSignatures.Contains(entry.StructuralSignature) ||
                    duplicateTracker.RejectedStructuralSignatures.Contains(entry.StructuralSignature))
                {
                    if (!string.IsNullOrWhiteSpace(entry.Fingerprint))
                    {
                        duplicateTracker.RejectedFingerprints.Add(entry.Fingerprint);
                    }

                    duplicateTracker.RejectedStructuralSignatures.Add(entry.StructuralSignature);
                    duplicateTracker.RejectedSemanticEntries.Add(entry);
                    continue;
                }
            }

            var structuralDuplicate = referenceEntries.Any(reference =>
                HasStructuralNearDuplicate(entry, reference, structuralThreshold));
            if (structuralDuplicate)
            {
                if (!string.IsNullOrWhiteSpace(entry.Fingerprint))
                {
                    duplicateTracker.RejectedFingerprints.Add(entry.Fingerprint);
                }

                if (!string.IsNullOrWhiteSpace(entry.StructuralSignature))
                {
                    duplicateTracker.RejectedStructuralSignatures.Add(entry.StructuralSignature);
                }

                duplicateTracker.RejectedSemanticEntries.Add(entry);
                continue;
            }

            if (entry.Embedding is null || entry.Embedding.Count == 0)
            {
                accepted.Add(CloneQuestionCandidate(entry.Question));
                if (!string.IsNullOrWhiteSpace(entry.StructuralSignature))
                {
                    duplicateTracker.AcceptedStructuralSignatures.Add(entry.StructuralSignature);
                }

                duplicateTracker.AcceptedSemanticEntries.Add(entry);
                continue;
            }

            var isDuplicate = referenceEntries.Any(reference =>
                reference.Embedding is not null &&
                reference.Embedding.Count > 0 &&
                ComputeCosineSimilarity(entry.Embedding, reference.Embedding) >= threshold);

            if (isDuplicate)
            {
                if (!string.IsNullOrWhiteSpace(entry.Fingerprint))
                {
                    duplicateTracker.RejectedFingerprints.Add(entry.Fingerprint);
                }

                duplicateTracker.RejectedSemanticEntries.Add(entry);
                continue;
            }

            accepted.Add(CloneQuestionCandidate(entry.Question));
            if (!string.IsNullOrWhiteSpace(entry.StructuralSignature))
            {
                duplicateTracker.AcceptedStructuralSignatures.Add(entry.StructuralSignature);
            }

            duplicateTracker.AcceptedSemanticEntries.Add(entry);
            referenceEntries.Add(entry);
        }

        return accepted;
    }

    private async Task EnsureSemanticEmbeddingsAsync(
        IReadOnlyList<SemanticQuestionEntry> entries,
        CancellationToken cancellationToken)
    {
        var pending = entries
            .Where(entry => (entry.Embedding is null || entry.Embedding.Count == 0) && !string.IsNullOrWhiteSpace(entry.Text))
            .ToList();
        if (pending.Count == 0)
        {
            return;
        }

        try
        {
            var resolved = _routingService.ResolveEmbedding(AIFeatureNames.Embedding);
            var response = await _aiExecutionService.ExecuteEmbeddingAsync(
                resolved,
                new AIEmbeddingRequest
                {
                    FeatureName = AIFeatureNames.Embedding,
                    Inputs = pending.Select(entry => entry.Text).ToList(),
                    Provider = resolved.Provider,
                    Model = resolved.Model
                },
                cancellationToken);

            if (response.Embeddings.Count != pending.Count)
            {
                return;
            }

            for (var index = 0; index < pending.Count; index++)
            {
                pending[index].Embedding = response.Embeddings[index];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic duplicate embedding check failed. Falling back to lexical duplicate handling only.");
        }
    }

    private static SemanticQuestionEntry CreateSemanticQuestionEntry(GeneratedQuestionCandidateDto question)
    {
        var fingerprint = QuestionFingerprinting.Build(question);
        var text = BuildSemanticDuplicateText(question);
        var structuralSignature = BuildStructuralDuplicateSignature(question);
        var keywordSet = ExtractDuplicateKeywords(question);
        return new SemanticQuestionEntry(question, question.Type, fingerprint, text, structuralSignature, keywordSet, null);
    }

    private static string BuildSemanticDuplicateText(GeneratedQuestionCandidateDto question)
    {
        var parts = new List<string>
        {
            question.Type,
            string.Join(", ", question.TopicTags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase)),
            question.Title,
            question.Description
        };

        if (string.Equals(question.Type, "FE", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(string.Join(" | ", question.Options ?? new List<string>()));
        }
        else
        {
            var tests = question.TestCases?
                .Select(test => $"{QuestionFingerprinting.NormalizeLooseText(test.Input)}=>{QuestionFingerprinting.NormalizeLooseText(test.ExpectedOutput)}")
                .ToList() ?? new List<string>();
            parts.Add(string.Join(" | ", tests));
        }

        return QuestionFingerprinting.TrimForFingerprint(
            QuestionFingerprinting.NormalizeLooseText(string.Join("\n", parts)));
    }

    private static bool HasStructuralNearDuplicate(
        SemanticQuestionEntry candidate,
        SemanticQuestionEntry reference,
        double overlapThreshold)
    {
        if (!string.Equals(candidate.QuestionType, reference.QuestionType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(candidate.StructuralSignature) &&
            string.Equals(candidate.StructuralSignature, reference.StructuralSignature, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (candidate.KeywordSet.Count == 0 || reference.KeywordSet.Count == 0)
        {
            return false;
        }

        var overlap = ComputeKeywordOverlap(candidate.KeywordSet, reference.KeywordSet);
        if (overlap < overlapThreshold)
        {
            return false;
        }

        return HasTopicOverlap(candidate.Question.TopicTags, reference.Question.TopicTags) ||
               overlap >= Math.Min(0.9d, overlapThreshold + 0.15d);
    }

    private static string BuildStructuralDuplicateSignature(GeneratedQuestionCandidateDto question)
    {
        var keywordSignature = string.Join(
            "|",
            ExtractDuplicateKeywords(question)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .Take(6));
        var topicSignature = string.Join(
            "|",
            question.TopicTags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .Take(3));

        if (string.Equals(question.Type, "PE", StringComparison.OrdinalIgnoreCase))
        {
            var solutionText = string.Join("\n", question.SolutionCode?.Select(file => file.Content) ?? Array.Empty<string>());
            var classCount = Regex.Matches(solutionText, @"\bclass\s+\w+\b", RegexOptions.IgnoreCase).Count;
            var methodCount = Regex.Matches(solutionText, @"\b(public|private|protected)\s+\w[\w<>\[\]]*\s+\w+\s*\(", RegexOptions.IgnoreCase).Count;
            var testCount = question.TestCases?.Count ?? 0;
            return $"{question.Type}|{topicSignature}|{keywordSignature}|classes={classCount}|methods={methodCount}|tests={testCount}";
        }

        var optionCount = question.Options?.Count ?? 0;
        var answerCount = question.CorrectAnswer?.Count ?? 0;
        return $"{question.Type}|{topicSignature}|{keywordSignature}|options={optionCount}|answers={answerCount}";
    }

    private static HashSet<string> ExtractDuplicateKeywords(GeneratedQuestionCandidateDto question)
    {
        var source = string.Join(
            " ",
            new[]
            {
                question.Title,
                question.Description,
                string.Join(" ", question.TopicTags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
            });

        return Regex.Matches(QuestionFingerprinting.NormalizeLooseText(source), @"[a-z#][a-z0-9#_]{1,}")
            .Select(match => match.Value)
            .Where(token => !DuplicateKeywordStopWords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static double ComputeKeywordOverlap(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0d;
        }

        var intersection = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Count();
        var union = left.Union(right, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0d : (double)intersection / union;
    }

    private static bool HasTopicOverlap(IReadOnlyList<string> left, IReadOnlyList<string> right)
        => left.Any(leftTopic => right.Any(rightTopic =>
            string.Equals(leftTopic?.Trim(), rightTopic?.Trim(), StringComparison.OrdinalIgnoreCase)));

    private static bool MatchesHistoricalScope(
        string? questionSourceScope,
        string requestSourceScope,
        string? questionOwnerUserId,
        string? requestUserId,
        string? questionCourseId,
        string? requestCourseId)
    {
        if (!string.Equals(questionSourceScope?.Trim(), requestSourceScope, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(requestSourceScope, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(questionOwnerUserId, requestUserId, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(requestCourseId))
        {
            return true;
        }

        return string.Equals(questionCourseId, requestCourseId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesHistoricalTopics(
        IReadOnlyList<string>? questionTopics,
        IReadOnlyList<string> requestedTopics)
    {
        if (requestedTopics.Count == 0)
        {
            return true;
        }

        if (questionTopics is null || questionTopics.Count == 0)
        {
            return false;
        }

        return questionTopics.Any(questionTopic =>
            requestedTopics.Any(requestTopic =>
                string.Equals(questionTopic?.Trim(), requestTopic?.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    private static GeneratedQuestionCandidateDto ConvertToGeneratedCandidate(FEQuestion question)
        => new()
        {
            Type = "FE",
            TopicTags = question.TopicTags?.ToList() ?? new List<string>(),
            Difficulty = question.Difficulty,
            Title = question.Title,
            Description = question.Description,
            SourceChunkIds = question.LegacySourceChunkIds?.ToList() ?? new List<string>(),
            Options = question.Options?.ToList(),
            CorrectAnswer = question.CorrectAnswer?.ToList(),
            Explanation = question.Explanation
        };

    private static GeneratedQuestionCandidateDto ConvertToGeneratedCandidate(PEQuestion question)
        => new()
        {
            Type = "PE",
            TopicTags = question.TopicTags?.ToList() ?? new List<string>(),
            Difficulty = question.Difficulty,
            Title = question.Title,
            Description = question.Description,
            SourceChunkIds = question.LegacyChunkIds?.ToList() ?? new List<string>(),
            SkeletonCode = question.SkeletonCode?.Select(file => new CodeFile
            {
                Filename = file.Filename,
                Content = file.Content,
                IsReadOnly = file.IsReadOnly
            }).ToList(),
            SolutionCode = question.SolutionCode?.Select(file => new CodeFile
            {
                Filename = file.Filename,
                Content = file.Content,
                IsReadOnly = file.IsReadOnly
            }).ToList(),
            TestCases = question.TestCases?.Select(test => new TestCase
            {
                Input = test.Input,
                ExpectedOutput = test.ExpectedOutput,
                IsHidden = test.IsHidden
            }).ToList()
        };

    private static double ComputeCosineSimilarity(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        if (left.Count == 0 || right.Count == 0 || left.Count != right.Count)
        {
            return 0d;
        }

        double dot = 0d;
        double leftMagnitude = 0d;
        double rightMagnitude = 0d;
        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude <= 0d || rightMagnitude <= 0d)
        {
            return 0d;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private static void SeedAcceptedQuestionKeys(
        IReadOnlyList<GeneratedQuestionCandidateDto>? seededQuestions,
        HashSet<string> acceptedFingerprints,
        HashSet<string> acceptedTitles,
        HashSet<string> acceptedStructuralSignatures)
    {
        if (seededQuestions is null)
        {
            return;
        }

        foreach (var question in seededQuestions)
        {
            var fingerprint = QuestionFingerprinting.Build(question);
            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                acceptedFingerprints.Add(fingerprint);
            }

            var normalizedTitle = QuestionFingerprinting.NormalizeLooseText(question.Title);
            if (!string.IsNullOrWhiteSpace(normalizedTitle))
            {
                acceptedTitles.Add(normalizedTitle);
            }

            var rawTitle = question.Title?.Trim();
            if (!string.IsNullOrWhiteSpace(rawTitle))
            {
                acceptedTitles.Add(rawTitle);
            }

            var structuralSignature = BuildStructuralDuplicateSignature(question);
            if (!string.IsNullOrWhiteSpace(structuralSignature))
            {
                acceptedStructuralSignatures.Add(structuralSignature);
            }
        }
    }

    private static HashSet<int> ExtractRejectedQuestionIndexes(QuestionReviewDecisionDto review)
    {
        var rejected = new HashSet<int>();
        var regex = new Regex(@"\bQuestion\s+(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        foreach (var issue in review.Issues)
        {
            foreach (Match match in regex.Matches(issue))
            {
                if (int.TryParse(match.Groups[1].Value, out var oneBased) && oneBased > 0)
                {
                    rejected.Add(oneBased - 1);
                }
            }
        }

        return rejected;
    }

    private static List<GeneratedQuestionCandidateDto> CollectAcceptedAttemptQuestions(
        IReadOnlyList<GeneratedQuestionCandidateDto> questions,
        QuestionReviewDecisionDto ruleReview,
        QuestionReviewDecisionDto? llmReview,
        HashSet<string> acceptedFingerprints,
        HashSet<string> acceptedTitles)
    {
        var rejectedIndexes = ExtractRejectedQuestionIndexes(ruleReview);
        if (llmReview is not null)
        {
            rejectedIndexes.UnionWith(ExtractRejectedQuestionIndexes(llmReview));
        }

        var ruleAccepted = string.Equals(ruleReview.ReviewStatus, "accepted", StringComparison.OrdinalIgnoreCase) &&
                           !ruleReview.NeedsRevision &&
                           ruleReview.SchemaValid &&
                           ruleReview.ContentGrounded &&
                           ruleReview.Issues.Count == 0;
        var llmAccepted = llmReview is null ||
                          (string.Equals(llmReview.ReviewStatus, "accepted", StringComparison.OrdinalIgnoreCase) &&
                           !llmReview.NeedsRevision &&
                           llmReview.SchemaValid &&
                           llmReview.ContentGrounded &&
                           llmReview.Issues.Count == 0);
        if (!ruleAccepted || !llmAccepted)
        {
            // Only keep partial acceptance when review explicitly identified rejected indexes.
            // Generic set-level failures should block silent acceptance of the whole attempt.
            var hasExplicitPerQuestionRejection = rejectedIndexes.Count > 0;
            if (!hasExplicitPerQuestionRejection)
            {
                return new List<GeneratedQuestionCandidateDto>();
            }
        }

        var accepted = new List<GeneratedQuestionCandidateDto>();
        for (var index = 0; index < questions.Count; index++)
        {
            if (rejectedIndexes.Contains(index))
            {
                continue;
            }

            var question = questions[index];
            var fingerprint = QuestionFingerprinting.Build(question);
            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                if (acceptedFingerprints.Contains(fingerprint))
                {
                    continue;
                }

                acceptedFingerprints.Add(fingerprint);
            }

            var normalizedTitle = QuestionFingerprinting.NormalizeLooseText(question.Title);
            var rawTitle = question.Title?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedTitle))
            {
                if (acceptedTitles.Contains(normalizedTitle) || (!string.IsNullOrWhiteSpace(rawTitle) && acceptedTitles.Contains(rawTitle)))
                {
                    continue;
                }

                acceptedTitles.Add(normalizedTitle);
                if (!string.IsNullOrWhiteSpace(rawTitle))
                {
                    acceptedTitles.Add(rawTitle);
                }
            }

            accepted.Add(CloneQuestionCandidate(question));
        }

        return accepted;
    }

    private static string BuildRefillGuidance(
        GenerationReviewRequestDto request,
        IReadOnlyList<GeneratedQuestionCandidateDto> acceptedQuestions,
        IReadOnlyList<GenerationReviewResultDto> batchResults,
        int remainingNeeded)
    {
        var guidance = new List<string>
        {
            $"Generate exactly {remainingNeeded} new {request.QuestionType} question(s).",
            "Do not repeat accepted question shapes, titles, or near-equivalent rule checks.",
            "Prefer new coverage and stronger reasoning demand over rewording the same constructor or definition fact."
        };

        var acceptedTitles = acceptedQuestions
            .Select(question => question.Title?.Trim())
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        if (acceptedTitles.Count > 0)
        {
            guidance.Add($"Already accepted question titles to avoid repeating: {string.Join("; ", acceptedTitles)}.");
        }

        var recentIssues = batchResults
            .SelectMany(result => result.Review.Issues)
            .Where(issue => !string.IsNullOrWhiteSpace(issue))
            .Where(issue =>
                issue.Contains("difficulty", StringComparison.OrdinalIgnoreCase) ||
                issue.Contains("repetition", StringComparison.OrdinalIgnoreCase) ||
                issue.Contains("too close", StringComparison.OrdinalIgnoreCase) ||
                issue.Contains("distractor", StringComparison.OrdinalIgnoreCase) ||
                issue.Contains("explanation", StringComparison.OrdinalIgnoreCase) ||
                issue.Contains("undercovers", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        foreach (var issue in recentIssues)
        {
            guidance.Add($"Avoid prior rejection cause: {issue}");
        }

        if (string.Equals(request.QuestionType, "FE", StringComparison.OrdinalIgnoreCase))
        {
            guidance.Add("For FE refill, keep explanations concise, directly justify the correct option, and use distractors grounded in the cited chunk.");
            guidance.Add("For FE Medium refill, avoid pure definition recall; prefer short code-tracing or applied reasoning when the chunk supports it.");
        }
        else
        {
            guidance.Add("For PE refill, keep the task learner-facing, grounded, and behavior-rich; do not regenerate trivial template or relabeled demo tasks.");
        }

        return string.Join("\n", guidance);
    }

    private static QuestionReviewDecisionDto AggregateAttemptReviewsForCore(
        IReadOnlyList<QuestionReviewDecisionDto> attemptReviews,
        int requestedCount,
        int acceptedCount)
    {
        if (attemptReviews.Count == 0)
        {
            return new QuestionReviewDecisionDto
            {
                ReviewStatus = "needs_revision",
                Issues = new List<string> { "No attempt reviews were produced." },
                Suggestions = new List<string> { "Retry the request or broaden the selected scope." },
                Score = 0.2m,
                SchemaValid = false,
                ContentGrounded = false,
                NeedsRevision = true,
                ReviewerModel = string.Empty,
                ReviewMode = "AttemptAggregate"
            };
        }

        if (acceptedCount >= requestedCount && requestedCount > 0)
        {
            return BuildSatisfiedAcceptedReview(attemptReviews, "AttemptAggregate");
        }

        var issues = attemptReviews
            .SelectMany(review => review.Issues)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var suggestions = attemptReviews
            .SelectMany(review => review.Suggestions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (acceptedCount < requestedCount)
        {
            issues.Add($"Only {acceptedCount} accepted question(s) were retained out of the requested {requestedCount} after per-attempt review and refill.");
            suggestions.Add("The pipeline kept accepted questions across attempts and stopped when the remaining candidates could not pass review safely.");
        }

        var accepted = acceptedCount >= requestedCount &&
                       attemptReviews.All(review =>
                           string.Equals(review.ReviewStatus, "accepted", StringComparison.OrdinalIgnoreCase) &&
                           !review.NeedsRevision &&
                           review.SchemaValid &&
                           review.ContentGrounded &&
                           review.Issues.Count == 0);
        return new QuestionReviewDecisionDto
        {
            ReviewStatus = accepted ? "accepted" : "needs_revision",
            Issues = issues,
            Suggestions = suggestions,
            Score = attemptReviews.Average(review => review.Score),
            SchemaValid = acceptedCount > 0 || attemptReviews.All(review => review.SchemaValid),
            ContentGrounded = acceptedCount > 0 || attemptReviews.All(review => review.ContentGrounded),
            NeedsRevision = !accepted,
            ReviewerModel = attemptReviews.Select(review => review.ReviewerModel).FirstOrDefault(model => !string.IsNullOrWhiteSpace(model)) ?? string.Empty,
            ReviewMode = attemptReviews.Select(review => review.ReviewMode).FirstOrDefault(mode => !string.IsNullOrWhiteSpace(mode)) ?? "AttemptAggregate"
        };
    }


    private static QuestionPersistenceResultDto AggregatePersistence(IEnumerable<GenerationReviewResultDto> bucketResults)
    {
        var result = new QuestionPersistenceResultDto();
        foreach (var bucket in bucketResults)
        {
            result.FeQuestionIds.AddRange(bucket.Persistence.FeQuestionIds);
            result.PeQuestionIds.AddRange(bucket.Persistence.PeQuestionIds);
        }

        result.PersistedQuestionIds = result.FeQuestionIds
            .Concat(result.PeQuestionIds)
            .ToList();
        result.PersistedCount = result.PersistedQuestionIds.Count;
        result.Persisted = result.PersistedCount > 0;
        return result;
    }

    private static GenerationMetricsDto AggregateMetrics(IReadOnlyList<GenerationReviewResultDto> bucketResults)
    {
        var first = bucketResults.FirstOrDefault()?.Metrics ?? new GenerationMetricsDto();
        return new GenerationMetricsDto
        {
            GeneratorProvider = first.GeneratorProvider,
            GeneratorConfiguredModel = first.GeneratorConfiguredModel,
            GeneratorEffectiveModel = first.GeneratorEffectiveModel,
            GeneratorModelFamily = first.GeneratorModelFamily,
            ReviewerProvider = first.ReviewerProvider,
            ReviewerConfiguredModel = first.ReviewerConfiguredModel,
            ReviewerEffectiveModel = first.ReviewerEffectiveModel,
            ReviewerModelFamily = first.ReviewerModelFamily,
            GenerationInputTokens = bucketResults.Sum(item => item.Metrics.GenerationInputTokens),
            GenerationOutputTokens = bucketResults.Sum(item => item.Metrics.GenerationOutputTokens),
            ReviewInputTokens = bucketResults.Sum(item => item.Metrics.ReviewInputTokens),
            ReviewOutputTokens = bucketResults.Sum(item => item.Metrics.ReviewOutputTokens),
            GenerationRunId = $"mixed-{bucketResults.Count}-buckets",
            ChunkCount = first.ChunkCount,
            ContextPackId = first.ContextPackId,
            TotalReportedCostUsd = bucketResults.Sum(item => item.Metrics.TotalReportedCostUsd),
            UsdToVndRate = first.UsdToVndRate,
            ChargedVnd = bucketResults.Sum(item => item.Metrics.ChargedVnd),
            ActualDeductedVnd = bucketResults.Sum(item => item.Metrics.ActualDeductedVnd),
            AbsorbedVnd = bucketResults.Sum(item => item.Metrics.AbsorbedVnd),
            RemainingBalanceVnd = bucketResults.LastOrDefault()?.Metrics.RemainingBalanceVnd ?? first.RemainingBalanceVnd,
            GenerationUsageSource = first.GenerationUsageSource,
            ReviewUsageSource = first.ReviewUsageSource,
            GenerationCostSource = first.GenerationCostSource,
            ReviewCostSource = first.ReviewCostSource,
            PersistedFeQuestionIds = bucketResults.SelectMany(item => item.Metrics.PersistedFeQuestionIds).ToList(),
            PersistedPeQuestionIds = bucketResults.SelectMany(item => item.Metrics.PersistedPeQuestionIds).ToList(),
            GeneratorPromptKey = first.GeneratorPromptKey,
            GeneratorPromptVersion = first.GeneratorPromptVersion,
            ReviewerPromptKey = first.ReviewerPromptKey,
            ReviewerPromptVersion = first.ReviewerPromptVersion,
            RubricKey = first.RubricKey,
            RubricVersion = first.RubricVersion
        };
    }

    private static QuestionReviewDecisionDto AggregateReview(IReadOnlyList<GenerationReviewResultDto> bucketResults)
    {
        var reviews = bucketResults.Select(item => item.Review).ToList();
        var accepted = reviews.All(review =>
            !review.NeedsRevision &&
            string.Equals(review.ReviewStatus, "accepted", StringComparison.OrdinalIgnoreCase));
        var issues = new List<string>();
        var suggestions = new List<string>();

        for (var index = 0; index < bucketResults.Count; index++)
        {
            var bucket = bucketResults[index];
            foreach (var issue in bucket.Review.Issues)
            {
                issues.Add($"[{bucket.Difficulty}] {issue}");
            }

            foreach (var suggestion in bucket.Review.Suggestions)
            {
                suggestions.Add($"[{bucket.Difficulty}] {suggestion}");
            }
        }

        return new QuestionReviewDecisionDto
        {
            ReviewStatus = accepted ? "accepted" : "needs_revision",
            Issues = issues,
            Suggestions = suggestions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Score = reviews.Count == 0 ? 0m : reviews.Average(review => review.Score),
            SchemaValid = reviews.All(review => review.SchemaValid),
            ContentGrounded = reviews.All(review => review.ContentGrounded),
            NeedsRevision = !accepted,
            ReviewerModel = bucketResults.Select(item => item.ReviewerModel).FirstOrDefault(model => !string.IsNullOrWhiteSpace(model)) ?? string.Empty,
            ReviewMode = "MixedDifficulty"
        };
    }

    private static QuestionReviewDecisionDto AggregateBatchResultsForSingleDifficulty(
        IReadOnlyList<GenerationReviewResultDto> batchResults,
        int requestedCount,
        int acceptedCount)
    {
        if (batchResults.Count == 0)
        {
            return new QuestionReviewDecisionDto
            {
                ReviewStatus = "needs_revision",
                NeedsRevision = true,
                SchemaValid = false,
                ContentGrounded = false,
                Score = 0.2m,
                ReviewerModel = string.Empty,
                ReviewMode = "BatchedSingleDifficulty",
                Issues = new List<string> { "No generation batches were executed." },
                Suggestions = new List<string> { "Retry the request or broaden the selected scope." }
            };
        }

        if (acceptedCount >= requestedCount && requestedCount > 0)
        {
            return BuildSatisfiedAcceptedReview(batchResults.Select(item => item.Review).ToList(), "BatchedSingleDifficulty");
        }

        var issues = new List<string>();
        var suggestions = new List<string>();
        foreach (var batch in batchResults)
        {
            issues.AddRange(batch.Review.Issues);
            suggestions.AddRange(batch.Review.Suggestions);
        }

        if (acceptedCount < requestedCount)
        {
            issues.Add($"Only {acceptedCount} accepted question(s) were accumulated out of the requested {requestedCount}.");
            suggestions.Add("The pipeline kept accepted questions and stopped when refill attempts or quality constraints prevented safe completion.");
        }

        var accepted = acceptedCount >= requestedCount;
        return new QuestionReviewDecisionDto
        {
            ReviewStatus = accepted ? "accepted" : "needs_revision",
            Issues = issues.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Suggestions = suggestions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Score = batchResults.Average(item => item.Review.Score),
            SchemaValid = batchResults.All(item => item.Review.SchemaValid),
            ContentGrounded = batchResults.All(item => item.Review.ContentGrounded),
            NeedsRevision = !accepted,
            ReviewerModel = batchResults.Select(item => item.ReviewerModel).FirstOrDefault(model => !string.IsNullOrWhiteSpace(model)) ?? string.Empty,
            ReviewMode = "BatchedSingleDifficulty"
        };
    }

    private static QuestionReviewDecisionDto BuildSatisfiedAcceptedReview(
        IReadOnlyList<QuestionReviewDecisionDto> reviews,
        string reviewMode)
    {
        return new QuestionReviewDecisionDto
        {
            ReviewStatus = "accepted",
            Issues = new List<string>(),
            Suggestions = new List<string>(),
            Score = reviews.Count == 0 ? 1m : reviews.Average(review => review.Score),
            SchemaValid = reviews.Count == 0 || reviews.Any(review => review.SchemaValid),
            ContentGrounded = reviews.Count == 0 || reviews.Any(review => review.ContentGrounded),
            NeedsRevision = false,
            ReviewerModel = reviews.Select(review => review.ReviewerModel).FirstOrDefault(model => !string.IsNullOrWhiteSpace(model)) ?? string.Empty,
            ReviewMode = reviewMode
        };
    }

    private static GenerationShortfallReportDto? BuildMixedShortfallReport(
        GenerationReviewRequestDto request,
        ChunkResolutionResult resolvedSource,
        IReadOnlyList<DifficultyGenerationReportDto> difficultyReports,
        QuestionReviewDecisionDto aggregatedReview)
    {
        var requestedCount = difficultyReports.Sum(item => item.RequestedCount);
        var generatedCount = difficultyReports.Sum(item => item.GeneratedCount);
        var bucketsWithShortfall = difficultyReports
            .Where(item => item.GeneratedCount < item.RequestedCount || item.NeedsRevision)
            .ToList();

        if (bucketsWithShortfall.Count == 0)
        {
            return null;
        }

        var notes = new List<string>();
        var suggestedActions = new List<string>();
        foreach (var bucket in bucketsWithShortfall)
        {
            if (bucket.ShortfallReport?.Summary is not null)
            {
                notes.Add($"[{bucket.Difficulty}] {bucket.ShortfallReport.Summary}");
            }

            foreach (var note in bucket.ShortfallReport?.Notes ?? new List<string>())
            {
                notes.Add($"[{bucket.Difficulty}] {note}");
            }

            foreach (var action in bucket.ShortfallReport?.SuggestedActions ?? new List<string>())
            {
                suggestedActions.Add($"[{bucket.Difficulty}] {action}");
            }
        }

        return new GenerationShortfallReportDto
        {
            RequestedCount = requestedCount,
            GeneratedCount = generatedCount,
            StopReason = generatedCount < requestedCount
                ? "requested_count_exceeds_grounded_capacity"
                : "mixed_bucket_review_rejection",
            Summary = $"Requested {requestedCount} mixed-difficulty question(s), but only {generatedCount} grounded question(s) could be produced across the requested buckets.",
            CoverageRatio = resolvedSource.ContextPack?.TopicCoverageRatio ?? 0m,
            PackedTokenCount = resolvedSource.ContextPack?.TokenCount ?? 0,
            DominantChapter = resolvedSource.ContextPack?.DominantChapterKey,
            CoveredTopics = resolvedSource.ContextPack?.CoveredTopics ?? new List<string>(),
            UncoveredTopics = resolvedSource.ContextPack?.UncoveredTopics ?? new List<string>(),
            Notes = notes.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SuggestedActions = suggestedActions.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private async Task<AITextResponse> GenerateQuestionsAsync(
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto> chunks,
        AIResolvedExecutionOptions resolved,
        AIPromptTemplateDto prompt,
        JsonElement rubric,
        QuestionGenerationReviewRuntimePolicy? policy,
        string? revisionFeedback,
        IReadOnlyList<GeneratedQuestionCandidateDto>? previousQuestions,
        CancellationToken cancellationToken)
    {
        var promptChunks = SelectPromptChunksForRequest(request, chunks);
        resolved = ApplyGenerationRuntimeCaps(request, resolved);
        var renderedUserPrompt = _promptService.Render(prompt.UserPrompt, new Dictionary<string, string?>
        {
            ["subject"] = request.Subject,
            ["difficulty"] = request.Difficulty,
            ["question_type"] = request.QuestionType,
            ["count"] = request.Count.ToString(),
            ["source_scope"] = request.Retrieval?.SourceScope,
            ["chapter_key"] = request.Retrieval?.ChapterKey,
            ["chapter_title"] = ResolveChapterTitle(promptChunks, request.Retrieval?.ChapterKey),
            ["selected_topics_json"] = JsonSerializer.Serialize(request.Retrieval?.TargetTopics ?? new List<string>(), JsonOptions),
            ["subject_focus"] = BuildSubjectFocus(request.Subject, request.QuestionType, policy),
            ["generation_guidance"] = BuildGenerationGuidance(request, promptChunks, policy),
            ["rubric_json"] = JsonSerializer.Serialize(rubric, JsonOptions),
            ["chunks_json"] = SerializeChunksForPrompt(request, promptChunks),
            ["revision_feedback"] = string.IsNullOrWhiteSpace(revisionFeedback) ? "none" : revisionFeedback,
            ["previous_questions_json"] = previousQuestions is null ? "[]" : SerializeQuestionsForPrompt(previousQuestions, PromptQuestionSerializationMode.PreviousQuestions)
        });

        return await _aiExecutionService.ExecuteTextAsync(
            resolved,
            new AITextRequest
            {
                FeatureName = AIFeatureNames.QuestionGeneration,
                SystemPrompt = prompt.SystemPrompt,
                UserPrompt = renderedUserPrompt,
                Provider = resolved.Provider,
                Model = resolved.Model,
                Temperature = resolved.Temperature,
                MaxTokens = resolved.MaxTokens
            },
            cancellationToken);
    }

    private async Task<RepairGenerationResult> AttemptRepairAsync(
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto> chunks,
        AIResolvedExecutionOptions resolved,
        AIPromptTemplateDto prompt,
        JsonElement rubric,
        QuestionGenerationReviewRuntimePolicy? policy,
        string brokenResponse,
        CancellationToken cancellationToken)
    {
        var promptChunks = SelectPromptChunksForRequest(request, chunks);
        resolved = ApplyGenerationRuntimeCaps(request, resolved);
        var renderedUserPrompt = _promptService.Render(prompt.UserPrompt, new Dictionary<string, string?>
        {
            ["subject"] = request.Subject,
            ["difficulty"] = request.Difficulty,
            ["question_type"] = request.QuestionType,
            ["source_scope"] = request.Retrieval?.SourceScope,
            ["chapter_key"] = request.Retrieval?.ChapterKey,
            ["chapter_title"] = ResolveChapterTitle(promptChunks, request.Retrieval?.ChapterKey),
            ["selected_topics_json"] = JsonSerializer.Serialize(request.Retrieval?.TargetTopics ?? new List<string>(), JsonOptions),
            ["subject_focus"] = BuildSubjectFocus(request.Subject, request.QuestionType, policy),
            ["generation_guidance"] = BuildGenerationGuidance(request, promptChunks, policy),
            ["rubric_json"] = JsonSerializer.Serialize(rubric, JsonOptions),
            ["chunks_json"] = SerializeChunksForPrompt(request, promptChunks),
            ["normalization_issues"] = "Generated response could not be parsed into the required JSON array schema.",
            ["candidate_response"] = brokenResponse
        });

        var response = await _aiExecutionService.ExecuteTextAsync(
            resolved,
            new AITextRequest
            {
                FeatureName = AIFeatureNames.QuestionGeneration,
                SystemPrompt = prompt.SystemPrompt,
                UserPrompt = renderedUserPrompt,
                Provider = resolved.Provider,
                Model = resolved.Model,
                Temperature = 0.2,
                MaxTokens = resolved.MaxTokens
            },
            cancellationToken);

        return new RepairGenerationResult(ParseQuestions(response.Content), response);
    }

    private async Task<AITextResponse> ReviewQuestionsAsync(
        GenerationReviewRequestDto request,
        IReadOnlyList<GeneratedQuestionCandidateDto> questions,
        IReadOnlyList<RetrievedChunkDto> chunks,
        AIResolvedExecutionOptions resolved,
        AIPromptTemplateDto prompt,
        JsonElement rubric,
        QuestionGenerationReviewRuntimePolicy? policy,
        CancellationToken cancellationToken)
    {
        var promptChunks = SelectPromptChunksForRequest(request, chunks);
        resolved = ApplyReviewRuntimeCaps(request, resolved);
        var renderedUserPrompt = _promptService.Render(prompt.UserPrompt, new Dictionary<string, string?>
        {
            ["subject"] = request.Subject,
            ["question_type"] = request.QuestionType,
            ["source_scope"] = request.Retrieval?.SourceScope,
            ["chapter_key"] = request.Retrieval?.ChapterKey,
            ["chapter_title"] = ResolveChapterTitle(promptChunks, request.Retrieval?.ChapterKey),
            ["selected_topics_json"] = JsonSerializer.Serialize(request.Retrieval?.TargetTopics ?? new List<string>(), JsonOptions),
            ["subject_focus"] = BuildSubjectFocus(request.Subject, request.QuestionType, policy),
            ["rubric_json"] = JsonSerializer.Serialize(rubric, JsonOptions),
            ["chunks_json"] = SerializeChunksForPrompt(request, promptChunks),
            ["questions_json"] = SerializeQuestionsForPrompt(questions, PromptQuestionSerializationMode.Review)
        });

        return await _aiExecutionService.ExecuteTextAsync(
            resolved,
            new AITextRequest
            {
                FeatureName = AIFeatureNames.QuestionReview,
                SystemPrompt = prompt.SystemPrompt,
                UserPrompt = renderedUserPrompt,
                Provider = resolved.Provider,
                Model = resolved.Model,
                Temperature = resolved.Temperature,
                MaxTokens = resolved.MaxTokens
            },
            cancellationToken);
    }

    private async Task<QuestionReviewDecisionDto> BuildRuleBasedReviewAsync(
        GenerationReviewRequestDto request,
        IReadOnlyList<GeneratedQuestionCandidateDto> questions,
        IReadOnlyList<RetrievedChunkDto> chunks,
        IReadOnlyList<GeneratedQuestionCandidateDto>? previousQuestions,
        QuestionGenerationReviewRuntimePolicy? policy,
        string reviewerModel,
        CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        var suggestions = new List<string>();
        var schemaValid = true;
        var contentGrounded = true;
        var chunkMap = chunks.ToDictionary(chunk => chunk.ChunkId, StringComparer.OrdinalIgnoreCase);
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceScope = ResolveQuestionSourceScope(request);
        var sourceActor = request.UseActualCostOnly ? "Admin" : "Student";
        var ownerUserId = string.Equals(sourceScope, "BYOS", StringComparison.OrdinalIgnoreCase) ? request.UserId : null;
        var shapeFingerprints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var previousFingerprints = (previousQuestions ?? Array.Empty<GeneratedQuestionCandidateDto>())
            .Select(QuestionFingerprinting.Build)
            .Where(fingerprint => !string.IsNullOrWhiteSpace(fingerprint))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (questions.Count == 0)
        {
            var missingFieldSummary = string.Equals(request.QuestionType, "FE", StringComparison.OrdinalIgnoreCase)
                ? "options, correct_answer labels, explanations,"
                : "code/test-case fields,";
            return new QuestionReviewDecisionDto
            {
                ReviewStatus = "needs_revision",
                Issues = new List<string>
                {
                    $"The question set is empty, so there is no assessable {request.QuestionType} item to review.",
                    $"The submission does not provide any question objects, {missingFieldSummary} or source_chunk_ids required for a valid {request.QuestionType} assessment set.",
                    "Because no questions are present, grounding to the provided chunks cannot be established.",
                    "The set cannot be evaluated for rubric alignment, difficulty, distractor quality, or instructional usefulness."
                },
                Suggestions = new List<string>
                {
                    $"Provide at least one complete {request.QuestionType} question object.",
                    "Ensure each question is grounded in the selected chapter and topics and cites valid source_chunk_ids.",
                    string.Equals(request.QuestionType, "FE", StringComparison.OrdinalIgnoreCase)
                        ? "Include plausible multiple-choice options, a label-based correct_answer, and a concise explanation grounded in the chunk evidence."
                        : "Include complete skeleton code, solution code, and executable test cases that match the task statement."
                },
                Score = 0.2m,
                SchemaValid = false,
                ContentGrounded = false,
                NeedsRevision = true,
                ReviewerModel = reviewerModel,
                ReviewMode = "RuleBased"
            };
        }

        for (var index = 0; index < questions.Count; index++)
        {
            var question = questions[index];
            var citedChunks = question.SourceChunkIds
                .Where(chunkMap.ContainsKey)
                .Select(id => chunkMap[id])
                .ToList();

            if (!titles.Add(question.Title))
            {
                issues.Add($"Duplicate generated title detected: '{question.Title}'.");
            }

            if (!string.Equals(question.Type, request.QuestionType, StringComparison.OrdinalIgnoreCase))
            {
                schemaValid = false;
                issues.Add($"Question {index + 1}: type mismatch.");
            }

            if (string.IsNullOrWhiteSpace(question.Title) || string.IsNullOrWhiteSpace(question.Description))
            {
                schemaValid = false;
                issues.Add($"Question {index + 1}: title and description are required.");
            }

            if (question.SourceChunkIds.Count == 0 || question.SourceChunkIds.Any(id => !chunkMap.ContainsKey(id)))
            {
                schemaValid = false;
                contentGrounded = false;
                issues.Add($"Question {index + 1}: source_chunk_ids are missing or reference unknown chunks.");
            }
            else
            {
                var groundingIssues = GetGroundingQualityIssues(question, citedChunks);
                if (groundingIssues.Count > 0)
                {
                    contentGrounded = false;
                    foreach (var groundingIssue in groundingIssues)
                    {
                        issues.Add($"Question {index + 1}: {groundingIssue}");
                    }
                }
            }

            var topicOverlap = question.SourceChunkIds
                .Where(chunkMap.ContainsKey)
                .SelectMany(id => chunkMap[id].TopicTags)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Intersect(question.TopicTags, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (question.TopicTags.Count > 0 && topicOverlap.Count == 0)
            {
                contentGrounded = false;
                issues.Add($"Question {index + 1}: topic_tags do not overlap with the cited chunk tags.");
            }

            if (ContainsMetaSourceWording(question.Title) || ContainsMetaSourceWording(question.Description))
            {
                issues.Add($"Question {index + 1}: contains meta-source wording.");
            }

            var fingerprint = QuestionFingerprinting.Build(question);
            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                if (shapeFingerprints.TryGetValue(fingerprint, out var firstIndex))
                {
                    issues.Add($"Question {index + 1}: duplicate question shape detected with question {firstIndex + 1}.");
                }
                else
                {
                    shapeFingerprints[fingerprint] = index;
                }

                if (previousFingerprints.Contains(fingerprint))
                {
                    issues.Add($"Question {index + 1}: near-duplicate of a previous generation attempt.");
                }
            }

            if (string.Equals(request.QuestionType, "FE", StringComparison.OrdinalIgnoreCase))
            {
                var expectedOptionCount = policy?.FeRules?.OptionCount is > 0 ? policy.FeRules.OptionCount : 4;
                if (question.Options is null || question.Options.Count != expectedOptionCount)
                {
                    schemaValid = false;
                    issues.Add($"Question {index + 1}: FE must contain exactly {expectedOptionCount} options.");
                }

                var labels = question.CorrectAnswer ?? new List<string>();
                var expectedCorrectAnswerCount = policy?.FeRules?.CorrectAnswerCount is > 0 ? policy.FeRules.CorrectAnswerCount : 1;
                var labelPattern = string.IsNullOrWhiteSpace(policy?.FeRules?.CorrectAnswerLabelRegex)
                    ? "^[A-D]$"
                    : policy!.FeRules!.CorrectAnswerLabelRegex!;
                var optionSet = (question.Options ?? new List<string>())
                    .Select(item => item?.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var isValidCorrect = labels.All(label =>
                    Regex.IsMatch(label ?? string.Empty, labelPattern, RegexOptions.IgnoreCase) ||
                    optionSet.Contains(label.Trim()));

                if (labels.Count != expectedCorrectAnswerCount || !isValidCorrect)
                {
                    schemaValid = false;
                    issues.Add($"Question {index + 1}: FE correct_answer must contain exactly {expectedCorrectAnswerCount} answer(s) as either an option label (matching '{labelPattern}') or exact option text.");
                }

                var feQualityIssues = GetFeQualityIssues(question, citedChunks, policy);
                if (feQualityIssues.Count > 0)
                {
                    foreach (var feQualityIssue in feQualityIssues)
                    {
                        issues.Add($"Question {index + 1}: {feQualityIssue}");
                    }
                }
            }
            else
            {
                if (question.SkeletonCode is null || question.SkeletonCode.Count == 0)
                {
                    schemaValid = false;
                    issues.Add($"Question {index + 1}: PE skeleton_code is required.");
                }

                if (question.SolutionCode is null || question.SolutionCode.Count == 0)
                {
                    schemaValid = false;
                    issues.Add($"Question {index + 1}: PE solution_code is required.");
                }

                if (question.TestCases is null || question.TestCases.Count < 2)
                {
                    schemaValid = false;
                    issues.Add($"Question {index + 1}: PE must contain at least 2 test cases.");
                }

                var lowValuePeIssues = GetLowValuePeIssues(question, chunkMap, policy);
                if (lowValuePeIssues.Count > 0)
                {
                    foreach (var lowValueIssue in lowValuePeIssues)
                    {
                        issues.Add($"Question {index + 1}: {lowValueIssue}");
                    }
                }

                var peConsistencyIssues = GetPeConsistencyIssues(question, citedChunks);
                if (peConsistencyIssues.Count > 0)
                {
                    contentGrounded = false;
                    foreach (var consistencyIssue in peConsistencyIssues)
                    {
                        issues.Add($"Question {index + 1}: {consistencyIssue}");
                    }
                }
            }
        }

        foreach (var question in questions)
        {
            var fingerprint = QuestionFingerprinting.Build(question);
            if (string.Equals(request.QuestionType, "FE", StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _feQuestionRepository.GetByTitleInScopeAsync(sourceScope, request.CourseId, ownerUserId, question.Title);
                if (existing is not null)
                {
                    issues.Add($"Duplicate existing FE question title detected: '{question.Title}'.");
                }

                if (!string.IsNullOrWhiteSpace(fingerprint))
                {
                    var existingByFingerprint = await _feQuestionRepository.GetByFingerprintInScopeAsync(sourceScope, request.CourseId, ownerUserId, fingerprint);
                    if (existingByFingerprint is not null)
                    {
                        issues.Add($"Duplicate existing FE question fingerprint detected for title '{question.Title}'.");
                    }
                }
            }
            else
            {
                var existing = await _peQuestionRepository.GetByTitleInScopeAsync(sourceScope, request.CourseId, ownerUserId, question.Title);
                if (existing is not null)
                {
                    issues.Add($"Duplicate existing PE question title detected: '{question.Title}'.");
                }

                if (!string.IsNullOrWhiteSpace(fingerprint))
                {
                    var existingByFingerprint = await _peQuestionRepository.GetByFingerprintInScopeAsync(sourceScope, request.CourseId, ownerUserId, fingerprint);
                    if (existingByFingerprint is not null)
                    {
                        issues.Add($"Duplicate existing PE question fingerprint detected for title '{question.Title}'.");
                    }
                }
            }
        }

        if (issues.Count == 0)
        {
            suggestions.Add("Rule-based review accepted the generated set.");
        }
        else
        {
            suggestions.Add("Fix schema, grounding, and duplication issues before persisting.");
            if (string.Equals(request.QuestionType, "FE", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add("For FE, keep the stem concept-focused, ensure exactly one clearly best answer, and avoid weak distractors such as obviously unrelated choices or meta options.");
            }
            else
            {
                suggestions.Add("For PE, avoid lecture-demo shapes such as simple message-label overriding, toString-only formatting, or same-logic tasks with renamed nouns.");
                suggestions.Add("For PE, if the first draft is only a one-expression formula demo or a trivial abstract/subclass template, regenerate into a learner-facing task with stronger behavior rules and broader test variation.");
            }
        }

        var accepted = schemaValid && contentGrounded && issues.Count == 0;
        return new QuestionReviewDecisionDto
        {
            ReviewStatus = accepted ? "accepted" : "needs_revision",
            Issues = issues,
            Suggestions = suggestions,
            Score = accepted ? 0.92m : Math.Max(0.2m, 0.9m - (issues.Count * 0.12m)),
            SchemaValid = schemaValid,
            ContentGrounded = contentGrounded,
            NeedsRevision = !accepted,
            ReviewerModel = reviewerModel,
            ReviewMode = "RuleBased"
        };
    }

    private async Task<QuestionPersistenceResultDto> PersistQuestionsAsync(
        GenerationReviewRequestDto request,
        IReadOnlyList<GeneratedQuestionCandidateDto> questions,
        QuestionReviewDecisionDto review,
        IReadOnlyList<RetrievedChunkDto> chunks,
        CancellationToken cancellationToken)
    {
        if (!request.PersistQuestions || request.SuppressPersistenceSideEffects)
        {
            return new QuestionPersistenceResultDto { Persisted = false };
        }

        if (questions.Count == 0)
        {
            return new QuestionPersistenceResultDto
            {
                Persisted = false,
                SkippedReasons = new List<string> { "No generated questions were available to persist." }
            };
        }

        var result = new QuestionPersistenceResultDto();
        var sourceScope = ResolveQuestionSourceScope(request);
        var sourceActor = request.UseActualCostOnly ? "Admin" : "Student";
        var ownerUserId = string.Equals(sourceScope, "BYOS", StringComparison.OrdinalIgnoreCase) ? request.UserId : null;
        var defaultStatus = "Draft";
        var persistedCourseId = await ResolvePersistCourseIdAsync(request, chunks, cancellationToken);
        foreach (var question in questions)
        {
            if (string.IsNullOrWhiteSpace(question.Title))
            {
                result.SkippedReasons.Add("Skipped a generated question because its title is empty.");
                continue;
            }

            var fingerprint = QuestionFingerprinting.Build(question);

            if (string.Equals(question.Type, "FE", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(persistedCourseId))
                {
                    result.SkippedReasons.Add($"Skipped FE question '{question.Title}' because the source document is missing a valid course.");
                    continue;
                }

                var existing = await _feQuestionRepository.GetByTitleInScopeAsync(sourceScope, request.CourseId, ownerUserId, question.Title);
                if (existing is not null)
                {
                    result.SkippedReasons.Add($"Skipped FE question '{question.Title}' because the title already exists in this scope.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(fingerprint))
                {
                    var existingByFingerprint = await _feQuestionRepository.GetByFingerprintInScopeAsync(sourceScope, request.CourseId, ownerUserId, fingerprint);
                    if (existingByFingerprint is not null)
                    {
                        result.SkippedReasons.Add($"Skipped FE question '{question.Title}' because a duplicate fingerprint already exists in this scope.");
                        continue;
                    }
                }

                var entity = new FEQuestion
                {
                    CourseId = persistedCourseId,
                    TopicTags = question.TopicTags,
                    Difficulty = question.Difficulty,
                    Title = question.Title,
                    Description = question.Description,
                    Options = question.Options ?? new List<string>(),
                    CorrectAnswer = question.CorrectAnswer ?? new List<string>(),
                    Explanation = question.Explanation,
                    Status = defaultStatus,
                    Source = sourceActor,
                    SourceScope = sourceScope,
                    OwnerUserId = ownerUserId,
                    CreatedBy = request.UserId,
                    IsPublic = false,
                    QuestionFingerprint = fingerprint,
                    SourceDocumentIds = ResolveSourceDocumentIds(question, request, chunks)
                };
                await _feQuestionRepository.CreateAsync(entity);
                result.FeQuestionIds.Add(entity.Id);
                question.PersistedQuestionId = entity.Id;
                question.PersistedStatus = entity.Status;
                question.PersistedIsPublic = entity.IsPublic;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(persistedCourseId))
                {
                    result.SkippedReasons.Add($"Skipped PE question '{question.Title}' because the source document is missing a valid course.");
                    continue;
                }

                var existing = await _peQuestionRepository.GetByTitleInScopeAsync(sourceScope, request.CourseId, ownerUserId, question.Title);
                if (existing is not null)
                {
                    result.SkippedReasons.Add($"Skipped PE question '{question.Title}' because the title already exists in this scope.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(fingerprint))
                {
                    var existingByFingerprint = await _peQuestionRepository.GetByFingerprintInScopeAsync(sourceScope, request.CourseId, ownerUserId, fingerprint);
                    if (existingByFingerprint is not null)
                    {
                        result.SkippedReasons.Add($"Skipped PE question '{question.Title}' because a duplicate fingerprint already exists in this scope.");
                        continue;
                    }
                }

                if (question.SkeletonCode is null || question.SkeletonCode.Count == 0)
                {
                    result.SkippedReasons.Add($"Skipped PE question '{question.Title}' because skeleton code is missing.");
                    continue;
                }

                if (question.SolutionCode is null || question.SolutionCode.Count == 0)
                {
                    result.SkippedReasons.Add($"Skipped PE question '{question.Title}' because solution code is missing.");
                    continue;
                }

                if (question.TestCases is null || question.TestCases.Count == 0)
                {
                    result.SkippedReasons.Add($"Skipped PE question '{question.Title}' because test cases are missing.");
                    continue;
                }

                var entity = new PEQuestion
                {
                    CourseId = persistedCourseId,
                    TopicTags = question.TopicTags,
                    Difficulty = question.Difficulty,
                    Title = question.Title,
                    Description = question.Description,
                    SkeletonCode = question.SkeletonCode
    ?? throw new InvalidOperationException(
        "Accepted PE question is missing skeleton code."),

                    SolutionCode = question.SolutionCode
    ?? throw new InvalidOperationException(
        "Accepted PE question is missing solution code."),

                    TestCases = question.TestCases
    ?? throw new InvalidOperationException(
        "Accepted PE question is missing test cases."),
                    Status = defaultStatus,
                    Source = sourceActor,
                    SourceScope = sourceScope,
                    OwnerUserId = ownerUserId,
                    CreatedBy = request.UserId,
                    IsPublic = false,
                    AllowedLanguageIds = new List<int>
                    {
                        InferPeLanguageId(question)
                    },
                    DefaultLanguageId = InferPeLanguageId(question),
                    QuestionFingerprint = fingerprint,
                    SourceDocumentIds = ResolveSourceDocumentIds(question, request, chunks)
                };
                await _peQuestionRepository.CreateAsync(entity);
                result.PeQuestionIds.Add(entity.Id);
                question.PersistedQuestionId = entity.Id;
                question.PersistedStatus = entity.Status;
                question.PersistedIsPublic = entity.IsPublic;
            }
        }

        result.PersistedQuestionIds = result.FeQuestionIds
            .Concat(result.PeQuestionIds)
            .ToList();
        result.PersistedCount = result.PersistedQuestionIds.Count;
        result.Persisted = result.PersistedCount > 0;

        return result;
    }

    private async Task<string?> ResolvePersistCourseIdAsync(
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto> chunks,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.CourseId))
        {
            return request.CourseId;
        }

        var documentIds = ResolveSourceDocumentIds(
            new GeneratedQuestionCandidateDto
            {
                SourceChunkIds = chunks
                    .Where(chunk => !string.IsNullOrWhiteSpace(chunk.ChunkId))
                    .Select(chunk => chunk.ChunkId)
                    .ToList()
            },
            request,
            chunks);

        foreach (var documentId in documentIds)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                continue;
            }

            var document = await _documentRepository.GetByIdAsync(documentId);
            if (!string.IsNullOrWhiteSpace(document?.CourseId))
            {
                return document.CourseId;
            }
        }

        return null;
    }

    private static List<string> ResolveSourceDocumentIds(
        GeneratedQuestionCandidateDto question,
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto> chunks)
    {
        var sourceDocumentIds = chunks
            .Where(chunk =>
                !string.IsNullOrWhiteSpace(chunk.DocumentId) &&
                question.SourceChunkIds.Contains(chunk.ChunkId, StringComparer.OrdinalIgnoreCase))
            .Select(chunk => chunk.DocumentId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sourceDocumentIds.Count == 0 && !string.IsNullOrWhiteSpace(request.DocumentId))
        {
            sourceDocumentIds.Add(request.DocumentId);
        }

        return sourceDocumentIds;
    }

    private static List<GeneratedQuestionCandidateDto> NormalizeQuestions(
        List<GeneratedQuestionCandidateDto> questions,
        string questionType,
        IReadOnlyList<RetrievedChunkDto> chunks,
        IEnumerable<string> validChunkIds)
    {
        var validChunkIdSet = validChunkIds
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var question in questions)
        {
            question.Type = string.IsNullOrWhiteSpace(question.Type) ? questionType : question.Type.ToUpperInvariant();
            question.TopicTags = question.TopicTags.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            question.SourceChunkIds = question.SourceChunkIds
                .Where(item => !string.IsNullOrWhiteSpace(item) && validChunkIdSet.Contains(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (string.Equals(questionType, "FE", StringComparison.OrdinalIgnoreCase))
            {
                question.Options ??= new List<string>();
                question.CorrectAnswer = NormalizeCorrectAnswer(question.Options, question.CorrectAnswer);
                question.Explanation = NormalizeFeExplanation(question.Explanation);
                question.SkeletonCode = null;
                question.SolutionCode = null;
                question.TestCases = null;
            }
            else
            {
                question.Options = null;
                question.CorrectAnswer = null;
                question.Explanation = null;
                question.SkeletonCode = NormalizeCodeFiles(question.SkeletonCode, includeReadOnly: true);
                question.SolutionCode = NormalizeCodeFiles(question.SolutionCode, includeReadOnly: false);
                PreventSolutionLeakIntoSkeleton(question);
                question.TestCases = NormalizeTestCases(question.TestCases);
                SanitizePeQuestion(question, chunks);
            }
        }

        return questions;
    }

    private static int InferPeLanguageId(
        GeneratedQuestionCandidateDto question)
    {
        static int? ResolveFromFileName(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return null;
            }

            var normalized = filename.Trim();

            if (normalized.EndsWith(
                    ".java",
                    StringComparison.OrdinalIgnoreCase))
            {
                return JavaLanguageId;
            }

            if (normalized.EndsWith(
                    ".c",
                    StringComparison.OrdinalIgnoreCase))
            {
                return CLanguageId;
            }

            return null;
        }

        var skeletonLanguage = question.SkeletonCode?
            .Select(file => ResolveFromFileName(file?.Filename))
            .FirstOrDefault(languageId => languageId.HasValue);

        if (skeletonLanguage.HasValue)
        {
            return skeletonLanguage.Value;
        }

        var solutionLanguage = question.SolutionCode?
            .Select(file => ResolveFromFileName(file?.Filename))
            .FirstOrDefault(languageId => languageId.HasValue);

        if (solutionLanguage.HasValue)
        {
            return solutionLanguage.Value;
        }

        return CLanguageId;
    }

    private static List<CodeFile>? NormalizeCodeFiles(List<CodeFile>? files, bool includeReadOnly)
    {
        if (files is null || files.Count == 0)
        {
            return files;
        }

        return files
            .Where(file => file is not null)
            .Select(file => new CodeFile
            {
                Filename = string.IsNullOrWhiteSpace(file.Filename) ? "main.txt" : file.Filename.Trim(),
                Content = NormalizeCodeFileContent(file.Content, file.Filename, includeReadOnly ? file.IsReadOnly : false),
                IsReadOnly = includeReadOnly && file.IsReadOnly
            })
            .ToList();
    }

    private static string NormalizeCodeFileContent(string? content, string? filename, bool isReadOnly)
    {
        var normalized = content ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        if (!isReadOnly && string.Equals(Path.GetExtension(filename ?? string.Empty), ".java", StringComparison.OrdinalIgnoreCase))
        {
            normalized = RemoveJavaConstructorReturnPlaceholders(normalized);
        }

        return normalized;
    }

    private static string NormalizeFeExplanation(string? explanation)
    {
        var normalized = NormalizeWhitespace(explanation ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return TruncateToSentenceCount(normalized, 3);
    }

    private static string TruncateToSentenceCount(string content, int maxSentences)
    {
        if (string.IsNullOrWhiteSpace(content) || maxSentences <= 0)
        {
            return string.Empty;
        }

        var matches = Regex.Matches(content, @"[^.!?]+[.!?]?");
        if (matches.Count <= maxSentences)
        {
            return content.Trim();
        }

        var selected = matches
            .Take(maxSentences)
            .Select(match => match.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join(" ", selected).Trim();
    }

    private static string RemoveJavaConstructorReturnPlaceholders(string content)
    {
        var classNames = Regex.Matches(content, @"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)\b")
            .Select(match => match.Groups[1].Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        if (classNames.Count == 0)
        {
            return content;
        }

        var lines = content.Replace("\r\n", "\n").Split('\n');
        var output = new List<string>(lines.Length);
        var constructorBraceDepth = 0;
        var insideConstructor = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!insideConstructor && IsJavaConstructorStart(trimmed, classNames))
            {
                insideConstructor = true;
                constructorBraceDepth = CountChar(line, '{') - CountChar(line, '}');
                output.Add(line);
                continue;
            }

            if (insideConstructor)
            {
                if (Regex.IsMatch(trimmed, "^return\\s+(null|0|false|true|'\\\\0'|\"\"|new\\s+\\w+[^;]*)\\s*;\\s*$", RegexOptions.IgnoreCase))
                {
                    output.Add($"{GetIndentation(line)}// TODO: implement");
                    constructorBraceDepth += CountChar(line, '{') - CountChar(line, '}');
                    if (constructorBraceDepth <= 0)
                    {
                        insideConstructor = false;
                        constructorBraceDepth = 0;
                    }
                    continue;
                }

                output.Add(line);
                constructorBraceDepth += CountChar(line, '{') - CountChar(line, '}');
                if (constructorBraceDepth <= 0)
                {
                    insideConstructor = false;
                    constructorBraceDepth = 0;
                }
                continue;
            }

            output.Add(line);
        }

        return string.Join("\n", output);
    }

    private static bool IsJavaConstructorStart(string trimmedLine, IReadOnlySet<string> classNames)
    {
        if (string.IsNullOrWhiteSpace(trimmedLine))
        {
            return false;
        }

        var match = Regex.Match(
            trimmedLine,
            @"^(?:public|protected|private)?\s*([A-Za-z_][A-Za-z0-9_]*)\s*\([^)]*\)\s*\{",
            RegexOptions.IgnoreCase);
        return match.Success && classNames.Contains(match.Groups[1].Value);
    }

    private static void PreventSolutionLeakIntoSkeleton(GeneratedQuestionCandidateDto question)
    {
        if (question.SkeletonCode is null || question.SolutionCode is null)
        {
            return;
        }

        if (question.SkeletonCode.Count == 0 || question.SolutionCode.Count == 0)
        {
            return;
        }

        var normalizedSolutionByFile = question.SolutionCode
            .Where(file => file is not null && !string.IsNullOrWhiteSpace(file.Filename))
            .GroupBy(file => file.Filename.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < question.SkeletonCode.Count; index++)
        {
            var skeletonFile = question.SkeletonCode[index];
            if (skeletonFile is null || skeletonFile.IsReadOnly)
            {
                continue;
            }

            var fileName = string.IsNullOrWhiteSpace(skeletonFile.Filename) ? "main.txt" : skeletonFile.Filename.Trim();
            if (!normalizedSolutionByFile.TryGetValue(fileName, out var solutionFile))
            {
                solutionFile = normalizedSolutionByFile.Values.FirstOrDefault();
            }

            if (solutionFile is null)
            {
                continue;
            }

            var skeletonContent = NormalizeCodeTextForComparison(skeletonFile.Content);
            var solutionContent = NormalizeCodeTextForComparison(solutionFile.Content);
            if (string.IsNullOrWhiteSpace(skeletonContent) || string.IsNullOrWhiteSpace(solutionContent))
            {
                continue;
            }

            if (!LooksLikeLeakedSolution(skeletonContent, solutionContent))
            {
                continue;
            }

            skeletonFile.Content = BuildWritableSkeletonStub(solutionFile.Content, fileName);
        }
    }

    private static bool LooksLikeLeakedSolution(string skeletonContent, string solutionContent)
    {
        if (string.Equals(skeletonContent, solutionContent, StringComparison.Ordinal))
        {
            return true;
        }

        if (skeletonContent.Contains(solutionContent, StringComparison.Ordinal) ||
            solutionContent.Contains(skeletonContent, StringComparison.Ordinal))
        {
            return true;
        }

        var overlap = EstimateTokenOverlapRatio(skeletonContent, solutionContent);
        return overlap >= 0.9m;
    }

    private static string NormalizeCodeTextForComparison(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        return Regex.Replace(content, @"\s+", string.Empty);
    }

    private static string BuildWritableSkeletonStub(string? solutionContent, string fileName)
    {
        var content = solutionContent ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            return BuildFallbackSkeleton(fileName);
        }

        var lines = content.Replace("\r\n", "\n").Split('\n');
        var output = new List<string>();
        var skippingMethodBody = false;
        var braceDepth = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine ?? string.Empty;
            var trimmed = line.Trim();

            if (!skippingMethodBody)
            {
                if (IsMethodStartLine(trimmed))
                {
                    output.Add(line);
                    var indent = GetIndentation(line);
                    output.Add($"{indent}    // TODO: implement");

                    var defaultReturn = BuildDefaultReturnStatement(trimmed, indent);
                    if (!string.IsNullOrWhiteSpace(defaultReturn))
                    {
                        output.Add(defaultReturn);
                    }

                    output.Add($"{indent}}}");
                    skippingMethodBody = true;
                    braceDepth = CountChar(line, '{') - CountChar(line, '}');
                    continue;
                }

                output.Add(line);
                continue;
            }

            braceDepth += CountChar(line, '{') - CountChar(line, '}');
            if (braceDepth <= 0)
            {
                skippingMethodBody = false;
                braceDepth = 0;
            }
        }

        var candidate = string.Join("\n", output).Trim();
        return string.IsNullOrWhiteSpace(candidate) ? BuildFallbackSkeleton(fileName) : candidate;
    }

    private static bool IsMethodStartLine(string trimmedLine)
    {
        if (string.IsNullOrWhiteSpace(trimmedLine))
        {
            return false;
        }

        if (!trimmedLine.Contains('(') || !trimmedLine.Contains(')') || !trimmedLine.Contains('{'))
        {
            return false;
        }

        var lowered = trimmedLine.ToLowerInvariant();
        if (lowered.StartsWith("if ") || lowered.StartsWith("if(") ||
            lowered.StartsWith("for ") || lowered.StartsWith("for(") ||
            lowered.StartsWith("while ") || lowered.StartsWith("while(") ||
            lowered.StartsWith("switch ") || lowered.StartsWith("switch(") ||
            lowered.StartsWith("catch ") || lowered.StartsWith("catch(") ||
            lowered.StartsWith("else") || lowered.StartsWith("do ") ||
            lowered.StartsWith("try ") || lowered.StartsWith("class ") ||
            lowered.StartsWith("interface ") || lowered.StartsWith("struct ") ||
            lowered.StartsWith("enum "))
        {
            return false;
        }

        if (LooksLikeConstructorSignature(trimmedLine))
        {
            return false;
        }

        return true;
    }

    private static bool LooksLikeConstructorSignature(string trimmedLine)
    {
        if (string.IsNullOrWhiteSpace(trimmedLine))
        {
            return false;
        }

        var signatureHead = trimmedLine.Split('(')[0].Trim();
        var signatureTokens = signatureHead.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (signatureTokens.Length == 0)
        {
            return false;
        }

        var methodLikeName = signatureTokens[^1];
        if (string.IsNullOrWhiteSpace(methodLikeName))
        {
            return false;
        }

        if (signatureTokens.Length == 1)
        {
            return true;
        }

        var previousToken = signatureTokens[^2];
        var nonReturnTypeMarkers = new[]
        {
            "public",
            "private",
            "protected",
            "internal",
            "static",
            "final",
            "sealed",
            "abstract"
        };

        return nonReturnTypeMarkers.Contains(previousToken, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildDefaultReturnStatement(string trimmedSignature, string indent)
    {
        var signatureHead = trimmedSignature.Split('(')[0].Trim();
        var signatureTokens = signatureHead.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (signatureTokens.Length < 2)
        {
            return string.Empty;
        }

        var returnType = signatureTokens[^2].Trim();
        return returnType switch
        {
            "void" => string.Empty,
            "boolean" or "bool" => $"{indent}    return false;",
            "byte" or "short" or "int" or "long" or "float" or "double" or "char" => $"{indent}    return 0;",
            _ => $"{indent}    return null;"
        };
    }

    private static string GetIndentation(string line)
    {
        var match = Regex.Match(line ?? string.Empty, @"^\s*");
        return match.Success ? match.Value : string.Empty;
    }

    private static int CountChar(string value, char target)
        => string.IsNullOrEmpty(value) ? 0 : value.Count(ch => ch == target);

    private static string BuildFallbackSkeleton(string fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return extension switch
        {
            ".java" => "class Solution {\n    // TODO: implement\n}\n",
            ".c" => "/* TODO: implement */\n",
            _ => "// TODO: implement\n"
        };
    }

    private static List<TestCase>? NormalizeTestCases(List<TestCase>? testCases)
    {
        if (testCases is null || testCases.Count == 0)
        {
            return testCases;
        }

        return testCases
            .Where(test => test is not null)
            .Select(test => new TestCase
            {
                Input = test.Input ?? string.Empty,
                ExpectedOutput = test.ExpectedOutput ?? string.Empty,
                IsHidden = test.IsHidden
            })
            .ToList();
    }

    private static void SanitizePeQuestion(GeneratedQuestionCandidateDto question, IReadOnlyList<RetrievedChunkDto> chunks)
    {
        if (!string.Equals(question.Type, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var citedChunks = chunks
            .Where(chunk => question.SourceChunkIds.Contains(chunk.ChunkId, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var citedText = string.Join("\n", citedChunks.Select(chunk => chunk.ContentText ?? string.Empty));
        var topicText = string.Join(" ", question.TopicTags ?? new List<string>());

        var sourceSupportsGrow = citedText.Contains("grow()", StringComparison.OrdinalIgnoreCase);
        var isPriorityQueueShape =
            topicText.Contains("priority queue", StringComparison.OrdinalIgnoreCase) ||
            citedText.Contains("priority queue", StringComparison.OrdinalIgnoreCase);

        if (sourceSupportsGrow && isPriorityQueueShape)
        {
            question.Description = RemoveUnsupportedFixedCapacityPhrases(question.Description);
            question.Description = RewritePriorityQueueImplementationDetails(question.Description);
        }
    }

    private static string RemoveUnsupportedFixedCapacityPhrases(string? description)
    {
        var result = description ?? string.Empty;
        if (string.IsNullOrWhiteSpace(result))
        {
            return result;
        }

        var patterns = new[]
        {
            @"(?im)^\s*-\s*if the queue is full,.*(?:\r?\n)?",
            @"(?im)^\s*-\s*when the queue is full,.*(?:\r?\n)?",
            @"(?im)^\s*-\s*reject .*full.*(?:\r?\n)?",
            @"(?im)^\s*-\s*do not resize.*(?:\r?\n)?",
            @"(?i)\bwith capacity exactly n\b",
            @"(?i)\buse an array of size n\b",
            @"(?i)\bfixed[- ]capacity\b",
            @"(?i)\bnon-growing\b"
        };

        foreach (var pattern in patterns)
        {
            result = Regex.Replace(result, pattern, string.Empty);
        }

        return Regex.Replace(result, @"\n{3,}", "\n\n").Trim();
    }

    private static string RewritePriorityQueueImplementationDetails(string? description)
    {
        var result = description ?? string.Empty;
        if (string.IsNullOrWhiteSpace(result))
        {
            return result;
        }

        result = Regex.Replace(
            result,
            @"(?is)For this task,\s*the queue must behave like this array-based version:\s*when a value is inserted,.*?largest value first\.",
            "For this task, removing elements from the queue must always return the current largest value first.");

        var removablePatterns = new[]
        {
            @"(?im)^\s*Use this insertion rule:\s*(?:\r?\n)?",
            @"(?im)^\s*-\s*Keep the internal array .*?(?:\r?\n)?",
            @"(?im)^\s*-\s*When inserting .*shift.*(?:\r?\n)?",
            @"(?i)\bkeep the internal array in ascending order\b",
            @"(?i)\bkeep the internal array in descending order\b",
            @"(?i)\bsmaller values are shifted right\b",
            @"(?i)\blarger values are shifted left\b"
        };

        foreach (var pattern in removablePatterns)
        {
            result = Regex.Replace(result, pattern, string.Empty);
        }

        return Regex.Replace(result, @"\n{3,}", "\n\n").Trim();
    }

    // Normalize FE correct answers to option text (not A/B/C/D labels) to keep DB consistent with imported questions.
    // Accepts either labels (A-D) or option text (or "A. option").
    private static List<string> NormalizeCorrectAnswer(IReadOnlyList<string>? options, IReadOnlyList<string>? answers)
    {
        var normalized = new List<string>();
        if (answers is null || answers.Count == 0)
        {
            return normalized;
        }

        if (options is null || options.Count == 0)
        {
            return normalized;
        }

        foreach (var answer in answers)
        {
            if (string.IsNullOrWhiteSpace(answer))
            {
                continue;
            }

            var trimmed = answer.Trim();

            // Label -> option text
            if (Regex.IsMatch(trimmed, "^[A-D]$", RegexOptions.IgnoreCase))
            {
                var optionIndex = char.ToUpperInvariant(trimmed[0]) - 'A';
                if (optionIndex >= 0 && optionIndex < options.Count)
                {
                    normalized.Add(options[optionIndex].Trim());
                }
                continue;
            }

            // "A. option text" -> option text
            var labelPrefixMatch = Regex.Match(trimmed, @"^\s*([A-D])\s*[\.\)]\s*(.+)\s*$", RegexOptions.IgnoreCase);
            if (labelPrefixMatch.Success)
            {
                var optionIndex = char.ToUpperInvariant(labelPrefixMatch.Groups[1].Value[0]) - 'A';
                if (optionIndex >= 0 && optionIndex < options.Count)
                {
                    normalized.Add(options[optionIndex].Trim());
                }
                else
                {
                    normalized.Add(labelPrefixMatch.Groups[2].Value.Trim());
                }
                continue;
            }

            // Option text -> canonicalize to the exact option string if it matches
            for (var i = 0; i < options.Count && i < 4; i++)
            {
                if (string.Equals(options[i]?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    normalized.Add(options[i]!.Trim());
                    break;
                }
            }
        }

        return normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<GeneratedQuestionCandidateDto> ParseQuestions(string content)
    {
        var json = ExtractJson(content);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<GeneratedQuestionCandidateDto>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.EnumerateArray().Select(ParseQuestionElement).ToList();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("questions", out var questions) && questions.ValueKind == JsonValueKind.Array)
                {
                    return questions.EnumerateArray().Select(ParseQuestionElement).ToList();
                }
            }
        }
        catch
        {
        }

        return new List<GeneratedQuestionCandidateDto>();
    }

    private static QuestionReviewDecisionDto ParseReview(string content, string reviewerModel, string mode)
    {
        var json = ExtractJson(content);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new QuestionReviewDecisionDto
            {
                ReviewStatus = "needs_revision",
                Issues = new List<string> { "Reviewer output could not be parsed." },
                Suggestions = new List<string> { "Retry review with clearer JSON-only output." },
                Score = 0.2m,
                SchemaValid = false,
                ContentGrounded = false,
                NeedsRevision = true,
                ReviewerModel = reviewerModel,
                ReviewMode = mode
            };
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new QuestionReviewDecisionDto
            {
                ReviewStatus = ReadString(root, "reviewStatus") ?? "needs_revision",
                Issues = ReadStringArray(root, "issues"),
                Suggestions = ReadStringArray(root, "suggestions"),
                Score = (decimal)(ReadDouble(root, "score") ?? 0.2),
                SchemaValid = ReadBool(root, "schemaValid") ?? false,
                ContentGrounded = ReadBool(root, "contentGrounded") ?? false,
                NeedsRevision = ReadBool(root, "needsRevision") ?? true,
                ReviewerModel = reviewerModel,
                ReviewMode = mode
            };
        }
        catch
        {
            return new QuestionReviewDecisionDto
            {
                ReviewStatus = "needs_revision",
                Issues = new List<string> { "Reviewer output could not be parsed." },
                Suggestions = new List<string> { "Retry review with clearer JSON-only output." },
                Score = 0.2m,
                SchemaValid = false,
                ContentGrounded = false,
                NeedsRevision = true,
                ReviewerModel = reviewerModel,
                ReviewMode = mode
            };
        }
    }

    private static QuestionReviewDecisionDto MergeReview(QuestionReviewDecisionDto rule, QuestionReviewDecisionDto llm)
    {
        var issues = rule.Issues.Concat(llm.Issues).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var accepted = string.Equals(rule.ReviewStatus, "accepted", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(llm.ReviewStatus, "accepted", StringComparison.OrdinalIgnoreCase) &&
                       !rule.NeedsRevision && !llm.NeedsRevision &&
                       rule.SchemaValid && llm.SchemaValid &&
                       rule.ContentGrounded && llm.ContentGrounded &&
                       issues.Count == 0;
        var suggestions = rule.Suggestions
            .Concat(llm.Suggestions)
            .Where(suggestion => accepted || !string.Equals(suggestion, "Rule-based review accepted the generated set.", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new QuestionReviewDecisionDto
        {
            ReviewStatus = accepted ? "accepted" : "needs_revision",
            Issues = issues,
            Suggestions = suggestions,
            Score = Math.Min(rule.Score, llm.Score),
            SchemaValid = rule.SchemaValid && llm.SchemaValid,
            ContentGrounded = rule.ContentGrounded && llm.ContentGrounded,
            NeedsRevision = !accepted || rule.NeedsRevision || llm.NeedsRevision,
            ReviewerModel = llm.ReviewerModel,
            ReviewMode = llm.ReviewMode
        };
    }

    private static bool IsReviewerParseFailure(QuestionReviewDecisionDto review)
        => review.Issues.Count == 1 &&
           string.Equals(review.Issues[0], "Reviewer output could not be parsed.", StringComparison.OrdinalIgnoreCase);

    private async Task<AIJsonArtifactDto> LoadRubricArtifactAsync(string subject, string questionType, CancellationToken cancellationToken)
    {
        var rubricFile = $"ai-rubrics\\{NormalizeSubject(subject)}_{questionType.ToUpperInvariant()}.json";
        var rubric = await _artifactCatalogService.GetArtifactAsync(rubricFile, cancellationToken);
        if (rubric is null)
        {
            throw new InvalidOperationException($"Rubric file '{rubricFile}' is not configured.");
        }

        return rubric;
    }

    private static AIResolvedExecutionOptions ResolveReviewerOptions(GenerationReviewRequestDto request, AIResolvedExecutionOptions generatorResolved)
    {
        return request.Mode.Trim() switch
        {
            "SingleAgent" => generatorResolved,
            "SameModelDualRole" => request.ReviewerOverride is null
                ? new AIResolvedExecutionOptions
                {
                    FeatureName = AIFeatureNames.QuestionReview,
                    Provider = "OpenAI",
                    Model = "gpt-5.4-mini"
                }
                : new AIResolvedExecutionOptions
                {
                    FeatureName = AIFeatureNames.QuestionReview,
                    Provider = request.ReviewerOverride.Provider ?? "OpenAI",
                    Model = request.ReviewerOverride.Model ?? "gpt-5.4-mini",
                    Temperature = request.ReviewerOverride.Temperature,
                    MaxTokens = request.ReviewerOverride.MaxTokens,
                    UsedFrontendOverride = true
                },
            _ => request.ReviewerOverride is null
                ? new AIResolvedExecutionOptions
                {
                    FeatureName = AIFeatureNames.QuestionReview,
                    Provider = "OpenAI",
                    Model = "gpt-5.4-mini"
                }
                : new AIResolvedExecutionOptions
                {
                    FeatureName = AIFeatureNames.QuestionReview,
                    Provider = request.ReviewerOverride.Provider ?? "OpenAI",
                    Model = request.ReviewerOverride.Model ?? "gpt-5.4-mini",
                    Temperature = request.ReviewerOverride.Temperature,
                    MaxTokens = request.ReviewerOverride.MaxTokens,
                    UsedFrontendOverride = true
                }
        };
    }

    private static int ResolveAttemptCount(GenerationReviewRequestDto request)
    {
        if (string.Equals(request.Mode, "SingleAgent", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var configuredAttempts = request.MaxAttempts <= 0 ? 2 : request.MaxAttempts;
        if (request.DifficultyProfile.Count > 1)
        {
            return 1;
        }

        if (string.Equals(request.QuestionType, "FE", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return Math.Clamp(configuredAttempts, 1, 2);
    }

    private static bool IsPeMediumHardRequest(GenerationReviewRequestDto request)
        => string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase) &&
           (string.Equals(request.Difficulty, "Medium", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.Difficulty, "Hard", StringComparison.OrdinalIgnoreCase));

    private static AIResolvedExecutionOptions ApplyGenerationRuntimeCaps(
        GenerationReviewRequestDto request,
        AIResolvedExecutionOptions resolved)
    {
        var capped = CloneResolvedOptions(resolved);
        capped.MaxTokens = CapGenerationMaxTokens(request, resolved.MaxTokens);
        return capped;
    }

    private static AIResolvedExecutionOptions ApplyReviewRuntimeCaps(
        GenerationReviewRequestDto request,
        AIResolvedExecutionOptions resolved)
    {
        var capped = CloneResolvedOptions(resolved);
        capped.MaxTokens = CapReviewMaxTokens(request, resolved.MaxTokens);
        return capped;
    }

    private static AIResolvedExecutionOptions CloneResolvedOptions(AIResolvedExecutionOptions resolved)
        => new()
        {
            FeatureName = resolved.FeatureName,
            Provider = resolved.Provider,
            Model = resolved.Model,
            Temperature = resolved.Temperature,
            MaxTokens = resolved.MaxTokens,
            UsedFrontendOverride = resolved.UsedFrontendOverride,
            FallbackProvider = resolved.FallbackProvider,
            FallbackModel = resolved.FallbackModel
        };

    private static int? CapGenerationMaxTokens(GenerationReviewRequestDto request, int? configuredMaxTokens)
    {
        var requestedCount = Math.Max(1, request.Count);
        if (IsPeMediumHardRequest(request) && request.DifficultyProfile.Count <= 1)
        {
            var peCap = Math.Max(1400, 1500 + ((requestedCount - 1) * 350));
            peCap = Math.Min(peCap, 2200);
            return configuredMaxTokens.HasValue
                ? Math.Min(configuredMaxTokens.Value, peCap)
                : peCap;
        }

        if (string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase) &&
            request.DifficultyProfile.Count <= 1)
        {
            return configuredMaxTokens;
        }

        var cap = string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase)
            ? 1400 + ((requestedCount - 1) * 500)
            : 650 + (requestedCount * 260);

        if (request.DifficultyProfile.Count > 1)
        {
            cap = string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase)
                ? Math.Min(cap, 2200)
                : Math.Min(cap, 2600);
        }

        cap = Math.Max(
            string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase) ? 1200 : 700,
            cap);

        return configuredMaxTokens.HasValue
            ? Math.Min(configuredMaxTokens.Value, cap)
            : cap;
    }

    private static int? CapReviewMaxTokens(GenerationReviewRequestDto request, int? configuredMaxTokens)
    {
        var requestedCount = Math.Max(1, request.Count);
        if (IsPeMediumHardRequest(request) && request.DifficultyProfile.Count <= 1)
        {
            var peCap = Math.Max(420, 520 + (requestedCount * 130));
            peCap = Math.Min(peCap, 1100);
            return configuredMaxTokens.HasValue
                ? Math.Min(configuredMaxTokens.Value, peCap)
                : peCap;
        }

        if (string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase) &&
            request.DifficultyProfile.Count <= 1)
        {
            return configuredMaxTokens;
        }

        var cap = string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase)
            ? 500 + (requestedCount * 140)
            : 360 + (requestedCount * 90);

        if (request.DifficultyProfile.Count > 1)
        {
            cap = string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase)
                ? Math.Min(cap, 1400)
                : Math.Min(cap, 1200);
        }

        cap = Math.Max(320, cap);
        return configuredMaxTokens.HasValue
            ? Math.Min(configuredMaxTokens.Value, cap)
            : cap;
    }

    private static void NormalizeRequest(GenerationReviewRequestDto request)
    {
        request.Subject = NormalizeSubject(request.Subject);
        request.QuestionType = request.QuestionType.Trim().ToUpperInvariant();
        request.DifficultyProfile = NormalizeDifficultyProfile(request.DifficultyProfile);
        request.Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty.Trim();
        request.DifficultyMode = request.DifficultyProfile.Count > 1 ? "mixed" : "single";
        request.Mode = string.IsNullOrWhiteSpace(request.Mode) ? "SameModelDualRole" : request.Mode.Trim();
        if (request.DifficultyProfile.Count == 1)
        {
            request.Difficulty = request.DifficultyProfile[0].Difficulty;
        }

        request.Count = request.DifficultyProfile.Count > 0
            ? Math.Max(1, request.DifficultyProfile.Sum(item => item.Count))
            : Math.Max(1, request.Count);
        request.MaxAttempts = Math.Max(1, request.MaxAttempts);
        request.CourseId = string.IsNullOrWhiteSpace(request.CourseId) ? null : request.CourseId.Trim();
        request.DocumentId = string.IsNullOrWhiteSpace(request.DocumentId) ? null : request.DocumentId.Trim();

        if (request.Retrieval is not null)
        {
            request.Retrieval.CourseId = string.IsNullOrWhiteSpace(request.Retrieval.CourseId) ? null : request.Retrieval.CourseId.Trim();
            request.Retrieval.DocumentId = string.IsNullOrWhiteSpace(request.Retrieval.DocumentId) ? null : request.Retrieval.DocumentId.Trim();
            request.Retrieval.ChapterKey = string.IsNullOrWhiteSpace(request.Retrieval.ChapterKey) ? null : request.Retrieval.ChapterKey.Trim();
            request.Retrieval.ChapterKeys = ResolveRequestedChapterKeys(request.Retrieval.ChapterKey, request.Retrieval.ChapterKeys);
            request.Retrieval.ChapterKey = request.Retrieval.ChapterKeys.FirstOrDefault();
            request.Retrieval.Subject = NormalizeSubject(request.Retrieval.Subject);
            request.Retrieval.QuestionType = request.Retrieval.QuestionType.Trim().ToUpperInvariant();
            request.Retrieval.Difficulty = string.IsNullOrWhiteSpace(request.Retrieval.Difficulty)
                ? (request.DifficultyProfile.Count > 1 ? "Mixed" : request.Difficulty)
                : request.Retrieval.Difficulty.Trim();
            request.Retrieval.RequestedQuestionCount = request.Count;
            request.Retrieval.TargetTopics = request.Retrieval.TargetTopics?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }
    }

    private static void ValidateRequest(GenerationReviewRequestDto request)
    {
        if (request.DifficultyProfile.Count > MaxDifficultyBuckets)
        {
            throw new InvalidOperationException($"Generation supports at most {MaxDifficultyBuckets} difficulty buckets per request.");
        }

        if (request.DifficultyProfile.Count > 0)
        {
            var distinctDifficultyCount = request.DifficultyProfile
                .Select(item => item.Difficulty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (distinctDifficultyCount != request.DifficultyProfile.Count)
            {
                throw new InvalidOperationException("difficultyProfile must not contain duplicate difficulties.");
            }
        }

        if (request.DifficultyProfile.Count > 1)
        {
            ValidateMixedCountLimits(request.QuestionType, request.DifficultyProfile, request.Count);
        }

        var hasContextPack = !string.IsNullOrWhiteSpace(request.ContextPackId);
        var hasRetrieval = request.Retrieval is not null;
        var hasChunks = request.Chunks is { Count: > 0 };

        var sourceCount = (hasContextPack ? 1 : 0) + (hasRetrieval ? 1 : 0) + (hasChunks ? 1 : 0);
        if (sourceCount == 0)
        {
            throw new InvalidOperationException("Generation request must provide exactly one input source: contextPackId, retrieval, or chunks.");
        }

        if (sourceCount > 1)
        {
            throw new InvalidOperationException("Generation request must not provide multiple input sources at the same time.");
        }

        if (!hasRetrieval)
        {
            return;
        }

        var retrieval = request.Retrieval!;
        retrieval.SourceScope = string.IsNullOrWhiteSpace(retrieval.SourceScope) ? "SYSTEM" : retrieval.SourceScope.Trim().ToUpperInvariant();

        if (retrieval.TargetTopics is null || retrieval.TargetTopics.Count == 0)
        {
            throw new InvalidOperationException("Retrieval-based generation requires at least one target topic.");
        }

        if (!retrieval.AllowExtendedScope && retrieval.ChapterKeys.Count > 3)
        {
            throw new InvalidOperationException("Retrieval-based generation supports at most 3 chapters per request in this phase.");
        }

        if (!string.IsNullOrWhiteSpace(request.CourseId) &&
            !string.Equals(request.CourseId, retrieval.CourseId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Generation request courseId does not match retrieval.courseId.");
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentId) &&
            !string.Equals(request.DocumentId, retrieval.DocumentId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Generation request documentId does not match retrieval.documentId.");
        }

        if (!string.IsNullOrWhiteSpace(retrieval.Subject) &&
            !string.Equals(request.Subject, retrieval.Subject, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Generation request subject does not match retrieval.subject.");
        }

        if (!string.IsNullOrWhiteSpace(retrieval.QuestionType) &&
            !string.Equals(request.QuestionType, retrieval.QuestionType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Generation request questionType does not match retrieval.questionType.");
        }

        if (string.Equals(retrieval.SourceScope, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(retrieval.CourseId))
            {
                throw new InvalidOperationException("SYSTEM generation requires courseId in retrieval request.");
            }

            if (string.IsNullOrWhiteSpace(retrieval.DocumentId))
            {
                throw new InvalidOperationException("SYSTEM generation requires documentId in retrieval request.");
            }
        }
        else if (string.Equals(retrieval.SourceScope, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(retrieval.DocumentId))
            {
                throw new InvalidOperationException("BYOS generation requires documentId in retrieval request.");
            }

            if (!string.IsNullOrWhiteSpace(retrieval.CourseId) &&
                !string.IsNullOrWhiteSpace(request.CourseId) &&
                !string.Equals(request.CourseId, retrieval.CourseId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("BYOS generation must not mix a different courseId with retrieval.documentId.");
            }
        }
        else
        {
            throw new InvalidOperationException("Retrieval-based generation currently supports only SYSTEM or BYOS sourceScope.");
        }
    }

    private static string NormalizeSubject(string subject)
    {
        return AISubjectDomainMapper.NormalizeSubjectOrCourseCode(subject);
    }

    private static List<string> ResolveRequestedChapterKeys(string? chapterKey, IReadOnlyList<string>? chapterKeys)
        => new[] { chapterKey }
            .Concat(chapterKeys ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string? ResolveChapterTitle(IReadOnlyList<RetrievedChunkDto> chunks, string? chapterKey)
    {
        if (string.IsNullOrWhiteSpace(chapterKey))
        {
            return null;
        }

        return chunks
            .FirstOrDefault(chunk => string.Equals(chunk.ChapterKey, chapterKey, StringComparison.OrdinalIgnoreCase))
            ?.SectionTitle;
    }

    private static string ResolveQuestionSourceScope(GenerationReviewRequestDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.Retrieval?.SourceScope))
        {
            return request.Retrieval.SourceScope.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentId) && string.IsNullOrWhiteSpace(request.CourseId))
        {
            return "BYOS";
        }

        return "SYSTEM";
    }

    private static string BuildSubjectFocus(string subject, string questionType, QuestionGenerationReviewRuntimePolicy? policy = null)
    {
        var normalizedSubject = NormalizeSubject(subject);
        var normalizedQuestionType = questionType.ToUpperInvariant();
        var configuredFocus = (policy?.SubjectFocusRules ?? new List<SubjectFocusRulePolicy>())
            .FirstOrDefault(rule => MatchesReviewRule(rule.Subject, rule.QuestionType, normalizedSubject, normalizedQuestionType));
        if (!string.IsNullOrWhiteSpace(configuredFocus?.Message))
        {
            return configuredFocus.Message!;
        }

        return (normalizedSubject, normalizedQuestionType) switch
        {
            ("C", "FE") => "Introductory C concepts for FPT students: variables, operators, conditions, loops, arrays, functions, pointers at beginner scope.",
            ("C", "PE") => "Introductory C practical tasks for FPT students with clear input/output and beginner-friendly coding requirements.",
            ("JAVA_OOP", "FE") => "Java OOP concepts for FPT students: class, object, constructor, encapsulation, inheritance, polymorphism, interface.",
            ("JAVA_OOP", "PE") => "Java OOP practical coding tasks for FPT students with class design, method behavior, and simple object interactions.",
            ("DSA_JAVA", "FE") => "Data structures and algorithms in Java for FPT students: arrays, linked lists, stacks, queues, trees, graphs, sorting, searching.",
            ("DSA_JAVA", "PE") => "Java DSA practical tasks for FPT students focusing on algorithmic behavior, small implementations, and edge-case handling.",
            _ => "FPT University programming-course assessment generation."
        };
    }

    private static string BuildPeRetryGuidance(
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto>? chunks = null,
        QuestionGenerationReviewRuntimePolicy? policy = null)
    {
        if (!string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var subject = NormalizeSubject(request.Subject);
        var topics = (request.Retrieval?.TargetTopics ?? new List<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToLowerInvariant())
            .ToList();

        var hints = new List<string>();
        foreach (var rule in policy?.RetryGuidanceRules ?? new List<RetryGuidanceRulePolicy>())
        {
            if (!MatchesReviewRule(rule.Subject, rule.QuestionType, subject, request.QuestionType))
            {
                continue;
            }

            if (!MatchesTopicPatterns(topics, rule.TopicPatterns, ParseTopicMatchMode(rule.TopicMatchMode)))
            {
                continue;
            }

            hints.AddRange(rule.Messages.Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        if (chunks is { Count: > 0 })
        {
            var chunkText = string.Join("\n", chunks.Select(item => item.ContentText ?? string.Empty)).ToLowerInvariant();
            foreach (var rule in policy?.SourceRiskGuidanceRules ?? new List<SourceRiskGuidanceRulePolicy>())
            {
                if (!MatchesReviewRule(rule.Subject, rule.QuestionType, subject, request.QuestionType))
                {
                    continue;
                }

                if (!MatchesTopicPatterns(topics, rule.TopicPatterns, ParseTopicMatchMode(rule.TopicMatchMode)))
                {
                    continue;
                }

                var matched = rule.ChunkTermGroups.Any(group => group.Count > 0 &&
                                                                group.All(term => chunkText.Contains(term, StringComparison.OrdinalIgnoreCase)));
                if (!matched)
                {
                    continue;
                }

                hints.AddRange(rule.Messages.Where(item => !string.IsNullOrWhiteSpace(item)));
            }
        }

        return string.Join("\n", hints.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildRevisionFeedback(
        GenerationReviewRequestDto request,
        QuestionReviewDecisionDto latestReview,
        int acceptedCount,
        int remainingCount,
        IReadOnlyList<RetrievedChunkDto>? chunks,
        QuestionGenerationReviewRuntimePolicy? policy)
    {
        var remaining = Math.Max(0, remainingCount);
        var items = latestReview.Issues
            .Concat(latestReview.Suggestions)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(NormalizeRetryFeedbackLine)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        items.Add($"Accepted {acceptedCount} question(s) so far. Generate exactly {remaining} additional new question(s) that do not repeat prior accepted or rejected shapes.");

        var peGuidance = BuildPeRetryGuidance(request, chunks, policy);
        if (!string.IsNullOrWhiteSpace(peGuidance))
        {
            items.AddRange(
                peGuidance
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(NormalizeRetryFeedbackLine)
                    .Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        var budgetedItems = (string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase)
                ? items.Take(MaxPeRetryFeedbackItems)
                : items)
            .ToList();
        var text = string.Join("\n", budgetedItems);

        if (string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase) &&
            text.Length > MaxPeRetryFeedbackCharacters)
        {
            text = $"{text[..MaxPeRetryFeedbackCharacters].TrimEnd()}...";
        }

        return text;
    }

    private static string NormalizeRetryFeedbackLine(string message)
    {
        var normalized = NormalizeWhitespace(message ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (normalized.Length > 180)
        {
            normalized = $"{normalized[..180].TrimEnd()}...";
        }

        return normalized;
    }

    private static string ExtractJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var startArray = content.IndexOf('[');
        var startObject = content.IndexOf('{');
        if (startArray >= 0 && (startArray < startObject || startObject < 0))
        {
            var endArray = content.LastIndexOf(']');
            return endArray >= startArray ? content[startArray..(endArray + 1)] : string.Empty;
        }

        if (startObject >= 0)
        {
            var endObject = content.LastIndexOf('}');
            return endObject >= startObject ? content[startObject..(endObject + 1)] : string.Empty;
        }

        return string.Empty;
    }

    private static GeneratedQuestionCandidateDto ParseQuestionElement(JsonElement element)
    {
        return new GeneratedQuestionCandidateDto
        {
            Type = ReadString(element, "type") ?? string.Empty,
            TopicTags = ReadStringArray(element, "topic_tags", "topicTags"),
            Difficulty = ReadString(element, "difficulty") ?? string.Empty,
            Title = ReadString(element, "title") ?? string.Empty,
            Description = ReadString(element, "description") ?? string.Empty,
            SourceChunkIds = ReadStringArray(element, "source_chunk_ids", "sourceChunkIds"),
            SkeletonCode = ReadCodeFiles(element, "skeleton_code", "skeletonCode", includeReadOnly: true),
            SolutionCode = ReadCodeFiles(element, "solution_code", "solutionCode", includeReadOnly: false),
            TestCases = ReadTestCases(element, "test_cases", "testCases"),
            Options = ReadStringArray(element, "options"),
            CorrectAnswer = ReadStringArray(element, "correct_answer", "correctAnswer"),
            Explanation = ReadString(element, "explanation")
        };
    }

    private static string? ReadString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }
        return null;
    }

    private static bool? ReadBool(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.True) return true;
            if (property.ValueKind == JsonValueKind.False) return false;
        }
        return null;
    }

    private static double? ReadDouble(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value))
            {
                return value;
            }
        }
        return null;
    }

    private static List<string> ReadStringArray(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.Array)
            {
                return property.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .ToList();
            }
        }

        return new List<string>();
    }

    private static List<CodeFile>? ReadCodeFiles(JsonElement element, string snakeKey, string camelKey, bool includeReadOnly)
    {
        if (!element.TryGetProperty(snakeKey, out var property) && !element.TryGetProperty(camelKey, out property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<CodeFile>();
        foreach (var item in property.EnumerateArray())
        {
            result.Add(new CodeFile
            {
                Filename = ReadString(item, "filename", "fileName") ?? "main.txt",
                Content = ReadString(item, "content") ?? string.Empty,
                IsReadOnly = includeReadOnly && (ReadBool(item, "is_readonly", "isReadOnly") ?? false)
            });
        }

        return result;
    }

    private static List<TestCase>? ReadTestCases(JsonElement element, string snakeKey, string camelKey)
    {
        if (!element.TryGetProperty(snakeKey, out var property) && !element.TryGetProperty(camelKey, out property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<TestCase>();
        foreach (var item in property.EnumerateArray())
        {
            result.Add(new TestCase
            {
                Input = ReadString(item, "input") ?? string.Empty,
                ExpectedOutput = ReadString(item, "expected_output", "expectedOutput") ?? string.Empty,
                IsHidden = ReadBool(item, "is_hidden", "isHidden") ?? false
            });
        }

        return result;
    }

    private static bool ContainsMetaSourceWording(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lowered = text.ToLowerInvariant();
        return lowered.Contains("according to the slide", StringComparison.Ordinal) ||
               lowered.Contains("according to the page", StringComparison.Ordinal) ||
               lowered.Contains("from the document", StringComparison.Ordinal) ||
               lowered.Contains("in the chunk", StringComparison.Ordinal) ||
               lowered.Contains("in the text above", StringComparison.Ordinal);
    }

    private static bool CanAttemptRepair(string questionType, IReadOnlyList<RetrievedChunkDto> chunks)
    {
        if (!string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return chunks.Any(chunk => !IsLowInformationChunk(chunk));
    }

    private static List<string> GetGroundingQualityIssues(
        GeneratedQuestionCandidateDto question,
        IReadOnlyList<RetrievedChunkDto> citedChunks)
    {
        var issues = new List<string>();
        if (citedChunks.Count == 0)
        {
            return issues;
        }

        var informativeChunks = citedChunks
            .Where(chunk => !IsLowInformationChunk(chunk))
            .ToList();
        if (informativeChunks.Count == 0)
        {
            issues.Add("all cited chunks are low-information headers or meta content, so the question is not grounded strongly enough.");
            return issues;
        }

        var citedChunkText = string.Join(
            "\n",
            informativeChunks
                .Select(chunk => chunk.ContentText ?? string.Empty)
                .Where(text => !string.IsNullOrWhiteSpace(text)));
        var groundingOverlap = EstimateTokenOverlapRatio(
            $"{question.Title} {question.Description} {string.Join(" ", question.TopicTags)}",
            citedChunkText);
        if (groundingOverlap < 0.08m)
        {
            issues.Add("cited chunks do not provide enough lexical grounding for the generated question content.");
        }

        return issues;
    }

    private static List<string> GetPeConsistencyIssues(
        GeneratedQuestionCandidateDto question,
        IReadOnlyList<RetrievedChunkDto> citedChunks)
    {
        var issues = new List<string>();
        if (!string.Equals(question.Type, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return issues;
        }

        var description = $"{question.Title} {question.Description}".Trim();
        var solution = string.Join("\n", question.SolutionCode?.Select(file => file.Content) ?? Array.Empty<string>());
        var skeleton = string.Join("\n", question.SkeletonCode?.Select(file => file.Content) ?? Array.Empty<string>());
        var testInputs = string.Join("\n", question.TestCases?.Select(test => test.Input ?? string.Empty) ?? Array.Empty<string>());
        var testOutputs = string.Join("\n", question.TestCases?.Select(test => test.ExpectedOutput ?? string.Empty) ?? Array.Empty<string>());
        var codeAndTests = $"{skeleton}\n{solution}\n{testInputs}\n{testOutputs}";
        var informativeChunkText = string.Join(
            "\n",
            citedChunks
                .Where(chunk => !IsLowInformationChunk(chunk))
                .Select(chunk => chunk.ContentText ?? string.Empty));

        var descriptionTokens = TokenizeForComparison(description);
        var codeTokens = TokenizeForComparison(codeAndTests);
        var sourceTokens = TokenizeForComparison(informativeChunkText);

        if (descriptionTokens.Count >= 4)
        {
            var descriptionToCodeOverlap = descriptionTokens.Intersect(codeTokens, StringComparer.OrdinalIgnoreCase).Count();
            if (descriptionToCodeOverlap == 0)
            {
                issues.Add("problem statement does not align with the generated code or test artifacts.");
            }

            var descriptionToSourceOverlap = descriptionTokens.Intersect(sourceTokens, StringComparer.OrdinalIgnoreCase).Count();
            if (sourceTokens.Count >= 6 && descriptionToSourceOverlap == 0)
            {
                issues.Add("problem statement introduces a scenario that is not supported by the cited source chunks.");
            }
        }

        var normalizedDescription = NormalizeWhitespace(description);
        var normalizedSolution = NormalizeWhitespace(solution);
        var normalizedTests = NormalizeWhitespace($"{testInputs}\n{testOutputs}");

        if (RequiresThresholdLogic(normalizedDescription) &&
            !Regex.IsMatch(solution, @"(<=|>=|==|!=|<|>)", RegexOptions.IgnoreCase))
        {
            issues.Add("problem statement defines threshold or comparison behavior, but the solution does not show matching comparison logic.");
        }

        if (RequiresAlternatingBehavior(normalizedDescription) &&
            !Regex.IsMatch(solution, @"(%\s*2\b|cycle|turn|round|count\+\+|count\s*=)", RegexOptions.IgnoreCase))
        {
            issues.Add("problem statement requires alternating or cycle-based behavior, but the solution does not show matching state or parity logic.");
        }

        if (RequiresClampOrUpperBound(normalizedDescription) &&
            !Regex.IsMatch(solution, @"\bMath\.min\b|if\s*\([^)]*(<=|>=|<|>)|while\s*\(", RegexOptions.IgnoreCase))
        {
            issues.Add("problem statement requires an upper-bound or do-not-exceed rule, but the solution does not show any clamp or guarding logic.");
        }

        if (RequiresPositiveInputGuard(normalizedDescription) &&
            !Regex.IsMatch(solution, @"if\s*\([^)]*(> 0|>= 0|<= 0|< 0)", RegexOptions.IgnoreCase))
        {
            issues.Add("problem statement requires positive or non-positive input handling, but the solution does not show matching guard logic.");
        }

        if (RequiresFormattedProgressOrSummary(normalizedDescription) &&
            !Regex.IsMatch(solution, @"return\s+.*(""/""|PASS|FAIL|NORMAL|LOW|FULL|COLD|HOT|:)", RegexOptions.IgnoreCase))
        {
            issues.Add("problem statement requires a learner-facing formatted summary or progress string, but the solution does not appear to build that output.");
        }

        var testCases = question.TestCases ?? new List<TestCase>();
        if (testCases.Count > 0 &&
            testCases.All(test => string.IsNullOrWhiteSpace(test.Input) && string.IsNullOrWhiteSpace(test.ExpectedOutput)))
        {
            issues.Add("test cases are effectively empty and do not validate the proposed task.");
        }

        if (RequiresThresholdLogic(normalizedDescription) && !TestsCoverComparisonDiversity(testCases))
        {
            issues.Add("test cases do not appear to cover the comparison or boundary behavior described in the problem statement.");
        }

        if (RequiresAlternatingBehavior(normalizedDescription) &&
            !Regex.IsMatch(normalizedTests, @"\b(2|3|4|5|6|7|8|9)\b", RegexOptions.IgnoreCase))
        {
            issues.Add("test cases are too weak to demonstrate the alternating or cycle-based behavior required by the problem statement.");
        }

        return issues;
    }

    private static List<string> GetFeQualityIssues(
        GeneratedQuestionCandidateDto question,
        IReadOnlyList<RetrievedChunkDto> citedChunks,
        QuestionGenerationReviewRuntimePolicy? policy)
    {
        var issues = new List<string>();
        if (!string.Equals(question.Type, "FE", StringComparison.OrdinalIgnoreCase))
        {
            return issues;
        }

        var feRules = policy?.FeRules;
        var options = question.Options ?? new List<string>();
        var distinctOptions = options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => NormalizeWhitespace(option))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctOptions.Count > 0 && distinctOptions.Count != options.Count)
        {
            issues.Add(GetConfiguredIssueMessage(feRules?.IssueMessages, "duplicate_options", "FE options contain duplicates or near-duplicates."));
        }

        var minimumOptionLength = feRules?.MinimumOptionLength is > 0 ? feRules.MinimumOptionLength : 3;
        if (options.Any(option => NormalizeWhitespace(option).Length < minimumOptionLength))
        {
            issues.Add($"FE options contain a choice shorter than the minimum quality length ({minimumOptionLength}).");
        }

        var forbiddenOptionTerms = (feRules?.ForbiddenOptionTerms ?? new List<string> { "all of the above", "none of the above" })
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .ToList();
        if (forbiddenOptionTerms.Any(term => options.Any(option => option.Contains(term, StringComparison.OrdinalIgnoreCase))))
        {
            issues.Add(GetConfiguredIssueMessage(feRules?.IssueMessages, "forbidden_meta_option", "FE uses a forbidden meta option pattern such as 'All of the above' or 'None of the above'."));
        }

        var stem = NormalizeWhitespace(question.Description ?? question.Title ?? string.Empty);
        var minimumStemLength = feRules?.MinimumStemLength is > 0 ? feRules.MinimumStemLength : 18;
        if (stem.Length < minimumStemLength)
        {
            issues.Add($"FE stem is too short to provide useful academic review value (minimum {minimumStemLength} characters).");
        }

        if (feRules?.RequireQuestionMarkInStem ?? true)
        {
            if (!stem.Contains('?', StringComparison.Ordinal))
            {
                issues.Add(GetConfiguredIssueMessage(feRules?.IssueMessages, "missing_question_mark", "FE stem should read as a clear question."));
            }
        }

        var informativeChunkText = string.Join(
            "\n",
            citedChunks
                .Where(chunk => !IsLowInformationChunk(chunk))
                .Select(chunk => chunk.ContentText ?? string.Empty));
        if ((feRules?.RequireExplanation ?? false) && string.IsNullOrWhiteSpace(question.Explanation))
        {
            issues.Add(GetConfiguredIssueMessage(feRules?.IssueMessages, "missing_explanation", "FE explanation is required by policy."));
        }

        var correctLabels = (question.CorrectAnswer ?? new List<string>())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim().ToUpperInvariant())
            .ToList();
        var normalizedExplanation = NormalizeWhitespace(question.Explanation ?? string.Empty);
        var maxExplanationSentences = feRules?.MaximumExplanationSentenceCount is > 0
            ? feRules.MaximumExplanationSentenceCount
            : 3;
        var minimumExplanationSourceOverlap = feRules?.MinimumExplanationSourceOverlapRatio is > 0m and <= 1m
            ? feRules.MinimumExplanationSourceOverlapRatio
            : 0.08m;
        if (correctLabels.Count > 0 && !string.IsNullOrWhiteSpace(normalizedExplanation))
        {
            var optionMap = BuildOptionLabelMap(options);
            var correctOptionTexts = correctLabels
                .Where(optionMap.ContainsKey)
                .Select(label => optionMap[label])
                .ToList();
            var distractorTexts = optionMap
                .Where(item => !correctLabels.Contains(item.Key, StringComparer.OrdinalIgnoreCase))
                .Select(item => item.Value)
                .ToList();

            if (correctOptionTexts.Count > 0)
            {
                var bestCorrectOverlap = correctOptionTexts.Max(text => EstimateTokenOverlapRatio(normalizedExplanation, text));
                if (bestCorrectOverlap < 0.10m &&
                    !correctLabels.Any(label => normalizedExplanation.Contains($"answer is {label}", StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add("FE explanation does not clearly justify the selected correct answer.");
                }
            }

            if (distinctOptions.Count > 0)
            {
                var explanationToSourceOverlap = EstimateTokenOverlapRatio(normalizedExplanation, informativeChunkText);
                if (explanationToSourceOverlap < minimumExplanationSourceOverlap)
                {
                    issues.Add("FE explanation is too weakly grounded in the cited source content.");
                }
            }

            if (correctOptionTexts.Count > 0 && distractorTexts.Count > 0)
            {
                var bestCorrectOverlap = correctOptionTexts.Max(text => EstimateTokenOverlapRatio(normalizedExplanation, text));
                var bestDistractorOverlap = distractorTexts.Max(text => EstimateTokenOverlapRatio(normalizedExplanation, text));
                if (bestDistractorOverlap > bestCorrectOverlap + 0.12m)
                {
                    issues.Add("FE explanation aligns more strongly with a distractor than with the selected correct answer.");
                }
            }

            if (CountSentences(normalizedExplanation) > maxExplanationSentences)
            {
                issues.Add($"FE explanation is too long for a grounded review answer and should stay within {maxExplanationSentences} sentence(s).");
            }
        }
        var optionOverlapCount = distinctOptions.Count(option =>
            EstimateTokenOverlapRatio(option, informativeChunkText) >= (feRules?.MinimumGroundedOptionOverlapRatio is > 0m and <= 1m ? feRules.MinimumGroundedOptionOverlapRatio : 0.08m));
        var minimumGroundedOptions = feRules?.MinimumGroundedOptionCount is > 0 ? feRules.MinimumGroundedOptionCount : 2;
        if (distinctOptions.Count > 0 && optionOverlapCount < Math.Min(minimumGroundedOptions, distinctOptions.Count))
        {
            issues.Add($"FE distractor set is too weakly grounded in the cited content (needs at least {minimumGroundedOptions} content-connected options).");
        }

        return issues.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool RequiresThresholdLogic(string description)
        => Regex.IsMatch(description, @"\b(greater than|less than|at least|at most|greater than or equal|less than or equal|>=|<=|up to)\b", RegexOptions.IgnoreCase);

    private static bool RequiresAlternatingBehavior(string description)
        => Regex.IsMatch(description, @"\b(1st|2nd|3rd|4th|5th|odd|even|alternat|every other|cycle)\b", RegexOptions.IgnoreCase);

    private static bool RequiresClampOrUpperBound(string description)
        => Regex.IsMatch(description, @"\b(must not exceed|do not let|cannot exceed|never process more than|up to capacity|at most)\b", RegexOptions.IgnoreCase);

    private static bool RequiresPositiveInputGuard(string description)
        => Regex.IsMatch(description, @"\b(if .*positive|if .*not positive|otherwise, do nothing|if .*at least|if .*less than)\b", RegexOptions.IgnoreCase);

    private static bool RequiresFormattedProgressOrSummary(string description)
        => Regex.IsMatch(description, @"\b(return the progress|print the result in this format|summary|getprogress|getsummary|format current/target|format current/required)\b", RegexOptions.IgnoreCase);

    private static int CountSentences(string content)
        => Regex.Split(content ?? string.Empty, @"(?<=[.!?])\s+")
            .Count(part => !string.IsNullOrWhiteSpace(part));

    private static bool TestsCoverComparisonDiversity(IReadOnlyList<TestCase> testCases)
    {
        if (testCases.Count < 2)
        {
            return false;
        }

        var outputs = testCases
            .Select(test => NormalizeWhitespace(test.ExpectedOutput ?? string.Empty))
            .Where(output => !string.IsNullOrWhiteSpace(output))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return outputs.Count >= 2;
    }

    private static Dictionary<string, string> BuildOptionLabelMap(IReadOnlyList<string> options)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < options.Count; index++)
        {
            var label = ((char)('A' + index)).ToString();
            var raw = NormalizeWhitespace(options[index] ?? string.Empty);
            if (raw.StartsWith($"{label}.", StringComparison.OrdinalIgnoreCase))
            {
                raw = NormalizeWhitespace(raw[(label.Length + 1)..]);
            }

            map[label] = raw;
        }

        return map;
    }

    private static bool IsLowInformationChunk(RetrievedChunkDto chunk)
    {
        var text = (chunk.ContentText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var normalized = Regex.Replace(text, @"\s+", " ").Trim();
        if (Regex.IsMatch(normalized, @"^#+\s*parsed document\s*:", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^(parsed document|document title|slide title|page title)\b", RegexOptions.IgnoreCase))
        {
            return true;
        }

        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        if (lines.Count == 1 && IsBinaryLikeOrNoisyHeading(lines[0]))
        {
            return true;
        }

        var tokens = TokenizeForComparison(text);
        if (tokens.Count <= 3)
        {
            return true;
        }

        var metaOnlyPatterns = new[]
        {
            "parsed document",
            "document:",
            "slide:",
            "page:",
            "section:",
            "chapter:"
        };
        if (metaOnlyPatterns.Any(pattern => normalized.Contains(pattern, StringComparison.OrdinalIgnoreCase)) &&
            tokens.Count <= 6)
        {
            return true;
        }

        return false;
    }

    private static bool IsBinaryLikeOrNoisyHeading(string text)
    {
        var normalized = Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^[#\-\d\s\.]+$"))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^(?:[01]{2,}[ \-]*)+$"))
        {
            return true;
        }

        var alphaNumericTokens = Regex.Matches(normalized, @"[A-Za-z0-9]+")
            .Select(match => match.Value)
            .ToList();
        if (alphaNumericTokens.Count > 0 &&
            alphaNumericTokens.All(token => Regex.IsMatch(token, @"^[01]{2,}$")))
        {
            return true;
        }

        return false;
    }

    private static string NormalizeWhitespace(string content)
        => Regex.Replace(content ?? string.Empty, @"\s+", " ").Trim();

    private static List<string> GetLowValuePeIssues(
        GeneratedQuestionCandidateDto question,
        IReadOnlyDictionary<string, RetrievedChunkDto> chunkMap,
        QuestionGenerationReviewRuntimePolicy? policy)
    {
        var issues = new List<string>();
        if (!string.Equals(question.Type, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return issues;
        }

        var solution = string.Join("\n", question.SolutionCode?.Select(file => file.Content) ?? Array.Empty<string>());
        var skeleton = string.Join("\n", question.SkeletonCode?.Select(file => file.Content) ?? Array.Empty<string>());
        var description = question.Description ?? string.Empty;
        var combinedCode = $"{skeleton}\n{solution}";
        var peRules = policy?.PeLowValueRules;
        var methodNames = Regex.Matches(combinedCode, @"@Override\s+public\s+\w+(?:<[^>]+>)?\s+(\w+)\s*\(", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var classCount = Regex.Matches(combinedCode, @"\bclass\s+\w+\b").Count;
        var overrideCount = Regex.Matches(combinedCode, @"@Override\b", RegexOptions.IgnoreCase).Count;
        var weakOverrideMethodNames = GetConfiguredTerms(
            peRules?.WeakOverrideMethodNames,
            "toString", "getMessage", "getLabel", "getInfo", "describe");
        var hasWeakOverrideMethod = methodNames.Any(name => weakOverrideMethodNames.Contains(name, StringComparer.OrdinalIgnoreCase));
        var returnsMostlyFixedStrings = Regex.Matches(solution, "return\\s+\"[^\"]*\"\\s*(\\+\\s*\\w+\\s*){0,3};", RegexOptions.IgnoreCase).Count >= 2;
        var hasFormattingOnlyOutputs = (question.TestCases ?? new List<TestCase>())
            .Where(test => !string.IsNullOrWhiteSpace(test.ExpectedOutput))
            .All(test => Regex.IsMatch(test.ExpectedOutput, @"^[A-Za-z][A-Za-z0-9 :\\-]*([\\r\\n].+)?$", RegexOptions.None));
        var weakBehaviorDescriptionTerms = GetConfiguredTerms(
            peRules?.WeakBehaviorDescriptionTerms,
            "print", "return", "returns", "format", "formats", "message", "label", "text format", "toString");
        var weakBehaviorWords = ContainsAnyTerm(description, weakBehaviorDescriptionTerms);
        var nonUtilityMethods = Regex.Matches(combinedCode, @"\b(public|protected|private)\s+\w+(?:<[^>]+>)?\s+(\w+)\s*\(", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[2].Value)
            .Where(name =>
                !string.IsNullOrWhiteSpace(name) &&
                !string.Equals(name, "main", StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith("set", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var weakBehaviorMethodNames = GetConfiguredTerms(
            peRules?.WeakBehaviorMethodNames,
            "toString", "getSummary", "getInfo", "describe");
        var onlyWeakBehaviorMethodSurface = nonUtilityMethods.Count > 0 &&
                                            nonUtilityMethods.All(name => weakBehaviorMethodNames.Contains(name, StringComparer.OrdinalIgnoreCase));

        var topicText = string.Join(" ", question.TopicTags ?? new List<string>());
        var citedChunkText = string.Join("\n", question.SourceChunkIds
            .Where(chunkMap.ContainsKey)
            .Select(id => chunkMap[id].ContentText ?? string.Empty));
        var lexicalSimilarity = EstimateTokenOverlapRatio(
            $"{question.Title} {question.Description} {string.Join(" ", question.TopicTags)}",
            citedChunkText);
        var citedChunkTextLower = citedChunkText.ToLowerInvariant();
        var orderingBehaviorTerms = GetConfiguredTerms(
            peRules?.OrderingBehaviorTerms,
            "lexicographical", "alphabetical", "sorted", "ascending", "descending", "ordered");
        var orderingApiTerms = GetConfiguredTerms(
            peRules?.OrderingApiTerms,
            "compareTo", "Collections.sort", "Arrays.sort");
        var mentionsOrderingBehavior = ContainsAnyTerm(description, orderingBehaviorTerms) ||
                                       ContainsAnyTerm(solution, orderingApiTerms);
        var chunksSupportOrderingBehavior = Regex.IsMatch(citedChunkTextLower, @"\b(sort|sorted|sorting|lexicographical|alphabetical|compare)\b", RegexOptions.IgnoreCase);

        var looksLikeNearCopyLectureDemo =
            classCount >= 3 &&
            overrideCount >= 2 &&
            hasWeakOverrideMethod &&
            weakBehaviorWords &&
            (returnsMostlyFixedStrings || hasFormattingOnlyOutputs) &&
            lexicalSimilarity >= 0.22m;

        var looksLikeMessageOnlySubclassExercise =
            overrideCount >= 2 &&
            hasWeakOverrideMethod &&
            returnsMostlyFixedStrings &&
            hasFormattingOnlyOutputs;
        var looksLikeToStringOnlyWeakTask =
            overrideCount >= 1 &&
            hasWeakOverrideMethod &&
            onlyWeakBehaviorMethodSurface &&
            weakBehaviorWords &&
            !Regex.IsMatch(description, @"\b(calculate|compute|evaluate|decide|classify|update|validate)\b", RegexOptions.IgnoreCase);
        var inheritanceTopicLike =
            Regex.IsMatch(topicText, @"\binheritance\b|\bmethod overriding\b|\bpolymorphism\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(description, @"\binheritance\b|\boverrid(?:e|ing)\b|\bsuper\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(citedChunkTextLower, @"\binheritance\b|\boverrid(?:e|ing)\b|\bsuper\b", RegexOptions.IgnoreCase);
        var hasSuperReuseSurface = Regex.IsMatch(solution, @"\bsuper\s*\.\s*\w+\s*\(", RegexOptions.IgnoreCase) ||
                                   Regex.IsMatch(solution, @"\bsuper\s*\(", RegexOptions.IgnoreCase);
        var thinWrapperDomainTerms = new[]
        {
            "discount", "fee", "price", "status", "summary", "description", "report", "membership", "payment", "product"
        };
        var mentionsThinWrapperDomain = ContainsAnyTerm(description, thinWrapperDomainTerms) ||
                                        ContainsAnyTerm(solution, thinWrapperDomainTerms);
        var thinWrapperOutputShape = hasFormattingOnlyOutputs;
        var hasThinArithmeticSurface = Regex.IsMatch(solution, @"return\s+[^;]*[\+\-\*/][^;]*;", RegexOptions.IgnoreCase);
        var looksLikeJavaInheritanceThinWrapper =
            inheritanceTopicLike &&
            classCount == 2 &&
            overrideCount == 1 &&
            hasSuperReuseSurface &&
            mentionsThinWrapperDomain &&
            (thinWrapperOutputShape || hasThinArithmeticSurface);
        var looksLikeInventedOrderingBehavior =
            mentionsOrderingBehavior &&
            !chunksSupportOrderingBehavior;

        var hasAbstractBase = Regex.IsMatch(combinedCode, @"\babstract\s+class\s+\w+\b", RegexOptions.IgnoreCase);
        var hasSingleDispatchByType = Regex.IsMatch(solution, @"if\s*\(\s*\w+\.equals\(", RegexOptions.IgnoreCase) ||
                                      Regex.IsMatch(solution, @"switch\s*\(\s*\w+\s*\)", RegexOptions.IgnoreCase);
        var overrideMethodsAreSingleReturn = Regex.Matches(solution, @"@Override\s+public\s+\w+(?:<[^>]+>)?\s+\w+\s*\([^)]*\)\s*\{\s*return\s+[^;]+;\s*\}", RegexOptions.IgnoreCase | RegexOptions.Singleline).Count >= 2;
        var trivialArithmeticReturnCount = Regex.Matches(solution, @"return\s+[A-Za-z_][A-Za-z0-9_]*(\s*[\+\-\*/]\s*[A-Za-z_][A-Za-z0-9_]*){0,2}\s*;", RegexOptions.IgnoreCase).Count;
        var polymorphismTemplateTerms = GetConfiguredTerms(
            peRules?.PolymorphismTemplateTerms,
            "abstract class", "subclass", "base class", "shipment type", "payment type", "employee type", "shape type");
        var mentionsPolymorphismTemplateWords = ContainsAnyTerm(description, polymorphismTemplateTerms);
        var looksLikeTrivialPolymorphismArithmetic =
            hasAbstractBase &&
            classCount >= 3 &&
            overrideCount >= 2 &&
            hasSingleDispatchByType &&
            overrideMethodsAreSingleReturn &&
            trivialArithmeticReturnCount >= 2 &&
            mentionsPolymorphismTemplateWords;

        var hasControlFlowInSolution = Regex.IsMatch(solution, @"\b(if|else|for|while|switch)\b", RegexOptions.IgnoreCase);
        var hasSingleAssignmentFormula = Regex.IsMatch(solution, @"\w+\s*=\s*[^;]*[\+\-\*/][^;]*;", RegexOptions.IgnoreCase);
        var printsSingleScalar = Regex.IsMatch(solution, @"printf\s*\(\s*""%[dfiscu].*?""\s*,", RegexOptions.IgnoreCase) ||
                                 Regex.IsMatch(solution, @"System\.out\.println\s*\(", RegexOptions.IgnoreCase);
        var formulaDemoDescriptionTerms = GetConfiguredTerms(
            peRules?.FormulaDemoDescriptionTerms,
            "operator precedence", "use integer division", "use the remainder operator", "use the formula", "multiplication is done before addition");
        var mentionsOperatorDemo = ContainsAnyTerm(description, formulaDemoDescriptionTerms);
        var narrowArithmeticTests = (question.TestCases ?? new List<TestCase>()).Count >= 2 &&
                                    (question.TestCases ?? new List<TestCase>()).All(test =>
                                        Regex.IsMatch(test.Input ?? string.Empty, @"^\s*-?\d+(\s+-?\d+){1,3}\s*$") &&
                                        Regex.IsMatch(test.ExpectedOutput ?? string.Empty, @"^\s*-?\d+(\s*,\s*-?\d+)?\s*$"));
        var looksLikeFormulaOnlyDemo =
            !hasControlFlowInSolution &&
            hasSingleAssignmentFormula &&
            printsSingleScalar &&
            mentionsOperatorDemo &&
            narrowArithmeticTests;
        var looksLikeParityDrill =
            Regex.IsMatch(solution, @"%\s*2\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(description, @"\b(even|odd|divisible by 2)\b", RegexOptions.IgnoreCase) &&
            (question.TestCases ?? new List<TestCase>()).Count >= 2 &&
            (question.TestCases ?? new List<TestCase>())
                .All(test => Regex.IsMatch(test.ExpectedOutput ?? string.Empty, @"^\s*(EVEN|ODD)\s*$", RegexOptions.IgnoreCase));
        var looksLikeExactNotExactDrill =
            Regex.IsMatch(description, @"\b(remainder|exact|not exact|divided by)\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(solution, @"%\s*[A-Za-z_][A-Za-z0-9_]*", RegexOptions.IgnoreCase) &&
            (question.TestCases ?? new List<TestCase>()).Count >= 2 &&
            (question.TestCases ?? new List<TestCase>())
                .All(test => Regex.IsMatch(test.ExpectedOutput ?? string.Empty, @"^\s*(Exact|Not exact)\s*$", RegexOptions.IgnoreCase));
        var expectedOutputs = (question.TestCases ?? new List<TestCase>())
            .Select(test => (test.ExpectedOutput ?? string.Empty).Trim())
            .Where(output => !string.IsNullOrWhiteSpace(output))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var comparisonStatusOutputs = GetConfiguredTerms(
            peRules?.ComparisonStatusOutputs,
            "GREATER", "EQUAL", "SMALLER", "LESS");
        var hasComparisonStatusOutputs = expectedOutputs.Count is > 0 and <= 6 &&
                                         expectedOutputs.All(output =>
                                             output
                                                 .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                                 .Any(line => comparisonStatusOutputs.Contains(line, StringComparer.OrdinalIgnoreCase)));
        var comparisonWordsInDescription = GetConfiguredTerms(
            peRules?.ComparisonDescriptionTerms,
            "greater", "equal", "smaller", "less");
        var requiresComparisonClassification = ContainsAnyTerm(description, comparisonWordsInDescription);
        var hasCoveredEqualCase = (question.TestCases ?? new List<TestCase>())
            .Any(test => Regex.IsMatch(test.ExpectedOutput ?? string.Empty, @"(^|\r?\n)\s*EQUAL\s*($|\r?\n)", RegexOptions.IgnoreCase));
        var looksLikeUncoveredComparisonClassification =
            hasComparisonStatusOutputs &&
            requiresComparisonClassification &&
            !hasCoveredEqualCase;
        var looksLikePassFailThresholdCopy =
            Regex.IsMatch(description, @"\bpass\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(description, @"\bfail\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(solution, @"if\s*\(\s*\w+\s*(>=|>)\s*\d+\s*\)", RegexOptions.IgnoreCase) &&
            !question.TopicTags.Any(tag => tag.Contains("condition", StringComparison.OrdinalIgnoreCase));
        var binaryStatusOutputs = GetConfiguredTerms(
            peRules?.BinaryStatusOutputs,
            "COMPLETE", "INCOMPLETE", "DONE", "NOT DONE", "SUCCESS", "FAIL", "ACTIVE", "INACTIVE", "YES", "NO", "TRUE", "FALSE", "VALID", "INVALID");
        var hasBinaryStatusOutputs = expectedOutputs.Count is > 0 and <= 3 &&
                                     expectedOutputs.All(output => binaryStatusOutputs.Contains(output, StringComparer.OrdinalIgnoreCase));
        var hasThresholdStyleDescription =
            Regex.IsMatch(description, @"\b(at least|greater than|less than|threshold|minimum|required)\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(description, @"\breturns?\s+true\s+when\b", RegexOptions.IgnoreCase);
        var binaryStatusMethodNames = GetConfiguredTerms(
            peRules?.BinaryStatusMethodNames,
            "isComplete", "isValid", "isApproved", "isPassed", "getResult", "getStatus");
        var hasBinaryResultMethodSurface = ContainsAnyMethodName(combinedCode, binaryStatusMethodNames);
        var queueCommandTerms = GetConfiguredTerms(
            peRules?.QueueApiCloneCommandTerms,
            "ARRIVE", "SERVE", "NEXT", "EMPTY");
        var queueApiSurfaceMethods = GetConfiguredTerms(
            peRules?.QueueApiSurfaceMethods,
            "enqueue", "dequeue", "front", "isEmpty", "isFull");
        var queueCapacityJustificationTerms = GetConfiguredTerms(
            peRules?.QueueCapacityExplicitJustificationTerms,
            "do not resize", "non-growing", "fixed capacity", "circular reuse", "reuse freed positions");
        var nonDomainBinaryStatusExclusionTerms = GetConfiguredTerms(
            peRules?.NonDomainBinaryStatusExclusionTerms,
            "queue", "stack", "tree", "graph", "sort", "search", "shape", "area", "ticket", "membership", "discount", "payment");
        var looksLikeBinaryThresholdStatusTemplate =
            classCount <= 2 &&
            overrideCount <= 1 &&
            hasBinaryStatusOutputs &&
            hasThresholdStyleDescription &&
            hasBinaryResultMethodSurface &&
            !ContainsAnyTerm(description, nonDomainBinaryStatusExclusionTerms);
        var looksLikeQueueArrayTopic =
            Regex.IsMatch(topicText, @"\bqueues?\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(topicText, @"\barrays?\b", RegexOptions.IgnoreCase);
        var queueDescriptionTerms = GetConfiguredTerms(
            peRules?.QueueDescriptionTerms,
            "enqueue", "dequeue", "front", "empty");
        var descriptionMentionsQueueCommands =
            queueCommandTerms.Any(term => Regex.IsMatch(description, $@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase)) ||
            ContainsAnyTerm(description, queueDescriptionTerms);
        var solutionImplementsQueueApiSurface =
            queueApiSurfaceMethods.Any(method => Regex.IsMatch(combinedCode, $@"(?i)\b{Regex.Escape(method)}\s*\("));
        var testsUseCommandSimulationShape =
            (question.TestCases ?? new List<TestCase>()).Any(test =>
                queueCommandTerms.Any(term => Regex.IsMatch(test.Input ?? string.Empty, $@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase)));
        var looksLikeQueueApiClone =
            looksLikeQueueArrayTopic &&
            descriptionMentionsQueueCommands &&
            solutionImplementsQueueApiSurface &&
            testsUseCommandSimulationShape;
        var citedChunksPreferGrow =
            Regex.IsMatch(citedChunkTextLower, @"\bgrow\s*\(", RegexOptions.IgnoreCase);
        var questionUsesFixedCapacityFullSignal =
            Regex.IsMatch(description, @"\b(full|fixed[- ]capacity|capacity)\b", RegexOptions.IgnoreCase) ||
            (question.TestCases ?? new List<TestCase>())
                .Any(test => Regex.IsMatch(test.ExpectedOutput ?? string.Empty, @"^\s*full\s*$", RegexOptions.IgnoreCase));
        var lacksExplicitSimplifiedCapacityJustification =
            !queueCapacityJustificationTerms.Any(term => description.Contains(term, StringComparison.OrdinalIgnoreCase));
        var looksLikeQueueCapacityDrift =
            looksLikeQueueArrayTopic &&
            citedChunksPreferGrow &&
            questionUsesFixedCapacityFullSignal &&
            lacksExplicitSimplifiedCapacityJustification;
        var looksLikeAbstractClassTopic =
            Regex.IsMatch(topicText, @"\babstract classes?\b|\babstraction\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(description, @"\babstract classes?\b|\babstraction\b", RegexOptions.IgnoreCase) ||
            citedChunkTextLower.Contains("abstract class");
        var helperMethodMatches = Regex.Matches(combinedCode, @"\b(public|protected|private)\s+(?:static\s+)?(?:String|int|double|boolean|long|float|char|void)\s+(\w+)\s*\(", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[2].Value)
            .Where(name =>
                !string.IsNullOrWhiteSpace(name) &&
                !string.Equals(name, "main", StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith("get", StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith("set", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "toString", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var helperMethodCount = helperMethodMatches.Count;
        var returnsMostlyLabelOrPrefixStrings = Regex.Matches(solution, @"return\s+""[^""]{0,24}""\s*\+\s*[\w\.]+", RegexOptions.IgnoreCase).Count >= 1;
        var abstractWeakMethodNames = GetConfiguredTerms(
            peRules?.AbstractWeakMethodNames,
            "getMessage", "getLabel", "getInfo", "describe", "getSummary", "toString");
        var usesSingleAbstractMethodSurface = methodNames.Count > 0 &&
                                              methodNames.All(name => abstractWeakMethodNames.Contains(name, StringComparer.OrdinalIgnoreCase));
        var strongBehaviorDescriptionTerms = GetConfiguredTerms(
            peRules?.StrongBehaviorDescriptionTerms,
            "validate", "evaluate", "classify", "calculate", "compute", "check", "compare", "decide", "build", "format summary", "summarize");
        var lowBehaviorVerbSurface = !ContainsAnyTerm(description, strongBehaviorDescriptionTerms);
        var hasLittleBranchingOrStateLogic = !Regex.IsMatch(solution, @"\b(if|else|switch|for|while)\b", RegexOptions.IgnoreCase);
        var looksLikeThinAbstractTemplate =
            looksLikeAbstractClassTopic &&
            hasAbstractBase &&
            classCount <= 3 &&
            overrideCount >= 1 &&
            overrideCount <= 2 &&
            usesSingleAbstractMethodSurface &&
            helperMethodCount <= 1 &&
            returnsMostlyLabelOrPrefixStrings &&
            lowBehaviorVerbSurface &&
            hasLittleBranchingOrStateLogic;
        var looksLikeAbstractFormattingClone =
            looksLikeAbstractClassTopic &&
            hasAbstractBase &&
            overrideCount >= 1 &&
            hasWeakOverrideMethod &&
            hasFormattingOnlyOutputs &&
            onlyWeakBehaviorMethodSurface &&
            (returnsMostlyFixedStrings || returnsMostlyLabelOrPrefixStrings);
        var usesAnonymousOrOneOffSubclassPattern =
            looksLikeAbstractClassTopic &&
            Regex.IsMatch(solution, @"new\s+\w+\s*\([^)]*\)\s*\{", RegexOptions.IgnoreCase);
        var mentionsSingleConcreteSubclass =
            Regex.IsMatch(description, @"\bone concrete subclass\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(description, @"\bexactly one concrete subclass\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(description, @"\bone subclass\b", RegexOptions.IgnoreCase);
        var hasSimpleConditionalRule =
            Regex.IsMatch(solution, @"@Override\s+public\s+\w+(?:<[^>]+>)?\s+\w+\s*\([^)]*\)\s*\{\s*(if\s*\(|return\s+[^;?]+?\s*\?\s*[^;]+:\s*[^;]+;)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var mostlyScalarOrBooleanOutputs = (question.TestCases ?? new List<TestCase>()).Count >= 2 &&
                                           (question.TestCases ?? new List<TestCase>())
                                               .All(test => Regex.IsMatch(test.ExpectedOutput ?? string.Empty, @"^\s*([A-Za-z0-9=_\-]+(?:\s*,\s*[A-Za-z0-9=_\-]+)*\s*(\r?\n)?)+\s*$", RegexOptions.IgnoreCase));
        var looksLikeAbstractSingleSubclassRuleWrapper =
            looksLikeAbstractClassTopic &&
            hasAbstractBase &&
            classCount == 2 &&
            overrideCount == 1 &&
            helperMethodCount <= 2 &&
            mentionsSingleConcreteSubclass &&
            hasSimpleConditionalRule &&
            mostlyScalarOrBooleanOutputs;
        var matchedDemoFamilies = MatchConfiguredDemoFamilies(combinedCode, peRules?.DemoFamilies);

        if (looksLikeNearCopyLectureDemo)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "near_copy_lecture_demo", "PE shape is too close to a lecture-example pattern and only renames the original demo with cosmetic changes."));
        }

        foreach (var matchedFamily in matchedDemoFamilies.Where(item => item.FamilyMatched))
        {
            if (!string.IsNullOrWhiteSpace(matchedFamily.Rule.FamilyIssue))
            {
                issues.Add(matchedFamily.Rule.FamilyIssue);
            }
        }

        foreach (var matchedFamily in matchedDemoFamilies.Where(item => item.MethodMatched))
        {
            if (!string.IsNullOrWhiteSpace(matchedFamily.Rule.MethodIssue))
            {
                issues.Add(matchedFamily.Rule.MethodIssue);
            }
        }

        if (looksLikeMessageOnlySubclassExercise)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "message_only_subclass", "PE polymorphism shape is too weak because subclasses differ mainly by fixed labels or message strings."));
        }

        if (looksLikeToStringOnlyWeakTask)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "to_string_only_weak_task", "PE polymorphism shape is still too weak because the learner mainly overrides one formatting/display method and does not implement richer object behavior."));
        }

        if (looksLikeJavaInheritanceThinWrapper)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "java_inheritance_thin_wrapper", "PE Java inheritance shape is too template-like: one subclass mainly wraps a super call with a thin arithmetic or summary-style change, which is not yet a strong learner-facing assessment task."));
        }

        if (looksLikeInventedOrderingBehavior)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "invented_ordering_behavior", "PE introduces a new ordering or sorting rule that is not supported by the cited chunks, so the grounding is too weak."));
        }

        if (looksLikeTrivialPolymorphismArithmetic)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "trivial_polymorphism_arithmetic", "PE polymorphism shape is too template-like: abstract base plus two subclasses with one-line arithmetic overrides is not yet a strong learner-facing assessment task."));
        }

        if (looksLikeFormulaOnlyDemo)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "formula_only_demo", "PE shape is still a formula-demo exercise that mirrors lecture syntax too directly and lacks enough learner-facing assessment value."));
        }

        if (looksLikeParityDrill)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "parity_drill", "PE shape is only an even/odd parity drill, which is too weak and too generic for the current source-backed PE quality target."));
        }

        if (looksLikeExactNotExactDrill)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "exact_not_exact_drill", "PE shape is only an exact/not-exact remainder wrapper around a lecture-style arithmetic example and does not add enough assessment value."));
        }

        if (looksLikeUncoveredComparisonClassification)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "uncovered_comparison_branch", "PE introduces comparison-classification output such as GREATER/EQUAL/SMALLER but the tests do not cover all required branches."));
        }

        if (looksLikePassFailThresholdCopy)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "pass_fail_threshold_copy", "PE shape is too close to a generic PASS/FAIL threshold exemplar and is not clearly grounded in the selected source topic."));
        }

        if (looksLikeBinaryThresholdStatusTemplate)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "binary_threshold_status_template", "PE shape is still too generic: a binary threshold-based status wrapper like COMPLETE/INCOMPLETE or YES/NO does not create enough OOP-specific assessment value for this grounded PE flow."));
        }

        if (looksLikeQueueApiClone)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "queue_api_clone", "PE queue/array shape is too close to the cited lecture queue API and only wraps enqueue/front/dequeue/isEmpty with lightly renamed commands such as ARRIVE/NEXT/SERVE."));
        }

        if (looksLikeQueueCapacityDrift)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "queue_capacity_drift", "PE queue/array shape changes the source behavior from grow-on-full to fixed-capacity rejection without clearly stating that the assessment intentionally uses a non-growing circular queue variant."));
        }

        if (looksLikeThinAbstractTemplate)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "thin_abstract_template", "PE abstract-class shape is too weak: the abstract parent and subclasses only support a thin label/message template instead of a meaningful inherited behavior task."));
        }

        if (looksLikeAbstractFormattingClone)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "abstract_formatting_clone", "PE abstract-class shape is still too template-like because the learner mainly overrides one formatting or summary method with cosmetic subclass differences."));
        }

        if (usesAnonymousOrOneOffSubclassPattern)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "anonymous_or_one_off_subclass", "PE abstract-class shape drifts into an anonymous or one-off subclass demo that is too code-shape-driven and not a strong learner-facing assessment item."));
        }

        if (looksLikeAbstractSingleSubclassRuleWrapper)
        {
            issues.Add(GetConfiguredIssueMessage(peRules?.IssueMessages, "abstract_single_subclass_rule_wrapper", "PE abstract-class shape is still too thin: one abstract parent plus one concrete subclass wrapping a simple conditional rule does not create enough abstract-class-specific assessment value."));
        }

        return issues.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static QuestionGenerationReviewRuntimePolicy? ParseQuestionGenerationReviewPolicy(JsonElement? policyElement)
    {
        if (policyElement is null || policyElement.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<QuestionGenerationReviewRuntimePolicy>(
                policyElement.Value.GetRawText(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
        }
        catch
        {
            return null;
        }
    }

    private static bool MatchesReviewRule(string? ruleSubject, string? ruleQuestionType, string subject, string questionType)
    {
        var subjectMatches = string.IsNullOrWhiteSpace(ruleSubject) || string.Equals(ruleSubject, subject, StringComparison.OrdinalIgnoreCase);
        var questionTypeMatches = string.IsNullOrWhiteSpace(ruleQuestionType) || string.Equals(ruleQuestionType, questionType, StringComparison.OrdinalIgnoreCase);
        return subjectMatches && questionTypeMatches;
    }

    private static List<string> GetConfiguredTerms(IEnumerable<string>? configured, params string[] fallback)
    {
        var values = (configured ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (values.Count > 0)
        {
            return values;
        }

        return fallback
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ContainsAnyTerm(string text, IEnumerable<string> terms)
    {
        var normalized = text ?? string.Empty;
        foreach (var term in terms.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            if (term.Contains(" ", StringComparison.Ordinal))
            {
                if (normalized.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAnyMethodName(string text, IEnumerable<string> methodNames)
        => methodNames.Any(methodName => Regex.IsMatch(text ?? string.Empty, $@"(?i)\b{Regex.Escape(methodName)}\s*\("));

    private static string GetConfiguredIssueMessage(IReadOnlyDictionary<string, string>? issueMessages, string key, string fallback)
    {
        if (issueMessages is not null &&
            issueMessages.TryGetValue(key, out var configured) &&
            !string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        return fallback;
    }

    private static bool MatchesTopicPatterns(IEnumerable<string> topics, IReadOnlyList<string>? patterns, TopicMatchMode mode)
    {
        var topicList = topics
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();
        var patternList = (patterns ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();
        if (patternList.Count == 0)
        {
            return true;
        }

        return mode switch
        {
            TopicMatchMode.All => patternList.All(pattern =>
                topicList.Any(topic => topic.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                                       pattern.Contains(topic, StringComparison.OrdinalIgnoreCase))),
            _ => patternList.Any(pattern =>
                topicList.Any(topic => topic.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                                       pattern.Contains(topic, StringComparison.OrdinalIgnoreCase)))
        };
    }

    private static string BuildGenerationGuidance(
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto> chunks,
        QuestionGenerationReviewRuntimePolicy? policy)
    {
        var messages = new List<string>();
        var topics = request.Retrieval?.TargetTopics ?? new List<string>();

        if (string.Equals(request.QuestionType, "FE", StringComparison.OrdinalIgnoreCase))
        {
            var feExplanationGuidance = BuildFeExplanationGuidance(policy?.FeRules);
            if (!string.IsNullOrWhiteSpace(feExplanationGuidance))
            {
                messages.Add(feExplanationGuidance);
            }
        }
        else
        {
            var javaInheritancePeGuidance = BuildJavaInheritancePeGuidance(request, chunks);
            if (!string.IsNullOrWhiteSpace(javaInheritancePeGuidance))
            {
                messages.Add(javaInheritancePeGuidance);
            }

            var dsaHashingPeGuidance = BuildDsaHashingPeGuidance(request, chunks);
            if (!string.IsNullOrWhiteSpace(dsaHashingPeGuidance))
            {
                messages.Add(dsaHashingPeGuidance);
            }
        }

        foreach (var rule in policy?.GenerationGuidanceRules ?? new List<GenerationGuidanceRulePolicy>())
        {
            if (!MatchesReviewRule(rule.Subject, rule.QuestionType, request.Subject, request.QuestionType))
            {
                continue;
            }

            var mode = ParseTopicMatchMode(rule.TopicMatchMode);
            if (!MatchesTopicPatterns(topics, rule.TopicPatterns, mode))
            {
                continue;
            }

            messages.AddRange(rule.Messages.Where(message => !string.IsNullOrWhiteSpace(message)));
        }

        foreach (var rule in policy?.SourceRiskGuidanceRules ?? new List<SourceRiskGuidanceRulePolicy>())
        {
            if (!MatchesReviewRule(rule.Subject, rule.QuestionType, request.Subject, request.QuestionType))
            {
                continue;
            }

            var mode = ParseTopicMatchMode(rule.TopicMatchMode);
            if (!MatchesTopicPatterns(topics, rule.TopicPatterns, mode))
            {
                continue;
            }

            var chunkText = string.Join("\n", chunks.Select(chunk => $"{chunk.SectionTitle}\n{chunk.ContentText}"));
            var hasRiskSignal = rule.ChunkTermGroups.Count == 0 ||
                                rule.ChunkTermGroups.Any(group => group.All(term => chunkText.Contains(term, StringComparison.OrdinalIgnoreCase)));
            if (!hasRiskSignal)
            {
                continue;
            }

            messages.AddRange(rule.Messages.Where(message => !string.IsNullOrWhiteSpace(message)));
        }

        if (messages.Count == 0)
        {
            return "none";
        }

        return string.Join("\n- ", new[] { "Follow these runtime guidance rules strictly:" }.Concat(messages.Distinct(StringComparer.OrdinalIgnoreCase).Select(message => message.Trim())));
    }

    private static string? BuildFeExplanationGuidance(FeRulesPolicy? feRules)
    {
        if (feRules is null || !feRules.RequireExplanation)
        {
            return null;
        }

        var maxSentences = feRules.MaximumExplanationSentenceCount is > 0
            ? feRules.MaximumExplanationSentenceCount
            : 3;

        return $"For FE explanations, stay strictly within the cited chunk evidence. Keep the explanation short and conservative, within {maxSentences} sentence(s), and prefer 1 to 2 short sentences when possible. Sentence 1 should state why the selected correct option is supported by the chunk. Mention a distractor only when the chunk directly supports that contrast, and do not explain every distractor one by one. Do not add extra background facts, textbook filler, or unsupported claims.";
    }

    private static string? BuildJavaInheritancePeGuidance(
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto> chunks)
    {
        if (!string.Equals(NormalizeSubject(request.Subject), "JAVA_OOP", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var topics = request.Retrieval?.TargetTopics ?? new List<string>();
        var topicSignals = string.Join(" ", topics);
        var chunkText = string.Join("\n", chunks.Select(chunk => $"{chunk.SectionTitle}\n{chunk.ContentText}"));
        var inheritanceLike =
            Regex.IsMatch(topicSignals, @"\binheritance\b|\bmethod overriding\b|\bsuper\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(chunkText, @"\binheritance\b|\boverrid(?:e|ing)\b|\bsuper\s*\(", RegexOptions.IgnoreCase);
        if (!inheritanceLike)
        {
            return null;
        }

        return "For JAVA_OOP PE on inheritance/overriding topics, do not invent a thin business or reporting story such as discounts, fees, status labels, summaries, or cosmetic description formatting unless the cited chunks clearly support that learner-facing behavior. Avoid superclass-plus-subclass demo shapes where the learner only overrides one string/arithmetic method or appends a value to a super call. Use a grounded object behavior that clearly needs inheritance or overriding, and keep Java starter code technically valid: constructors must never contain return statements.";
    }

    private static string? BuildDsaHashingPeGuidance(
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto> chunks)
    {
        if (!string.Equals(NormalizeSubject(request.Subject), "DSA_JAVA", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var topics = request.Retrieval?.TargetTopics ?? new List<string>();
        var topicSignals = string.Join(" ", topics);
        var chunkText = string.Join("\n", chunks.Select(chunk => $"{chunk.SectionTitle}\n{chunk.ContentText}"));
        var hashingLike =
            Regex.IsMatch(topicSignals, @"\bhash", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(chunkText, @"\bhash(ing| table| function| index| value)?\b", RegexOptions.IgnoreCase);
        if (!hashingLike)
        {
            return null;
        }

        var hasCollisionSignal = Regex.IsMatch(chunkText, @"\b(linear probing|quadratic probing|collision|open addressing|probe)\b", RegexOptions.IgnoreCase);
        var hasFoldingSignal = Regex.IsMatch(chunkText, @"\bfolding\b", RegexOptions.IgnoreCase);
        var hasDivisionSignal = Regex.IsMatch(chunkText, @"\bdivision hash\b|\bh\(x\)\s*=.*%\s*m\b", RegexOptions.IgnoreCase);

        var guidance = new List<string>
        {
            "For DSA_JAVA PE on hashing topics, prefer one concrete learner-facing algorithmic task grounded in the cited chunks: computing a hash index, applying folding or division, simulating linear or quadratic probing, or searching/inserting in a hash table.",
            "Do not drift into broad application lists, cryptographic hashing, or extendible-file theory unless the selected source scope clearly centers on that behavior.",
            "Keep the task operational, deterministic, and testable with concrete input/output cases that cover the required branches and probing behavior."
        };

        if (hasFoldingSignal && !hasCollisionSignal)
        {
            guidance.Add("When the cited source mainly teaches folding hash computation, keep the task at hash-index computation level. Do not introduce fixed-table insertion, collision counting, slot occupancy, or rehashing unless the selected chunks explicitly support those learner-facing requirements.");
            guidance.Add("If the source only shows standard folding, state clearly that the task uses the non-shift folding variant and keep inputs to non-negative numeric keys.");
        }

        if (hasDivisionSignal && !hasCollisionSignal)
        {
            guidance.Add("If the grounded content is only division-hash computation, do not extend the task into collision-resolution stories or table simulation.");
        }

        return string.Join(" ", guidance);
    }

    private static IReadOnlyList<RetrievedChunkDto> SelectPromptChunksForRequest(
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto> chunks)
    {
        var selected = chunks;
        if (ShouldFilterJavaInheritancePePromptChunks(request))
        {
            var filtered = chunks
                .Where(chunk => !LooksLikeJavaInheritanceExampleGlossaryChunk(chunk))
                .ToList();

            if (filtered.Count >= 2)
            {
                selected = filtered;
            }
        }

        if (ShouldFilterDsaHashingPePromptChunks(request))
        {
            var filtered = selected
                .Where(chunk => !LooksLikeDsaHashingPeripheralChunk(chunk))
                .OrderByDescending(chunk => ScoreDsaHashingPromptChunk(chunk))
                .ToList();

            var sameChapter = FilterDsaHashingPromptChunksToRequestedChapter(request, filtered);
            if (sameChapter.Count > 0)
            {
                selected = sameChapter;
            }
            else if (filtered.Count >= 1)
            {
                selected = filtered.Take(3).ToList();
            }
        }

        if (IsPeMediumHardRequest(request) && selected.Count > MaxPromptChunksPeMediumHard)
        {
            selected = selected
                .OrderByDescending(chunk => string.Equals(chunk.SelectionRole, "primary", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(chunk => chunk.TopicScore)
                .ThenByDescending(chunk => chunk.FinalScore)
                .Take(MaxPromptChunksPeMediumHard)
                .ToList();
        }

        return selected;
    }

    private static bool ShouldFilterJavaInheritancePePromptChunks(GenerationReviewRequestDto request)
    {
        if (!string.Equals(NormalizeSubject(request.Subject), "JAVA_OOP", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var topics = request.Retrieval?.TargetTopics ?? new List<string>();
        var topicText = string.Join(" ", topics);
        return Regex.IsMatch(topicText, @"\binheritance\b|\bmethod overriding\b|\bsuper\b|\bpolymorphism\b", RegexOptions.IgnoreCase);
    }

    private static bool LooksLikeJavaInheritanceExampleGlossaryChunk(RetrievedChunkDto chunk)
    {
        var content = chunk.ContentText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var hasImageSignal =
            content.Contains("[[IMAGE:", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("[Image Summary:", StringComparison.OrdinalIgnoreCase);

        var lines = content
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return false;
        }

        var identifierLikeLines = lines.Count(line =>
            Regex.IsMatch(line, @"^(\[\[IMAGE:.*\]\]|\[Image Summary:.*\]|`[^`]+`|\*\*[^*]+\*\*|[A-Za-z_][A-Za-z0-9_` ]{0,24})$", RegexOptions.IgnoreCase));
        var sentenceLikeLines = lines.Count(line =>
            Regex.IsMatch(line, @"\b(is|are|has|have|returns?|stores?|extends|inherits)\b", RegexOptions.IgnoreCase) ||
            line.Contains('.', StringComparison.Ordinal));
        var sectionLooksExampleOnly =
            Regex.IsMatch(chunk.SectionTitle ?? string.Empty, @"^(Item|Vase|Painting|Person|Student|Professor Student)$", RegexOptions.IgnoreCase);

        return sectionLooksExampleOnly &&
               (hasImageSignal || chunk.TokenCount <= 95) &&
               identifierLikeLines >= Math.Max(3, lines.Length / 2) &&
               sentenceLikeLines <= 1;
    }

    private static bool ShouldFilterDsaHashingPePromptChunks(GenerationReviewRequestDto request)
    {
        if (!string.Equals(NormalizeSubject(request.Subject), "DSA_JAVA", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var topics = request.Retrieval?.TargetTopics ?? new List<string>();
        var topicText = string.Join(" ", topics);
        if (Regex.IsMatch(topicText, @"\bhash", RegexOptions.IgnoreCase))
        {
            return true;
        }

        var chunkSignals = string.Join("\n", request.Chunks?.Select(chunk => $"{chunk.SectionTitle}\n{chunk.ContentText}") ?? Array.Empty<string>());
        return Regex.IsMatch(chunkSignals, @"\bhash(ing| function| index| table| value)?\b", RegexOptions.IgnoreCase);
    }

    private static List<RetrievedChunkDto> FilterDsaHashingPromptChunksToRequestedChapter(
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto> chunks)
    {
        var chapterKey = request.Retrieval?.ChapterKey;
        if (string.IsNullOrWhiteSpace(chapterKey))
        {
            chapterKey = chunks
                .Where(chunk => string.Equals(chunk.SelectionRole, "primary", StringComparison.OrdinalIgnoreCase))
                .Select(chunk => chunk.ChapterKey)
                .FirstOrDefault(key => !string.IsNullOrWhiteSpace(key))
                ?? chunks.Select(chunk => chunk.ChapterKey).FirstOrDefault(key => !string.IsNullOrWhiteSpace(key));
        }

        if (string.IsNullOrWhiteSpace(chapterKey))
        {
            return new List<RetrievedChunkDto>();
        }

        return chunks
            .Where(chunk => string.Equals(chunk.ChapterKey, chapterKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(chunk => chunk.TopicScore)
            .ThenByDescending(chunk => chunk.FinalScore)
            .Take(MaxPromptChunksPeMediumHard)
            .ToList();
    }

    private static bool LooksLikeDsaHashingPeripheralChunk(RetrievedChunkDto chunk)
    {
        var title = chunk.SectionTitle ?? string.Empty;
        var content = chunk.ContentText ?? string.Empty;
        var combined = $"{title}\n{content}";

        var peripheralTitle =
            Regex.IsMatch(title, @"\b(applications of hashing|cryptographic hash functions|hash functions for extendible files|hash functions for extendable files)\b", RegexOptions.IgnoreCase);
        var peripheralTheory =
            Regex.IsMatch(combined, @"\b(databases?|unix shell|ip routing|symbol tables?|games?)\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(combined, @"\bcryptographic\b", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(combined, @"\bextendible hashing\b|\bdynamic/extendible hashing\b|\bbuckets are added and deleted on demand\b", RegexOptions.IgnoreCase);
        return peripheralTitle || peripheralTheory;
    }

    private static int ScoreDsaHashingPromptChunk(RetrievedChunkDto chunk)
    {
        var title = chunk.SectionTitle ?? string.Empty;
        var content = chunk.ContentText ?? string.Empty;
        var combined = $"{title}\n{content}";
        var score = 0;

        if (Regex.IsMatch(title, @"\b(hash functions|collision resolution|perfect hash functions)\b", RegexOptions.IgnoreCase))
        {
            score += 4;
        }

        if (Regex.IsMatch(combined, @"\b(folding|division hash|linear probing|quadratic probing|open addressing|search an item|insert)\b", RegexOptions.IgnoreCase))
        {
            score += 5;
        }

        if (Regex.IsMatch(combined, @"\b(applications of hashing|cryptographic hash functions|extendible files|extendable files|ip routing|unix shell|databases?)\b", RegexOptions.IgnoreCase))
        {
            score -= 6;
        }

        score += Math.Min(2, chunk.TokenCount / 180);
        return score;
    }

    private static string SerializeChunksForPrompt(GenerationReviewRequestDto request, IReadOnlyList<RetrievedChunkDto> chunks)
    {
        var maxPromptChunks = IsPeMediumHardRequest(request) ? MaxPromptChunksPeMediumHard : MaxPromptChunks;
        var maxContentCharacters = IsPeMediumHardRequest(request) ? MaxChunkContentCharactersPeMediumHard : MaxChunkContentCharacters;
        var payload = chunks
            .OrderByDescending(chunk => string.Equals(chunk.SelectionRole, "primary", StringComparison.OrdinalIgnoreCase) ? 2 : 1)
            .ThenByDescending(chunk => chunk.TopicScore)
            .ThenByDescending(chunk => chunk.FinalScore)
            .Take(maxPromptChunks)
            .Select(chunk => new Dictionary<string, object?>
        {
            ["chunk_id"] = chunk.ChunkId,
            ["section_title"] = chunk.SectionTitle,
            ["chapter_key"] = chunk.ChapterKey,
            ["topic_tags"] = chunk.TopicTags,
            ["selection_reasons"] = (chunk.SelectionReasons ?? new List<string>())
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Take(MaxChunkSelectionReasons)
                .ToList(),
            ["token_count"] = chunk.TokenCount,
            ["topic_score"] = chunk.TopicScore,
            ["selection_role"] = chunk.SelectionRole,
            ["content_text"] = BuildPromptChunkContent(chunk.ContentText, maxContentCharacters)
        }).ToList();

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string BuildPromptChunkContent(string? contentText, int maxContentCharacters)
    {
        var sanitized = AIContentSanitizer.SanitizeChunkContent(contentText);
        var normalized = NormalizeWhitespace(sanitized);
        if (normalized.Length <= maxContentCharacters)
        {
            return normalized;
        }

        return $"{normalized[..maxContentCharacters].TrimEnd()}...";
    }

    private static string SerializeQuestionsForPrompt(
        IReadOnlyList<GeneratedQuestionCandidateDto> questions,
        PromptQuestionSerializationMode mode = PromptQuestionSerializationMode.Full)
    {
        var payload = questions.Select(question => new Dictionary<string, object?>
        {
            ["type"] = question.Type,
            ["topic_tags"] = question.TopicTags,
            ["difficulty"] = question.Difficulty,
            ["title"] = question.Title,
            ["description"] = question.Description,
            ["source_chunk_ids"] = question.SourceChunkIds,
            ["skeleton_code"] = ShouldIncludePromptCode(question, mode)
                ? SerializeCodeFilesForPrompt(question.SkeletonCode, includeReadOnly: true, mode)
                : null,
            ["solution_code"] = ShouldIncludePromptCode(question, mode)
                ? SerializeCodeFilesForPrompt(question.SolutionCode, includeReadOnly: false, mode)
                : null,
            ["test_cases"] = ShouldIncludePromptTestCases(question, mode)
                ? SerializeTestCasesForPrompt(question.TestCases, mode)
                : null,
            ["options"] = question.Options,
            ["correct_answer"] = question.CorrectAnswer,
            ["explanation"] = mode == PromptQuestionSerializationMode.PreviousQuestions && string.Equals(question.Type, "PE", StringComparison.OrdinalIgnoreCase)
                ? null
                : question.Explanation
        }).ToList();

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static bool ShouldIncludePromptCode(GeneratedQuestionCandidateDto question, PromptQuestionSerializationMode mode)
        => !(mode == PromptQuestionSerializationMode.PreviousQuestions &&
             string.Equals(question.Type, "PE", StringComparison.OrdinalIgnoreCase));

    private static bool ShouldIncludePromptTestCases(GeneratedQuestionCandidateDto question, PromptQuestionSerializationMode mode)
        => !(mode == PromptQuestionSerializationMode.PreviousQuestions &&
             string.Equals(question.Type, "PE", StringComparison.OrdinalIgnoreCase));

    private static PromptFootprint EstimateGenerationPromptFootprint(
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto> chunks,
        JsonElement rubric,
        QuestionGenerationReviewRuntimePolicy? policy,
        string? revisionFeedback,
        IReadOnlyList<GeneratedQuestionCandidateDto>? previousQuestions)
    {
        var promptChunks = SelectPromptChunksForRequest(request, chunks);
        var maxPromptChunks = IsPeMediumHardRequest(request) ? MaxPromptChunksPeMediumHard : MaxPromptChunks;
        var topicsJson = JsonSerializer.Serialize(request.Retrieval?.TargetTopics ?? new List<string>(), JsonOptions);
        var subjectFocus = BuildSubjectFocus(request.Subject, request.QuestionType, policy);
        var guidance = BuildGenerationGuidance(request, promptChunks, policy);
        var rubricJson = JsonSerializer.Serialize(rubric, JsonOptions);
        var chunksJson = SerializeChunksForPrompt(request, promptChunks);
        var previousQuestionsJson = previousQuestions is null ? "[]" : SerializeQuestionsForPrompt(previousQuestions, PromptQuestionSerializationMode.PreviousQuestions);
        var revision = string.IsNullOrWhiteSpace(revisionFeedback) ? "none" : revisionFeedback;

        return new PromptFootprint(
            topicsJson.Length + subjectFocus.Length + guidance.Length + rubricJson.Length + chunksJson.Length + previousQuestionsJson.Length + revision.Length,
            Math.Min(promptChunks.Count, maxPromptChunks),
            previousQuestions?.Count ?? 0,
            0);
    }

    private static PromptFootprint EstimateRepairPromptFootprint(
        GenerationReviewRequestDto request,
        IReadOnlyList<RetrievedChunkDto> chunks,
        JsonElement rubric,
        QuestionGenerationReviewRuntimePolicy? policy,
        string brokenResponse)
    {
        var promptChunks = SelectPromptChunksForRequest(request, chunks);
        var maxPromptChunks = IsPeMediumHardRequest(request) ? MaxPromptChunksPeMediumHard : MaxPromptChunks;
        var topicsJson = JsonSerializer.Serialize(request.Retrieval?.TargetTopics ?? new List<string>(), JsonOptions);
        var subjectFocus = BuildSubjectFocus(request.Subject, request.QuestionType, policy);
        var guidance = BuildGenerationGuidance(request, promptChunks, policy);
        var rubricJson = JsonSerializer.Serialize(rubric, JsonOptions);
        var chunksJson = SerializeChunksForPrompt(request, promptChunks);
        var broken = brokenResponse ?? string.Empty;

        return new PromptFootprint(
            topicsJson.Length + subjectFocus.Length + guidance.Length + rubricJson.Length + chunksJson.Length + broken.Length,
            Math.Min(promptChunks.Count, maxPromptChunks),
            0,
            broken.Length);
    }

    private static PromptFootprint EstimateReviewPromptFootprint(
        GenerationReviewRequestDto request,
        IReadOnlyList<GeneratedQuestionCandidateDto> questions,
        IReadOnlyList<RetrievedChunkDto> chunks,
        JsonElement rubric,
        QuestionGenerationReviewRuntimePolicy? policy)
    {
        var promptChunks = SelectPromptChunksForRequest(request, chunks);
        var maxPromptChunks = IsPeMediumHardRequest(request) ? MaxPromptChunksPeMediumHard : MaxPromptChunks;
        var topicsJson = JsonSerializer.Serialize(request.Retrieval?.TargetTopics ?? new List<string>(), JsonOptions);
        var subjectFocus = BuildSubjectFocus(request.Subject, request.QuestionType, policy);
        var rubricJson = JsonSerializer.Serialize(rubric, JsonOptions);
        var chunksJson = SerializeChunksForPrompt(request, promptChunks);
        var questionsJson = SerializeQuestionsForPrompt(questions, PromptQuestionSerializationMode.Review);

        return new PromptFootprint(
            topicsJson.Length + subjectFocus.Length + rubricJson.Length + chunksJson.Length + questionsJson.Length,
            Math.Min(promptChunks.Count, maxPromptChunks),
            questions.Count,
            0);
    }

    private static List<Dictionary<string, object?>>? SerializeCodeFilesForPrompt(
        IReadOnlyList<CodeFile>? files,
        bool includeReadOnly,
        PromptQuestionSerializationMode mode)
    {
        if (files is null)
        {
            return null;
        }

        return files.Select(file =>
        {
            var payload = new Dictionary<string, object?>
            {
                ["filename"] = file.Filename,
                ["content"] = TruncatePromptCodeContent(file.Content, mode)
            };

            if (includeReadOnly)
            {
                payload["is_readonly"] = file.IsReadOnly;
            }

            return payload;
        }).ToList();
    }

    private static List<Dictionary<string, object?>>? SerializeTestCasesForPrompt(
        IReadOnlyList<TestCase>? testCases,
        PromptQuestionSerializationMode mode)
    {
        if (testCases is null)
        {
            return null;
        }

        var capped = mode == PromptQuestionSerializationMode.PreviousQuestions
            ? testCases.Take(MaxPreviousQuestionTestCases)
            : mode == PromptQuestionSerializationMode.Review
                ? testCases.Take(MaxReviewTestCases)
                : testCases;

        return capped.Select(test => new Dictionary<string, object?>
        {
            ["input"] = test.Input,
            ["expected_output"] = test.ExpectedOutput,
            ["is_hidden"] = test.IsHidden
        }).ToList();
    }

    private static string TruncatePromptCodeContent(string? content, PromptQuestionSerializationMode mode)
    {
        var text = content ?? string.Empty;
        var maxChars = mode switch
        {
            PromptQuestionSerializationMode.PreviousQuestions => MaxPreviousQuestionCodeCharacters,
            PromptQuestionSerializationMode.Review => MaxReviewCodeCharacters,
            _ => int.MaxValue
        };

        if (text.Length <= maxChars)
        {
            return text;
        }

        return $"{text[..maxChars].TrimEnd()}\n/* truncated for prompt */";
    }

    private static TopicMatchMode ParseTopicMatchMode(string? value)
    {
        return string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            ? TopicMatchMode.All
            : TopicMatchMode.Any;
    }

    private static List<MatchedDemoFamily> MatchConfiguredDemoFamilies(string combinedCode, IReadOnlyList<DemoFamilyPolicy>? families)
    {
        var matches = new List<MatchedDemoFamily>();
        if (string.IsNullOrWhiteSpace(combinedCode) || families is null)
        {
            return matches;
        }

        foreach (var family in families)
        {
            var classTerms = family.RequiredClassTerms
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
            if (classTerms.Count == 0)
            {
                continue;
            }

            var familyMatched = classTerms.All(term =>
                Regex.IsMatch(combinedCode, $@"(?i)\b(class|interface|abstract\s+class)\s+{Regex.Escape(term)}\b"));
            if (familyMatched && family.RequiresAbstractBase && !Regex.IsMatch(combinedCode, @"(?i)\babstract\s+class\b"))
            {
                familyMatched = false;
            }

            var methodMatched = familyMatched &&
                                family.MethodTerms.Any(term => Regex.IsMatch(combinedCode, $@"(?i)\b{Regex.Escape(term)}\s*\("));
            matches.Add(new MatchedDemoFamily(family, familyMatched, methodMatched));
        }

        return matches;
    }

    private static decimal EstimateTokenOverlapRatio(string left, string right)
    {
        var leftTokens = TokenizeForComparison(left);
        var rightTokens = TokenizeForComparison(right);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0m;
        }

        var overlap = leftTokens.Intersect(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        return Math.Round((decimal)overlap / Math.Max(1, leftTokens.Count), 4);
    }

    private static HashSet<string> TokenizeForComparison(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "and", "or", "of", "to", "in", "on", "for", "with", "from",
            "write", "program", "complete", "class", "method", "main", "input", "output",
            "java", "c", "using", "that", "this", "must", "reads", "print", "returns"
        };

        return Regex.Matches(text ?? string.Empty, @"[A-Za-z_][A-Za-z0-9_]+")
            .Select(match => match.Value.Trim().ToLowerInvariant())
            .Where(token => token.Length > 2 && !stopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static ContextPackDto MapContextPack(AIContextPack pack)
    {
        return new ContextPackDto
        {
            PackId = pack.Id,
            PackType = pack.PackType,
            PackStrategy = pack.PackStrategy,
            PackStatus = pack.PackStatus,
            UserId = pack.UserId,
            CourseId = pack.CourseId,
            DocumentId = pack.DocumentId,
            Subject = pack.Subject,
            QuestionType = pack.QuestionType,
            TargetDifficulty = pack.TargetDifficulty,
            TargetTopics = pack.TargetTopics,
            SourceScope = pack.SourceScope,
            RetrievalQuery = pack.RetrievalQuery,
            SourceChunkIds = pack.SourceChunkIds,
            RetrievedChunks = pack.RetrievedChunks.Select(item => new RetrievedChunkDto
            {
                ChunkId = item.ChunkId,
                DocumentId = item.DocumentId ?? pack.DocumentId,
                ChunkIndex = item.ChunkIndex,
                SectionTitle = item.SectionTitle,
                TopicTags = item.TopicTags,
                TokenCount = item.TokenCount,
                TopicScore = item.TopicScore,
                LexicalScore = item.LexicalScore,
                SectionScore = item.SectionScore,
                FinalScore = item.FinalScore,
                SelectionRole = item.SelectionRole,
                ContentText = item.ContentText
            }).ToList(),
            Chunks = pack.Chunks.Select(item => new PackedChunkDto
            {
                ChunkId = item.ChunkId,
                Title = item.Title,
                PackedText = item.PackedText,
                TokenCount = item.TokenCount,
                SelectionRole = item.SelectionRole
            }).ToList(),
            SummaryText = pack.SummaryText,
            ContextText = pack.ContextText,
            TokenCount = pack.TokenCount,
            SourceTokenCount = pack.SourceTokenCount,
            CompressionRatio = pack.CompressionRatio,
            RecommendedQuestionCount = pack.RecommendedQuestionCount,
            MaxQuestionCount = pack.MaxQuestionCount,
            UsageCount = pack.UsageCount,
            LastUsedAt = pack.LastUsedAt,
            ExpiresAt = pack.ExpiresAt,
            CreatedAt = pack.CreatedAt,
            UpdatedAt = pack.UpdatedAt
        };
    }

    private sealed record ChunkResolutionResult(
        IReadOnlyList<RetrievedChunkDto> Chunks,
        ContextPackDto? ContextPack,
        RetrievalPlanResultDto? RetrievalPlan);

    private sealed record RepairGenerationResult(
        List<GeneratedQuestionCandidateDto> Questions,
        AITextResponse Response);

    private async Task UpdateContextPackAuditAsync(
        ChunkResolutionResult chunkResolution,
        string generationRunId,
        QuestionReviewDecisionDto review,
        QuestionPersistenceResultDto persistence,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chunkResolution.ContextPack?.PackId))
        {
            return;
        }

        var pack = await _contextPackRepository.GetByIdAsync(chunkResolution.ContextPack.PackId);
        if (pack is null)
        {
            return;
        }

        pack.LastGenerationRunId = generationRunId;
        pack.LastGenerationStatus = review.ReviewStatus;
        pack.LastGeneratedAt = DateTime.UtcNow;
        pack.LastUsedAt = DateTime.UtcNow;
        pack.UpdatedAt = DateTime.UtcNow;
        pack.UsageCount = Math.Max(pack.UsageCount, 0) + 1;
        pack.LastPersistedQuestionIds = persistence.FeQuestionIds
            .Concat(persistence.PeQuestionIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await _contextPackRepository.UpdateAsync(pack);
    }

    private async Task<string> LogUsageAsync(
        GenerationReviewRequestDto request,
        string agentId,
        string stepName,
        AIResolvedExecutionOptions resolved,
        AITextResponse response,
        int attempt,
        ChunkResolutionResult chunkResolution,
        Dictionary<string, object?> extraPayload,
        AIPromptTemplateDto? promptArtifact,
        AIJsonArtifactDto? policyArtifact,
        AIJsonArtifactDto? rubricArtifact,
        CancellationToken cancellationToken)
    {
        var usage = AIUsageAccounting.ForText(
            response.Provider ?? resolved.Provider,
            resolved.Model,
            response.Model,
            response.InputTokens,
            response.OutputTokens,
            response.TotalTokens,
            response.ReportedCostUsd,
            response.UsageSource,
            response.CostSource);
        var estimatedCredits = EstimateCredits(usage.CostUsd, usage.TotalTokens);

        var payload = new Dictionary<string, object?>
        {
            ["feature"] = "QuestionGenerationReview",
            ["step"] = stepName,
            ["mode"] = request.Mode,
            ["attempt"] = attempt,
            ["subject"] = request.Subject,
            ["question_type"] = request.QuestionType,
            ["difficulty"] = request.Difficulty,
            ["course_id"] = request.CourseId,
            ["document_id"] = request.DocumentId,
            ["context_pack_id"] = chunkResolution.ContextPack?.PackId,
            ["retrieval_plan_id"] = chunkResolution.RetrievalPlan?.PlanId,
            ["chunk_count"] = chunkResolution.Chunks.Count,
            ["provider"] = usage.Provider,
            ["model"] = usage.EffectiveModel,
            ["configured_model"] = resolved.Model,
            ["effective_model"] = usage.EffectiveModel,
            ["model_family"] = usage.ModelFamily,
            ["normalized_model_key"] = usage.NormalizedModelKey,
            ["input_tokens"] = usage.InputTokens,
            ["output_tokens"] = usage.OutputTokens,
            ["total_tokens"] = usage.TotalTokens,
            ["usage_source"] = usage.UsageSource,
            ["cost_source"] = usage.CostSource,
            ["fallback_used"] = response.FromFallback,
            ["fallback_from_provider"] = response.FallbackFromProvider,
            ["fallback_from_model"] = response.FallbackFromModel,
            ["fallback_reason_code"] = response.FallbackReasonCode,
            ["persist_questions"] = request.PersistQuestions,
            ["is_public"] = request.IsPublic,
            ["prompt_key"] = promptArtifact?.Key,
            ["prompt_version"] = promptArtifact?.Version,
            ["prompt_source"] = promptArtifact?.Source,
            ["policy_key"] = policyArtifact?.ArtifactKey,
            ["policy_version"] = policyArtifact?.Version,
            ["policy_source"] = policyArtifact?.Source,
            ["rubric_key"] = rubricArtifact?.ArtifactKey,
            ["rubric_version"] = rubricArtifact?.Version,
            ["rubric_source"] = rubricArtifact?.Source,
            ["runtime_snapshot"] = BuildRuntimeSnapshot(resolved, usage, response),
            ["artifact_snapshot"] = BuildArtifactSnapshot(promptArtifact, policyArtifact, rubricArtifact),
            ["raw_response"] = response.RawResponse
        };

        foreach (var item in extraPayload)
        {
            payload[item.Key] = item.Value;
        }

        var log = new AIUsageLog
        {
            TriggeredBy = request.UserId,
            AgentId = agentId,
            CreditsDeducted = estimatedCredits,
            TokensUsed = usage.TotalTokens,
            CostUsd = usage.CostUsd,
            CreatedAt = DateTime.UtcNow,
            PayloadData = payload
        };

        await _aiUsageLogRepository.CreateAsync(log);
        return log.Id;
    }

    private static Dictionary<string, object?> BuildRuntimeSnapshot(
        AIResolvedExecutionOptions resolved,
        AITextUsageSnapshot usage,
        AITextResponse response)
    {
        return new Dictionary<string, object?>
        {
            ["feature_name"] = resolved.FeatureName,
            ["configured_provider"] = resolved.Provider,
            ["configured_model"] = resolved.Model,
            ["effective_provider"] = usage.Provider,
            ["effective_model"] = usage.EffectiveModel,
            ["frontend_override_used"] = resolved.UsedFrontendOverride,
            ["fallback_configured"] = !string.IsNullOrWhiteSpace(resolved.FallbackProvider) && !string.IsNullOrWhiteSpace(resolved.FallbackModel),
            ["fallback_provider"] = resolved.FallbackProvider,
            ["fallback_model"] = resolved.FallbackModel,
            ["fallback_used"] = response.FromFallback,
            ["usage_source"] = usage.UsageSource,
            ["cost_source"] = usage.CostSource
        };
    }

    private static Dictionary<string, object?> BuildArtifactSnapshot(
        AIPromptTemplateDto? promptArtifact,
        AIJsonArtifactDto? policyArtifact,
        AIJsonArtifactDto? rubricArtifact)
    {
        return new Dictionary<string, object?>
        {
            ["prompt"] = promptArtifact is null ? null : new Dictionary<string, object?>
            {
                ["key"] = promptArtifact.Key,
                ["version"] = promptArtifact.Version,
                ["source"] = promptArtifact.Source,
                ["artifact_type"] = promptArtifact.ArtifactType
            },
            ["policy"] = policyArtifact is null ? null : new Dictionary<string, object?>
            {
                ["key"] = policyArtifact.ArtifactKey,
                ["version"] = policyArtifact.Version,
                ["source"] = policyArtifact.Source,
                ["artifact_type"] = policyArtifact.ArtifactType
            },
            ["rubric"] = rubricArtifact is null ? null : new Dictionary<string, object?>
            {
                ["key"] = rubricArtifact.ArtifactKey,
                ["version"] = rubricArtifact.Version,
                ["source"] = rubricArtifact.Source,
                ["artifact_type"] = rubricArtifact.ArtifactType
            }
        };
    }

    private static double EstimateCredits(decimal costUsd, int tokensUsed)
    {
        if (costUsd <= 0m)
        {
            return tokensUsed > 0 ? Math.Round(tokensUsed / 1000d, 4) : 0d;
        }

        return Math.Round((double)(costUsd * 100m), 4);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private enum TopicMatchMode
    {
        Any,
        All
    }

    private enum PromptQuestionSerializationMode
    {
        Full,
        PreviousQuestions,
        Review
    }

    private sealed record MatchedDemoFamily(DemoFamilyPolicy Rule, bool FamilyMatched, bool MethodMatched);

    private sealed class QuestionGenerationReviewRuntimePolicy
    {
        public List<ShortCircuitRulePolicy> ShortCircuitRules { get; init; } = new();
        public List<RetryGuidanceRulePolicy> RetryGuidanceRules { get; init; } = new();
        public List<GenerationGuidanceRulePolicy> GenerationGuidanceRules { get; init; } = new();
        public List<SourceRiskGuidanceRulePolicy> SourceRiskGuidanceRules { get; init; } = new();
        public List<SubjectFocusRulePolicy> SubjectFocusRules { get; init; } = new();
        public FeRulesPolicy? FeRules { get; init; }
        public PeLowValueRulesPolicy? PeLowValueRules { get; init; }
    }

    private sealed class ShortCircuitRulePolicy
    {
        public string? Subject { get; init; }
        public string? QuestionType { get; init; }
        public List<string> RequiredTopicsAny { get; init; } = new();
        public List<string> RequiredTopicSignalsAny { get; init; } = new();
        public List<List<string>> RequiredTopicSignalsAllGroups { get; init; } = new();
        public int MaxChunkCount { get; init; }
        public int ThinChunkTokenThreshold { get; init; }
        public int MinThinChunkCount { get; init; }
        public int MaxChapterSpread { get; init; }
        public string? BehaviorRichSignalRegex { get; init; }
        public List<List<string>> DemoPhraseGroups { get; init; } = new();
        public List<string> TheorySectionTitles { get; init; } = new();
        public List<string> TheorySectionContains { get; init; } = new();
        public List<string> ExcludedTextTerms { get; init; } = new();
        public string? ReviewStatus { get; init; }
        public decimal Score { get; init; }
        public List<string> Issues { get; init; } = new();
        public List<string> Suggestions { get; init; } = new();
    }

    private sealed class RetryGuidanceRulePolicy
    {
        public string? Subject { get; init; }
        public string? QuestionType { get; init; }
        public string? TopicMatchMode { get; init; }
        public List<string> TopicPatterns { get; init; } = new();
        public List<string> Messages { get; init; } = new();
    }

    private sealed class GenerationGuidanceRulePolicy
    {
        public string? Subject { get; init; }
        public string? QuestionType { get; init; }
        public string? TopicMatchMode { get; init; }
        public List<string> TopicPatterns { get; init; } = new();
        public List<string> Messages { get; init; } = new();
    }

    private sealed class SourceRiskGuidanceRulePolicy
    {
        public string? Subject { get; init; }
        public string? QuestionType { get; init; }
        public string? TopicMatchMode { get; init; }
        public List<string> TopicPatterns { get; init; } = new();
        public List<List<string>> ChunkTermGroups { get; init; } = new();
        public List<string> Messages { get; init; } = new();
    }

    private sealed class SubjectFocusRulePolicy
    {
        public string? Subject { get; init; }
        public string? QuestionType { get; init; }
        public string? Message { get; init; }
    }

    private sealed class FeRulesPolicy
    {
        public int OptionCount { get; init; }
        public int CorrectAnswerCount { get; init; }
        public string? CorrectAnswerLabelRegex { get; init; }
        public int MinimumOptionLength { get; init; }
        public int MinimumStemLength { get; init; }
        public bool RequireQuestionMarkInStem { get; init; }
        public bool RequireExplanation { get; init; }
        public int MaximumExplanationSentenceCount { get; init; }
        public decimal MinimumExplanationSourceOverlapRatio { get; init; }
        public int MinimumGroundedOptionCount { get; init; }
        public decimal MinimumGroundedOptionOverlapRatio { get; init; }
        public List<string> ForbiddenOptionTerms { get; init; } = new();
        public Dictionary<string, string> IssueMessages { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class PeLowValueRulesPolicy
    {
        public List<string> WeakOverrideMethodNames { get; init; } = new();
        public List<string> WeakBehaviorDescriptionTerms { get; init; } = new();
        public List<string> WeakBehaviorMethodNames { get; init; } = new();
        public List<string> StrongBehaviorDescriptionTerms { get; init; } = new();
        public List<string> OrderingBehaviorTerms { get; init; } = new();
        public List<string> OrderingApiTerms { get; init; } = new();
        public List<string> PolymorphismTemplateTerms { get; init; } = new();
        public List<string> FormulaDemoDescriptionTerms { get; init; } = new();
        public List<string> BinaryStatusOutputs { get; init; } = new();
        public List<string> BinaryStatusMethodNames { get; init; } = new();
        public List<string> NonDomainBinaryStatusExclusionTerms { get; init; } = new();
        public List<string> ComparisonStatusOutputs { get; init; } = new();
        public List<string> ComparisonDescriptionTerms { get; init; } = new();
        public List<string> QueueApiCloneCommandTerms { get; init; } = new();
        public List<string> QueueDescriptionTerms { get; init; } = new();
        public List<string> QueueApiSurfaceMethods { get; init; } = new();
        public List<string> QueueCapacityExplicitJustificationTerms { get; init; } = new();
        public List<string> AbstractWeakMethodNames { get; init; } = new();
        public List<DemoFamilyPolicy> DemoFamilies { get; init; } = new();
        public Dictionary<string, string> IssueMessages { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class DemoFamilyPolicy
    {
        public string? FamilyId { get; init; }
        public List<string> RequiredClassTerms { get; init; } = new();
        public bool RequiresAbstractBase { get; init; }
        public List<string> MethodTerms { get; init; } = new();
        public string FamilyIssue { get; init; } = string.Empty;
        public string MethodIssue { get; init; } = string.Empty;
    }

    private sealed class MixedDifficultyScopeAssessment
    {
        public MixedDifficultyScopeAssessment(
            string maxAllowedDifficulty,
            int packedTokenCount,
            decimal coverageRatio,
            int distinctChapterCount,
            IReadOnlyList<string> coveredTopics,
            IReadOnlyList<string> uncoveredTopics,
            IReadOnlyList<string> reasons,
            IReadOnlyList<string> suggestedActions)
        {
            MaxAllowedDifficulty = maxAllowedDifficulty;
            PackedTokenCount = packedTokenCount;
            CoverageRatio = coverageRatio;
            DistinctChapterCount = distinctChapterCount;
            CoveredTopics = coveredTopics;
            UncoveredTopics = uncoveredTopics;
            Reasons = reasons;
            SuggestedActions = suggestedActions;
        }

        public string MaxAllowedDifficulty { get; }
        public int PackedTokenCount { get; }
        public decimal CoverageRatio { get; }
        public int DistinctChapterCount { get; }
        public IReadOnlyList<string> CoveredTopics { get; }
        public IReadOnlyList<string> UncoveredTopics { get; }
        public IReadOnlyList<string> Reasons { get; }
        public IReadOnlyList<string> SuggestedActions { get; }
    }

    private sealed record PromptFootprint(
        int TotalChars,
        int ChunkCount,
        int PreviousQuestionCount,
        int BrokenResponseChars);

    private sealed record GenerationBatchPlan(string Difficulty, int Count, bool IsRefill);
    private sealed class RequestDuplicateTracker
    {
        public HashSet<string> AcceptedFingerprints { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> RejectedFingerprints { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AcceptedTitles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AcceptedStructuralSignatures { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> RejectedStructuralSignatures { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SemanticQuestionEntry> AcceptedSemanticEntries { get; } = new();
        public List<SemanticQuestionEntry> RejectedSemanticEntries { get; } = new();
    }

    private sealed class SemanticQuestionEntry
    {
        public SemanticQuestionEntry(
            GeneratedQuestionCandidateDto question,
            string questionType,
            string fingerprint,
            string text,
            string structuralSignature,
            HashSet<string> keywordSet,
            List<double>? embedding)
        {
            Question = question;
            QuestionType = questionType;
            Fingerprint = fingerprint;
            Text = text;
            StructuralSignature = structuralSignature;
            KeywordSet = keywordSet;
            Embedding = embedding;
        }

        public GeneratedQuestionCandidateDto Question { get; }
        public string QuestionType { get; }
        public string Fingerprint { get; }
        public string Text { get; }
        public string StructuralSignature { get; }
        public HashSet<string> KeywordSet { get; }
        public List<double>? Embedding { get; set; }
    }

}
