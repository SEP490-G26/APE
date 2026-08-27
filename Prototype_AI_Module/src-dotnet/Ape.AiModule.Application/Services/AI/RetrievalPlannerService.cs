using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;
using System.Globalization;

namespace Ape.AiModule.Application.Services.AI;

public sealed class RetrievalPlannerService : IRetrievalPlannerService
{
    private const int MinimumPackedTokenBudget = 512;
    private const int ShortChunkPassthroughTokenThreshold = 96;
    private const int SupportSummaryFloorChars = 140;

    private static readonly IReadOnlyDictionary<string, decimal> ScoringWeights = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["vector"] = 0.35m,
        ["topic"] = 0.20m,
        ["lexical"] = 0.15m,
        ["sectionPriority"] = 0.10m,
        ["assessmentValue"] = 0.10m,
        ["contentQuality"] = 0.05m,
        ["boost"] = 0.05m
    };

    private readonly IModuleRepository _moduleRepository;
    private readonly IContextPackStore _contextPackStore;

    public RetrievalPlannerService(IModuleRepository moduleRepository, IContextPackStore contextPackStore)
    {
        _moduleRepository = moduleRepository;
        _contextPackStore = contextPackStore;
    }

    public async Task<RetrievalPlanResult> PlanAsync(RetrievalPlanRequest request, CancellationToken cancellationToken)
    {
        if (request.UseCachedPack && !request.ForceRebuildPack)
        {
            var cached = await TryReuseCachedPackAsync(request, cancellationToken);
            if (cached is not null)
            {
                return BuildCachedResult(request, cached);
            }
        }

        var resolvedChunks = await ResolveSourceChunksAsync(request, cancellationToken);
        if (resolvedChunks.Count == 0)
        {
            throw new InvalidOperationException("Retrieval planner could not find any chunks for the requested scope.");
        }

        var plan = BuildPlan(request, resolvedChunks, includeChunkText: request.IncludeChunkText);
        await _contextPackStore.SaveAsync(plan.ContextPack, cancellationToken);
        return plan;
    }

    public async Task<RetrievalPlanDebugResult> PlanDebugAsync(RetrievalPlanRequest request, CancellationToken cancellationToken)
    {
        var resolvedChunks = await ResolveSourceChunksAsync(request, cancellationToken);
        if (resolvedChunks.Count == 0)
        {
            throw new InvalidOperationException("Retrieval planner could not find any chunks for the requested scope.");
        }

        var debugPlan = BuildPlan(request, resolvedChunks, includeChunkText: true, includeDebugPools: true);
        await _contextPackStore.SaveAsync(debugPlan.Plan.ContextPack, cancellationToken);
        return debugPlan;
    }

    public async Task<ContextPackBuildResult> BuildAsync(ContextPackBuildRequest request, CancellationToken cancellationToken)
    {
        if (request.ChunkIds is null || request.ChunkIds.Count == 0)
        {
            throw new InvalidOperationException("Context pack build requires at least one chunk id.");
        }

        var chunks = await _moduleRepository.SearchKnowledgeChunksAsync(
            request.CourseId,
            request.DocumentId,
            request.Subject,
            request.UserId,
            "HYBRID",
            cancellationToken);

        var selected = chunks
            .Where(chunk => request.ChunkIds.Contains(chunk.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (selected.Count == 0)
        {
            throw new InvalidOperationException("Context pack build could not resolve any chunk from the provided chunk ids.");
        }

        var normalizedRequest = new RetrievalPlanRequest(
            request.UserId,
            request.CourseId,
            request.DocumentId,
            request.Subject,
            request.QuestionType,
            request.Difficulty,
            Math.Max(1, InferRecommendedQuestionCount(request.GenerationMode)),
            request.GenerationMode,
            request.TargetTopics,
            null,
            "HYBRID",
            null,
            false,
            true,
            true,
            selected.Count,
            request.MaxPackedTokens,
            request.Notes,
            selected.Select(MapChunkToContext).ToList());

        var built = BuildPlan(normalizedRequest, selected, includeChunkText: true);
        var persisted = await _contextPackStore.SaveAsync(built.ContextPack with
        {
            PackType = request.PackType,
            PackStrategy = request.PackStrategy
        }, cancellationToken);

        return new ContextPackBuildResult(
            persisted,
            built.StageLogs,
            built.UsageLogs,
            built.Totals);
    }

    private async Task<AIContextPack?> TryReuseCachedPackAsync(RetrievalPlanRequest request, CancellationToken cancellationToken)
    {
        var candidates = await _contextPackStore.ListAsync(
            request.Subject,
            request.QuestionType,
            request.Difficulty,
            request.TargetTopics.FirstOrDefault(),
            "active",
            20,
            cancellationToken);

        var match = candidates.FirstOrDefault(candidate =>
            candidate.TargetTopics.Count == request.TargetTopics.Count
            && candidate.TargetTopics.All(topic => request.TargetTopics.Contains(topic, StringComparer.OrdinalIgnoreCase)));

        if (match is null)
        {
            return null;
        }

        return await _contextPackStore.TouchUsageAsync(match.PackId, cancellationToken);
    }

    private async Task<IReadOnlyList<KnowledgeChunk>> ResolveSourceChunksAsync(RetrievalPlanRequest request, CancellationToken cancellationToken)
    {
        if (request.SeedChunks is { Count: > 0 })
        {
            return request.SeedChunks.Select((chunk, index) => MapSeedChunk(request, chunk, index)).ToList();
        }

        return await _moduleRepository.SearchKnowledgeChunksAsync(
            request.CourseId,
            request.DocumentId,
            request.Subject,
            request.UserId,
            request.SourceScope,
            cancellationToken);
    }

    private RetrievalPlanResult BuildCachedResult(RetrievalPlanRequest request, AIContextPack cachedPack)
    {
        var cost = ZeroCost();
        var stageLogs = new List<PipelineStageLog>
        {
            new(
                "retrieval-plan",
                RunStatus.Completed,
                cost,
                new Dictionary<string, string>
                {
                    ["used_cached_pack"] = true.ToString(),
                    ["pack_id"] = cachedPack.PackId,
                    ["selected_chunk_count"] = cachedPack.SourceChunkIds.Count.ToString(),
                    ["token_count"] = cachedPack.TokenCount.ToString()
                },
                new NormalizedModelFields("system", "retrieval-planner", null, null, null, null, null, null))
        };

        var usageLogs = new List<AIUsageLogEntry>
        {
            CreateUsageLog(request.UserId, "AI_RetrievalPlanner", "retrieval_plan", 1, "system", "retrieval-planner", cost, "cached_pack", new
            {
                request.Subject,
                request.QuestionType,
                request.Difficulty,
                request.TargetTopics,
                cachedPack.PackId
            })
        };

        return new RetrievalPlanResult(
            $"rp-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..36],
            cachedPack.PackId,
            true,
            request.Subject,
            request.QuestionType,
            request.Difficulty,
            request.GenerationMode,
            request.TargetTopics,
            request.RetrievalQuery,
            new RetrievalTokenBudget(request.MaxPackedTokens, cachedPack.TokenCount, cachedPack.SourceTokenCount, cachedPack.CompressionRatio),
            new RetrievalCandidateStats(cachedPack.SourceChunkIds.Count, cachedPack.SourceChunkIds.Count, cachedPack.RetrievedChunks.Count, cachedPack.RetrievedChunks.Count, cachedPack.RetrievedChunks.Count, cachedPack.Chunks.Count),
            cachedPack.RetrievedChunks,
            cachedPack,
            stageLogs,
            usageLogs,
            cost);
    }

    private RetrievalPlanResult BuildPlan(RetrievalPlanRequest request, IReadOnlyList<KnowledgeChunk> sourceChunks, bool includeChunkText)
        => BuildPlanInternal(request, sourceChunks, includeChunkText, includeDebugPools: false).Plan;

    private RetrievalPlanDebugResult BuildPlan(RetrievalPlanRequest request, IReadOnlyList<KnowledgeChunk> sourceChunks, bool includeChunkText, bool includeDebugPools)
        => BuildPlanInternal(request, sourceChunks, includeChunkText, includeDebugPools);

    private RetrievalPlanDebugResult BuildPlanInternal(RetrievalPlanRequest request, IReadOnlyList<KnowledgeChunk> sourceChunks, bool includeChunkText, bool includeDebugPools)
    {
        var targetTopics = request.TargetTopics?.Where(static item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        var preFiltered = sourceChunks
            .Where(chunk => chunk.RetrievalEnabled)
            .Where(chunk => string.Equals(NormalizeSubject(chunk.SubjectCode), NormalizeSubject(request.Subject), StringComparison.OrdinalIgnoreCase))
            .Where(chunk => string.IsNullOrWhiteSpace(request.Language) || string.Equals(chunk.Language, request.Language, StringComparison.OrdinalIgnoreCase) || string.Equals(chunk.Language, "mixed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var topicCandidates = preFiltered
            .Where(chunk => ComputeTopicScore(chunk, targetTopics, request.PreferredChapterIds) > 0m)
            .ToList();

        var ranked = preFiltered
            .Select(chunk => BuildRetrievedChunkScore(chunk, targetTopics, request.PreferredChapterIds, request.RetrievalQuery, includeChunkText))
            .OrderByDescending(static item => item.FinalScore)
            .ThenBy(static item => item.TokenCount)
            .Take(Math.Max(1, request.MaxCandidateCount))
            .ToList();

        var selected = PackChunks(ranked, request.MaxPackedTokens, request.GenerationMode);
        if (selected.Count == 0)
        {
            selected = ranked.Take(Math.Min(3, ranked.Count)).ToList();
        }

        var selectedChunkIds = selected.Select(static item => item.ChunkId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var chunkById = preFiltered.ToDictionary(static chunk => chunk.Id, StringComparer.OrdinalIgnoreCase);
        var selectedSourceChunks = selected
            .Where(item => chunkById.ContainsKey(item.ChunkId))
            .Select(item => chunkById[item.ChunkId])
            .ToList();

        var packedSnapshots = BuildPackedSnapshots(selected, selectedSourceChunks, request.GenerationMode, request.MaxPackedTokens);
        var sourceTokenCount = EstimateChunkPayloadTokens(selectedSourceChunks.Select(MapChunkToContext));
        var packedTokenCount = EstimateChunkPayloadTokens(packedSnapshots.Select(MapSnapshotToContext));
        var summaryText = BuildPackedSummary(selected, selectedSourceChunks);
        var contextText = BuildPackedContext(packedSnapshots);
        var packId = BuildPackId(request, targetTopics);
        var now = DateTimeOffset.UtcNow;
        var recommendedQuestionCount = Math.Max(1, InferRecommendedQuestionCount(request.GenerationMode));
        var maxQuestionCount = Math.Max(recommendedQuestionCount, recommendedQuestionCount + 2);
        var pack = new AIContextPack(
            packId,
            "generation_context",
            "summary_first",
            "active",
            request.UserId,
            request.CourseId,
            request.DocumentId,
            NormalizeSubject(request.Subject),
            request.QuestionType,
            request.Difficulty,
            targetTopics,
            request.SourceScope,
            request.RetrievalQuery,
            selectedSourceChunks.Select(static chunk => chunk.Id).ToList(),
            selected,
            packedSnapshots,
            summaryText,
            contextText,
            packedTokenCount,
            sourceTokenCount,
            sourceTokenCount == 0 ? 1m : Math.Round((decimal)packedTokenCount / sourceTokenCount, 4),
            recommendedQuestionCount,
            maxQuestionCount,
            0,
            null,
            now.AddDays(30),
            now,
            now);

        var totals = ZeroCost();
        var stageLogs = new List<PipelineStageLog>
        {
            new(
                "retrieval-plan",
                RunStatus.Completed,
                totals,
                new Dictionary<string, string>
                {
                    ["candidate_count"] = ranked.Count.ToString(),
                    ["selected_chunk_count"] = selected.Count.ToString(),
                    ["pack_id"] = pack.PackId,
                    ["pack_tokens"] = pack.TokenCount.ToString(),
                    ["source_tokens"] = pack.SourceTokenCount.ToString(),
                    ["compression_ratio"] = pack.CompressionRatio.ToString(CultureInfo.InvariantCulture)
                },
                new NormalizedModelFields("system", "retrieval-planner", null, null, null, null, null, null))
        };

        var usageLogs = new List<AIUsageLogEntry>
        {
            CreateUsageLog(request.UserId, "AI_RetrievalPlanner", "retrieval_plan", 1, "system", "retrieval-planner", totals, "planned", new
            {
                request.Subject,
                request.QuestionType,
                request.Difficulty,
                request.TargetTopics,
                selected = selected.Select(static item => item.ChunkId).ToList(),
                pack.PackId,
                pack.TokenCount,
                pack.SourceTokenCount
            })
        };

        var result = new RetrievalPlanResult(
            $"rp-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..36],
            pack.PackId,
            false,
            NormalizeSubject(request.Subject),
            request.QuestionType,
            request.Difficulty,
            request.GenerationMode,
            targetTopics,
            request.RetrievalQuery,
            new RetrievalTokenBudget(request.MaxPackedTokens, pack.TokenCount, pack.SourceTokenCount, pack.CompressionRatio),
            new RetrievalCandidateStats(
                preFiltered.Count,
                topicCandidates.Count,
                Math.Min(preFiltered.Count, Math.Max(10, request.MaxCandidateCount / 2)),
                Math.Min(preFiltered.Count, Math.Max(10, request.MaxCandidateCount / 2)),
                ranked.Count,
                selected.Count),
            selected,
            pack,
            stageLogs,
            usageLogs,
            totals);

        var filters = new Dictionary<string, string?>
        {
            ["courseId"] = request.CourseId,
            ["documentId"] = request.DocumentId,
            ["subject"] = NormalizeSubject(request.Subject),
            ["questionType"] = request.QuestionType,
            ["difficulty"] = request.Difficulty,
            ["sourceScope"] = request.SourceScope,
            ["language"] = request.Language
        };

        var candidatePools = includeDebugPools
            ? new Dictionary<string, IReadOnlyList<string>>
            {
                ["topicCandidates"] = topicCandidates.Select(static chunk => chunk.Id).Take(20).ToList(),
                ["vectorCandidates"] = ranked.OrderByDescending(static item => item.VectorScore).Select(static item => item.ChunkId).Take(20).ToList(),
                ["lexicalCandidates"] = ranked.OrderByDescending(static item => item.LexicalScore).Select(static item => item.ChunkId).Take(20).ToList()
            }
            : new Dictionary<string, IReadOnlyList<string>>();

        var rejected = ranked
            .Where(item => !selectedChunkIds.Contains(item.ChunkId))
            .Take(20)
            .Select(item => new RejectedChunkInfo(item.ChunkId, item.TokenCount > request.MaxPackedTokens ? "token_budget_overflow" : "lower_ranked_candidate"))
            .ToList();

        return new RetrievalPlanDebugResult(result, filters, candidatePools, rejected, ScoringWeights);
    }

    private static RetrievedChunkScore BuildRetrievedChunkScore(
        KnowledgeChunk chunk,
        IReadOnlyList<string> targetTopics,
        IReadOnlyList<string>? preferredChapterIds,
        string? retrievalQuery,
        bool includeChunkText)
    {
        var topicScore = ComputeTopicScore(chunk, targetTopics, preferredChapterIds);
        var lexicalScore = ComputeLexicalScore(chunk, retrievalQuery, targetTopics);
        var vectorScore = Math.Round((topicScore * 0.6m) + (lexicalScore * 0.4m), 4);
        var sectionPriority = ComputeSectionPriority(chunk, preferredChapterIds);
        var finalScore = Math.Round(
            (vectorScore * ScoringWeights["vector"])
            + (topicScore * ScoringWeights["topic"])
            + (lexicalScore * ScoringWeights["lexical"])
            + (sectionPriority * ScoringWeights["sectionPriority"])
            + (chunk.AssessmentValueScore * ScoringWeights["assessmentValue"])
            + (chunk.ContentQualityScore * ScoringWeights["contentQuality"])
            + (chunk.RetrievalScoreBoost * ScoringWeights["boost"]), 4);

        return new RetrievedChunkScore(
            chunk.Id,
            chunk.ChunkIndex,
            chunk.SectionTitle,
            chunk.ChapterTitle,
            chunk.TopicPrimary ?? chunk.TopicTags.FirstOrDefault(),
            chunk.TopicTags,
            Math.Max(1, chunk.TokenCount),
            vectorScore,
            lexicalScore,
            topicScore,
            finalScore,
            "candidate",
            includeChunkText ? chunk.NormalizedText : null);
    }

    private static List<RetrievedChunkScore> PackChunks(IReadOnlyList<RetrievedChunkScore> ranked, int maxPackedTokens, string generationMode)
    {
        var selected = new List<RetrievedChunkScore>();
        var tokenBudget = Math.Max(MinimumPackedTokenBudget, maxPackedTokens);
        var runningTokens = 0;
        var seenTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in ranked)
        {
            if (runningTokens + candidate.TokenCount > tokenBudget && selected.Count > 0)
            {
                continue;
            }

            var role = selected.Count switch
            {
                0 => "primary",
                _ when !string.IsNullOrWhiteSpace(candidate.TopicPrimary) && seenTopics.Add(candidate.TopicPrimary) => "supporting",
                _ => "supporting"
            };

            selected.Add(candidate with { SelectionRole = role });
            runningTokens += candidate.TokenCount;

            if (string.Equals(generationMode, "single_question_precise", StringComparison.OrdinalIgnoreCase) && selected.Count >= 3)
            {
                break;
            }

            if (string.Equals(generationMode, "small_batch", StringComparison.OrdinalIgnoreCase) && selected.Count >= 6)
            {
                break;
            }
        }

        return selected;
    }

    private static IReadOnlyList<ChunkContextSnapshot> BuildPackedSnapshots(
        IReadOnlyList<RetrievedChunkScore> selected,
        IReadOnlyList<KnowledgeChunk> selectedSourceChunks,
        string generationMode,
        int maxPackedTokens)
    {
        if (selected.Count == 0 || selectedSourceChunks.Count == 0)
        {
            return [];
        }

        var scoreByChunkId = selected.ToDictionary(static item => item.ChunkId, StringComparer.OrdinalIgnoreCase);
        var tokenBudget = Math.Max(MinimumPackedTokenBudget, maxPackedTokens);
        var primaryTarget = ResolvePrimaryTokenTarget(generationMode, tokenBudget);
        var remainingBudget = Math.Max(128, tokenBudget - primaryTarget);
        var supportCount = Math.Max(0, selectedSourceChunks.Count - 1);
        var perSupportTarget = supportCount == 0 ? 0 : Math.Max(96, remainingBudget / supportCount);

        var snapshots = new List<ChunkContextSnapshot>(selectedSourceChunks.Count);
        for (var i = 0; i < selectedSourceChunks.Count; i++)
        {
            var chunk = selectedSourceChunks[i];
            var score = scoreByChunkId[chunk.Id];
            var targetTokens = i == 0 ? primaryTarget : perSupportTarget;
            var content = BuildPackedChunkContent(chunk, score, targetTokens, i == 0);
            snapshots.Add(new ChunkContextSnapshot(chunk.Id, content, chunk.TopicTags, chunk.SectionTitle, chunk.Language));
        }

        return snapshots;
    }

    private static string BuildPackedChunkContent(KnowledgeChunk chunk, RetrievedChunkScore score, int targetTokens, bool isPrimary)
    {
        var sourceText = chunk.MarkdownText;
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            sourceText = chunk.NormalizedText;
        }

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            sourceText = chunk.RawText;
        }

        sourceText = (sourceText ?? string.Empty).Trim();
        var summary = chunk.ChunkSummary ?? BuildChunkSummaryFromText(sourceText);
        var sourceTokens = EstimateTokens(sourceText);

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return summary;
        }

        if (sourceTokens <= Math.Min(ShortChunkPassthroughTokenThreshold, Math.Max(48, targetTokens)))
        {
            return sourceText;
        }

        if (!isPrimary && sourceTokens <= Math.Max(64, targetTokens - 12))
        {
            return sourceText;
        }

        if (isPrimary && sourceTokens <= Math.Max(72, targetTokens + 8))
        {
            return sourceText;
        }

        var lines = new List<string>
        {
            $"Section: {chunk.SectionTitle ?? chunk.ChapterTitle ?? "Untitled"}",
            $"Topic: {chunk.TopicPrimary ?? score.TopicPrimary ?? chunk.TopicTags.FirstOrDefault() ?? "general"}"
        };

        if (!isPrimary && chunk.TopicTags.Count > 0)
        {
            lines.Add($"Tags: {string.Join(", ", chunk.TopicTags.Take(4))}");
        }

        if (!isPrimary && chunk.ConceptKeywords is { Count: > 0 })
        {
            lines.Add($"Keywords: {string.Join(", ", chunk.ConceptKeywords.Take(5))}");
        }

        var textBudget = Math.Max(80, targetTokens - EstimateTokens(string.Join("\n", lines)));
        if (isPrimary)
        {
            var primaryBody = TrimToChars(sourceText, textBudget * 4);
            if (!string.IsNullOrWhiteSpace(primaryBody))
            {
                lines.Add(primaryBody);
            }
            else if (!string.IsNullOrWhiteSpace(summary))
            {
                lines.Add(summary);
            }
        }
        else
        {
            var supportSummary = TrimToChars(summary, Math.Max(SupportSummaryFloorChars, textBudget * 3));
            if (!string.IsNullOrWhiteSpace(supportSummary))
            {
                lines.Add(supportSummary);
            }

            if (!string.IsNullOrWhiteSpace(sourceText) && supportSummary.Length < sourceText.Length)
            {
                var trailing = BuildTrailingSnippet(sourceText, supportSummary, Math.Max(80, textBudget));
                if (!string.IsNullOrWhiteSpace(trailing))
                {
                    lines.Add($"Cue: {trailing}");
                }
            }
        }

        return string.Join("\n", lines).Trim();
    }

    private static string BuildTrailingSnippet(string fullText, string usedText, int charBudget)
    {
        if (string.IsNullOrWhiteSpace(fullText) || charBudget <= 0)
        {
            return string.Empty;
        }

        var normalized = fullText.Replace("\r", " ").Replace("\n", " ").Trim();
        if (normalized.Length <= usedText.Length + 20)
        {
            return string.Empty;
        }

        var start = Math.Min(normalized.Length - 1, Math.Max(usedText.Length, normalized.Length / 2));
        var length = Math.Min(charBudget, normalized.Length - start);
        return TrimToChars(normalized.Substring(start, length), charBudget);
    }

    private static int ResolvePrimaryTokenTarget(string generationMode, int tokenBudget)
        => generationMode.Trim().ToLowerInvariant() switch
        {
            "single_question_precise" => Math.Min(tokenBudget - 128, Math.Max(320, (int)(tokenBudget * 0.55))),
            "small_batch" => Math.Min(tokenBudget - 160, Math.Max(280, (int)(tokenBudget * 0.40))),
            "coverage_exam" => Math.Min(tokenBudget - 192, Math.Max(256, (int)(tokenBudget * 0.32))),
            _ => Math.Min(tokenBudget - 128, Math.Max(300, (int)(tokenBudget * 0.45)))
        };

    private static string BuildPackedSummary(IReadOnlyList<RetrievedChunkScore> selected, IReadOnlyList<KnowledgeChunk> chunks)
    {
        var byId = chunks.ToDictionary(static chunk => chunk.Id, StringComparer.OrdinalIgnoreCase);
        return string.Join("\n", selected.Select(score =>
        {
            var chunk = byId[score.ChunkId];
            var summary = chunk.ChunkSummary ?? BuildChunkSummaryFromText(chunk.NormalizedText);
            return $"- [{score.SelectionRole}] {chunk.SectionTitle ?? chunk.ChapterTitle ?? chunk.Id} | topics: {string.Join(", ", chunk.TopicTags.Take(6))} | summary: {summary}";
        }));
    }

    private static string BuildPackedContext(IReadOnlyList<ChunkContextSnapshot> chunks)
        => string.Join("\n\n", chunks.Select(chunk => $"[Chunk {chunk.ChunkId}]\nSection: {chunk.SectionTitle ?? "Untitled"}\nTopics: {string.Join(", ", chunk.TopicTags)}\nContent:\n{chunk.ContentText}"));

    private static ChunkContextDto MapChunkToContext(KnowledgeChunk chunk)
        => new(chunk.Id, chunk.NormalizedText, chunk.TopicTags, chunk.SectionTitle, chunk.Language);

    private static ChunkContextDto MapSnapshotToContext(ChunkContextSnapshot chunk)
        => new(chunk.ChunkId, chunk.ContentText, chunk.TopicTags, chunk.SectionTitle, chunk.Language);

    private static KnowledgeChunk MapSeedChunk(RetrievalPlanRequest request, ChunkContextDto chunk, int index)
    {
        var normalizedSubject = NormalizeSubject(request.Subject);
        var normalizedText = chunk.ContentText?.Trim() ?? string.Empty;
        var tokenCount = EstimateTokens(normalizedText);
        var now = DateTimeOffset.UtcNow;
        var topicPrimary = chunk.TopicTags.FirstOrDefault();

        return new KnowledgeChunk(
            chunk.ChunkId,
            request.CourseId,
            request.DocumentId ?? $"seed-doc-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            request.UserId,
            null,
            null,
            index,
            "hybrid_contextual",
            "paragraph",
            chunk.SectionTitle,
            "json",
            request.DocumentId ?? "seed-json",
            index + 1,
            index + 1,
            chunk.Language,
            normalizedSubject,
            normalizedText,
            normalizedText,
            normalizedText,
            chunk.TopicTags,
            Array.Empty<float>(),
            "seed",
            "seed",
            0,
            "seed",
            "seed",
            CountWords(normalizedText),
            tokenCount,
            normalizedText.Length,
            true,
            "active",
            now,
            now,
            ChapterId: null,
            ChapterTitle: chunk.SectionTitle,
            ChunkPath: chunk.SectionTitle is null ? [] : [chunk.SectionTitle],
            SourceBlockRefs: [],
            SyllabusScope: request.SourceScope,
            ChunkSummary: BuildChunkSummaryFromText(normalizedText),
            ConceptKeywords: chunk.TopicTags,
            TopicPrimary: topicPrimary,
            TopicSecondary: chunk.TopicTags.Skip(1).ToList(),
            PrerequisiteTags: [],
            EstimatedDifficulty: request.Difficulty,
            AssessmentValueScore: 0.7m,
            ContentQualityScore: 0.8m,
            RetrievalScoreBoost: 0m,
            DuplicateGroupId: null);
    }

    private static decimal ComputeTopicScore(KnowledgeChunk chunk, IReadOnlyList<string> targetTopics, IReadOnlyList<string>? preferredChapterIds)
    {
        if (targetTopics.Count == 0)
        {
            return 0.5m;
        }

        var chunkTags = chunk.TopicTags.Select(static item => item.Trim()).Where(static item => !string.IsNullOrWhiteSpace(item)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overlap = targetTopics.Count(topic => chunkTags.Contains(topic));
        var score = chunkTags.Count == 0 ? 0m : decimal.Round((decimal)overlap / targetTopics.Count, 4);
        if (!string.IsNullOrWhiteSpace(chunk.TopicPrimary) && targetTopics.Contains(chunk.TopicPrimary, StringComparer.OrdinalIgnoreCase))
        {
            score = Math.Min(1m, score + 0.2m);
        }

        if (preferredChapterIds is { Count: > 0 } && !string.IsNullOrWhiteSpace(chunk.ChapterId) && preferredChapterIds.Contains(chunk.ChapterId, StringComparer.OrdinalIgnoreCase))
        {
            score = Math.Min(1m, score + 0.1m);
        }

        return score;
    }

    private static decimal ComputeLexicalScore(KnowledgeChunk chunk, string? retrievalQuery, IReadOnlyList<string> targetTopics)
    {
        var probe = string.Join(' ', targetTopics.Append(retrievalQuery ?? string.Empty));
        var left = Tokenize(probe);
        var right = Tokenize($"{chunk.SectionTitle} {chunk.ChapterTitle} {chunk.NormalizedText}");
        if (left.Count == 0 || right.Count == 0)
        {
            return 0m;
        }

        var intersection = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Count();
        var union = left.Union(right, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0m : decimal.Round((decimal)intersection / union, 4);
    }

    private static decimal ComputeSectionPriority(KnowledgeChunk chunk, IReadOnlyList<string>? preferredChapterIds)
    {
        var score = 0.4m;
        if (!string.IsNullOrWhiteSpace(chunk.SectionTitle))
        {
            score += 0.2m;
        }

        if (preferredChapterIds is { Count: > 0 } && !string.IsNullOrWhiteSpace(chunk.ChapterId) && preferredChapterIds.Contains(chunk.ChapterId, StringComparer.OrdinalIgnoreCase))
        {
            score += 0.2m;
        }

        if (string.Equals(chunk.ChunkType, "code", StringComparison.OrdinalIgnoreCase)
            || string.Equals(chunk.ChunkType, "example", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.1m;
        }

        return Math.Min(1m, score);
    }

    private static string BuildPackId(RetrievalPlanRequest request, IReadOnlyList<string> targetTopics)
    {
        var topicPart = targetTopics.Count == 0 ? "general" : string.Join('-', targetTopics.Select(NormalizeSlug).Take(3));
        var value = $"pack-{NormalizeSlug(NormalizeSubject(request.Subject))}-{NormalizeSlug(request.QuestionType)}-{NormalizeSlug(request.Difficulty)}-{topicPart}-{Guid.NewGuid():N}";
        return value[..Math.Min(80, value.Length)];
    }

    private static string BuildChunkSummaryFromText(string text)
    {
        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (normalized.Length <= 220)
        {
            return normalized;
        }

        return normalized[..220].TrimEnd() + "...";
    }

    private static string TrimToChars(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text) || maxChars <= 0)
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (normalized.Length <= maxChars)
        {
            return normalized;
        }

        return normalized[..maxChars].TrimEnd() + "...";
    }

    private static int InferRecommendedQuestionCount(string generationMode)
        => generationMode.Trim().ToLowerInvariant() switch
        {
            "single_question_precise" => 1,
            "small_batch" => 3,
            "coverage_exam" => 5,
            _ => 2
        };

    private static int EstimateTokens(string text)
        => (int)Math.Max(1, Math.Ceiling((text?.Length ?? 0) / 4.0));

    private static int EstimateChunkPayloadTokens(IEnumerable<ChunkContextDto> chunks)
        => EstimateTokens(System.Text.Json.JsonSerializer.Serialize(chunks));

    private static int CountWords(string value)
        => string.IsNullOrWhiteSpace(value)
            ? 0
            : value.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

    private static HashSet<string> Tokenize(string value)
    {
        var normalized = new string((value ?? string.Empty)
            .ToLowerInvariant()
            .Where(static ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            .ToArray());

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeSubject(string subject)
    {
        var lowered = subject.Trim().ToLowerInvariant();
        if (lowered.Contains("java") && lowered.Contains("oop"))
        {
            return "JAVA_OOP";
        }

        if (lowered.Contains("dsa") || lowered.Contains("data structure") || lowered.Contains("algorithm"))
        {
            return "DSA_JAVA";
        }

        if (lowered.Contains("c_basic"))
        {
            return "C";
        }

        return lowered switch
        {
            "c" => "C",
            _ => subject.Trim().ToUpperInvariant()
        };
    }

    private static string NormalizeSlug(string value)
        => new string((value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Replace("--", "-")
            .Trim('-');

    private static TokenCostBreakdown ZeroCost()
        => new(0, 0, 0, 0, 0, 0);

    private static AIUsageLogEntry CreateUsageLog(
        string triggeredBy,
        string agentId,
        string stageName,
        int attemptIndex,
        string provider,
        string modelName,
        TokenCostBreakdown cost,
        string status,
        object payloadData)
        => new(
            $"usage-{Guid.NewGuid():N}"[..24],
            triggeredBy,
            agentId,
            $"retrieval-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40],
            stageName,
            attemptIndex,
            provider,
            modelName,
            0,
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
            new NormalizedModelFields(provider, modelName, null, null, null, null, null, null),
            cost.Error,
            cost.UsageCapture?.RawUsageJson);
}
