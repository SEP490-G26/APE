/**
 * AIEmbeddingTaggingService.cs
 * Implementation of Step 3 in the APE AI Pipeline: Semantic Chunking, Vector Embedding, and Automated Taxonomy Tagging.
 * 
 * Responsibilities:
 * 1. Takes normalized document content and splits it into semantic chunks (500-1000 tokens) with overlap.
 * 2. Batches chunks and calls Cohere Embedding Client (embed-multilingual-v3.0) to generate 1024-dim vectors.
 * 3. Calls Tagging Agent (DeepSeek / OpenAI fallback) to attach academic taxonomy tags to chunks.
 * 4. Produces DocumentChunk entities ready for persistence in MongoDB.
 */

using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Diagnostics;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Options;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

/// <summary>
/// Service that coordinates chunk creation, vector embedding calculation, and AI-assisted topic tagging.
/// </summary>
public class AIEmbeddingTaggingService : IAIEmbeddingTaggingService
{
    private const int DenseChunkSecondarySplitTokenThreshold = 850;
    private const int DenseChunkSecondarySplitCharacterThreshold = 3200;
    private const int DenseChunkMinimumSegmentCharacterThreshold = 260;
    private const int TaggingBatchSize = 20;
    private const int MaxConcurrentTaggingBatches = 2;
    private readonly IAIExecutionService _aiExecutionService;
    private readonly IAIFeatureRoutingService _routingService;
    private readonly IAITagTaxonomyProvider _tagTaxonomyProvider;
    private readonly AIOptions _options;
    private readonly ILogger<AIEmbeddingTaggingService> _logger;

    public AIEmbeddingTaggingService(
        IAIExecutionService aiExecutionService,
        IAIFeatureRoutingService routingService,
        IAITagTaxonomyProvider tagTaxonomyProvider,
        IOptions<AIOptions> options,
        ILogger<AIEmbeddingTaggingService> logger)
    {
        _aiExecutionService = aiExecutionService;
        _routingService = routingService;
        _tagTaxonomyProvider = tagTaxonomyProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Processes a full document into vector-embedded and taxonomy-tagged chunks.
    /// </summary>
    /// <param name="documentId">MongoDB ID of the parent document.</param>
    /// <param name="courseId">Associated course ID.</param>
    /// <param name="userId">Uploader user ID.</param>
    /// <param name="content">Clean markdown text of the document.</param>
    /// <param name="sourceType">Source format (pdf, docx, pptx).</param>
    /// <param name="subjectCode">Academic subject code (e.g., PRO192, PRN211).</param>
    /// <param name="structuralHints">Detected chapter and topic structural boundaries.</param>
    /// <param name="embeddingOverride">Optional override for embedding provider/model.</param>
    /// <param name="taggingOverride">Optional override for tagging provider/model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="EmbeddingTaggingRunResult"/> containing generated <see cref="DocumentChunk"/> list and token metrics.</returns>
    public async Task<EmbeddingTaggingRunResult> CreateChunksAsync(
        string documentId,
        string courseId,
        string userId,
        string content,
        string sourceType,
        string? subjectCode = null,
        ExtractionStructuralHintsDto? structuralHints = null,
        AIRuntimeOverride? embeddingOverride = null,
        AIRuntimeOverride? taggingOverride = null,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stageStopwatch = Stopwatch.StartNew();
        var blocks = ChunkContent(content, structuralHints);
        var chunkingMs = stageStopwatch.ElapsedMilliseconds;
        if (blocks.Count == 0)
        {
            return new EmbeddingTaggingRunResult();
        }

        var embeddingRoute = _routingService.ResolveEmbedding(AIFeatureNames.Embedding, embeddingOverride);
        AIEmbeddingResponse? embeddingResponse = null;

        var embeddings = new List<List<double>>(Enumerable.Repeat(new List<double>(), blocks.Count));
        if (_options.Enabled)
        {
            try
            {
                stageStopwatch.Restart();
                embeddingResponse = await _aiExecutionService.ExecuteEmbeddingAsync(
                    embeddingRoute,
                    new AIEmbeddingRequest
                    {
                        FeatureName = AIFeatureNames.Embedding,
                        Provider = embeddingRoute.Provider,
                        Model = embeddingRoute.Model,
                        Inputs = blocks.Select(block => block.Content).ToList(),
                        RuntimeOverride = embeddingOverride
                    },
                    cancellationToken);

                if (embeddingResponse.Embeddings.Count == blocks.Count)
                {
                    embeddings = embeddingResponse.Embeddings;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding generation failed. Falling back to empty vectors.");
            }
        }
        var embeddingMs = stageStopwatch.ElapsedMilliseconds;

        var taggingRoute = _routingService.ResolveText(AIFeatureNames.AutoTagging, taggingOverride);
        var tagResults = new List<List<string>>(Enumerable.Repeat(new List<string>(), blocks.Count));
        var taggingResponses = new ConcurrentBag<AITextResponse>();
        var taxonomyCatalog = _tagTaxonomyProvider.GetCatalog();
        var resolvedSubjectCode = ResolveSubjectCode(subjectCode, content, taxonomyCatalog);
        var taxonomy = taxonomyCatalog.ResolveBySubject(resolvedSubjectCode);
        var allowedTags = taxonomy?.CanonicalTags;
        var taggingBatches = blocks
            .Select((block, index) => new IndexedChunkDraft(index, block))
            .Chunk(TaggingBatchSize)
            .Select(batch => batch.ToList())
            .ToList();
        var taggingResults = new ConcurrentBag<BatchTagGenerationResult>();

        stageStopwatch.Restart();
        await Parallel.ForEachAsync(
            taggingBatches,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrentTaggingBatches,
                CancellationToken = cancellationToken
            },
            async (batch, ct) =>
            {
                var taggingResult = await GenerateTagsBatchAsync(
                    batch,
                    taggingRoute,
                    allowedTags,
                    resolvedSubjectCode,
                    taxonomyCatalog,
                    ct);

                if (taggingResult.Response is not null)
                {
                    taggingResponses.Add(taggingResult.Response);
                }

                taggingResults.Add(taggingResult);
            });
        var taggingMs = stageStopwatch.ElapsedMilliseconds;
        var taggingFallbackCalls = 0;
        foreach (var taggingResult in taggingResults)
        {
            taggingFallbackCalls += taggingResult.FallbackChunkCount;
            foreach (var item in taggingResult.Items)
            {
                tagResults[item.Index] = item.Tags;
            }
        }

        var chunks = new List<KnowledgeChunk>();
        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];
            var inferredSubjectCode = resolvedSubjectCode ?? InferSubjectCode(block.Content, block.SectionTitle, block.ChapterTitle);
            chunks.Add(new KnowledgeChunk
            {
                DocumentId = documentId,
                CourseId = courseId,
                UserId = userId,
                ChunkIndex = index,
                ChunkingStrategy = "logical_block",
                ChunkType = block.ChunkType,
                SectionTitle = block.SectionTitle,
                ChapterKey = block.ChapterKey,
                ChapterTitle = block.ChapterTitle,
                ChapterOrder = block.ChapterOrder,
                SectionPath = block.SectionPath.ToList(),
                SectionLevel = block.SectionLevel,
                SourceType = sourceType,
                SourceName = null,
                SourcePageFrom = null,
                SourcePageTo = null,
                SubjectCode = inferredSubjectCode,
                RawText = block.Content,
                NormalizedText = block.Content,
                MarkdownText = block.Content,
                TopicTags = tagResults[index],
                QuestionTypeAffinity = block.QuestionTypeAffinity.ToList(),
                Embedding = index < embeddings.Count ? embeddings[index] : new List<double>(),
                EmbeddingProvider = embeddingRoute.Provider,
                EmbeddingModel = embeddingRoute.Model,
                EmbeddingDim = index < embeddings.Count ? embeddings[index].Count : 0,
                TaggingProvider = taggingRoute.Provider,
                TaggingModel = taggingRoute.Model,
                Language = DetectLanguage(block.Content),
                WordCount = CountWords(block.Content),
                TokenCount = EstimateTokenCount(block.Content),
                CharCount = block.Content.Length,
                RetrievalEnabled = true,
                Status = "active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        totalStopwatch.Stop();
        _logger.LogInformation(
            "Embedding/tagging completed. DocumentId={DocumentId}, ChunkCount={ChunkCount}, TaggingBatches={TaggingBatches}, ChunkingMs={ChunkingMs}, EmbeddingMs={EmbeddingMs}, TaggingMs={TaggingMs}, TotalMs={TotalMs}",
            documentId,
            chunks.Count,
            taggingBatches.Count,
            chunkingMs,
            embeddingMs,
            taggingMs,
            totalStopwatch.ElapsedMilliseconds);

        return new EmbeddingTaggingRunResult
        {
            Chunks = chunks,
            EmbeddingUsage = BuildEmbeddingUsageSummary(embeddingRoute, embeddingResponse, embeddings),
            TaggingUsage = BuildTaggingUsageSummary(taggingRoute, taggingResponses.ToList(), taggingResponses.Count + taggingFallbackCalls, taggingFallbackCalls)
        };
    }

    private async Task<BatchTagGenerationResult> GenerateTagsBatchAsync(
        IReadOnlyList<IndexedChunkDraft> batch,
        AIResolvedExecutionOptions route,
        IReadOnlyList<string>? allowedTags,
        string? resolvedSubjectCode,
        AITagTaxonomyCatalog taxonomyCatalog,
        CancellationToken cancellationToken)
    {
        var fallbackItems = batch
            .Select(item => BuildFallbackTagItem(item, allowedTags, resolvedSubjectCode, taxonomyCatalog))
            .ToList();

        if (_options.Enabled)
        {
            try
            {
                var response = await _aiExecutionService.ExecuteTextAsync(
                    route,
                    new AITextRequest
                    {
                        FeatureName = AIFeatureNames.AutoTagging,
                        Provider = route.Provider,
                        Model = route.Model,
                        Temperature = route.Temperature,
                        MaxTokens = route.MaxTokens,
                        SystemPrompt = "Return strict JSON only. For each chunk, return 4 to 8 topic tags for retrieval. Tags must be short, lowercase, academically meaningful, and evidence-based from that chunk. Prefer concrete CS/course concepts over generic tags. Include one structural tag only if useful. Format: [{\"index\":0,\"tags\":[\"tag1\",\"tag2\"]}].",
                        UserPrompt = BuildBatchTaggingPrompt(batch, allowedTags, resolvedSubjectCode, fallbackItems)
                    },
                    cancellationToken);

                var parsedItems = ParseBatchModelTags(response.Content);
                if (parsedItems.Count > 0)
                {
                    var parsedByIndex = parsedItems.ToDictionary(item => item.Index, item => item.Tags);
                    var items = fallbackItems
                        .Select(item =>
                        {
                            if (!parsedByIndex.TryGetValue(item.Index, out var parsedTags))
                            {
                                return item;
                            }

                            var merged = MergeTags(
                                parsedTags.Where(tag => TagHasEvidence(tag, item.Block, item.SubjectCode, taxonomyCatalog)).ToList(),
                                item.HeuristicSeedTags,
                                allowedTags,
                                item.SubjectCode,
                                taxonomyCatalog);

                            return new TaggedChunkResult(
                                item.Index,
                                item.Block,
                                item.SubjectCode,
                                item.HeuristicSeedTags,
                                merged.Count > 0 ? merged : item.Tags);
                        })
                        .ToList();

                    return new BatchTagGenerationResult
                    {
                        Items = items,
                        Response = response,
                        FallbackChunkCount = items.Count(item => item.Tags.SequenceEqual(
                            fallbackItems.First(source => source.Index == item.Index).Tags,
                            StringComparer.Ordinal))
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-tagging failed for one chunk batch. Falling back to heuristics.");
            }
        }

        return new BatchTagGenerationResult
        {
            Items = fallbackItems,
            Response = null,
            FallbackChunkCount = fallbackItems.Count
        };
    }

    private TaggedChunkResult BuildFallbackTagItem(
        IndexedChunkDraft item,
        IReadOnlyList<string>? allowedTags,
        string? resolvedSubjectCode,
        AITagTaxonomyCatalog taxonomyCatalog)
    {
        var chunkSubjectCode = resolvedSubjectCode ?? InferSubjectCode(item.Block.Content, item.Block.SectionTitle, item.Block.ChapterTitle);
        var heuristicSeedTags = BuildDeterministicTags(item.Block, allowedTags, chunkSubjectCode);
        var merged = heuristicSeedTags.Count > 0
            ? heuristicSeedTags
            : ExtractHeuristicTags(item.Block.Content, item.Block.SectionTitle, item.Block.ChapterTitle, allowedTags, chunkSubjectCode);

        return new TaggedChunkResult(item.Index, item.Block, chunkSubjectCode, heuristicSeedTags, merged);
    }

    private static EmbeddingUsageSummaryDto BuildEmbeddingUsageSummary(
        AIResolvedExecutionOptions route,
        AIEmbeddingResponse? response,
        IReadOnlyList<List<double>> embeddings)
    {
        var usage = AIUsageAccounting.ForEmbedding(
            response?.Provider ?? route.Provider,
            route.Model,
            response?.Model,
            response?.TokensUsed,
            response?.ReportedCostUsd,
            response?.UsageSource,
            response?.CostSource);

        return new EmbeddingUsageSummaryDto
        {
            Provider = usage.Provider,
            ConfiguredModel = route.Model,
            EffectiveModel = usage.EffectiveModel,
            ModelFamily = usage.ModelFamily,
            NormalizedModelKey = usage.NormalizedModelKey,
            TokensUsed = usage.TokensUsed,
            UsageSource = usage.UsageSource,
            CostUsd = usage.CostUsd,
            CostSource = usage.CostSource,
            VectorCount = embeddings.Count,
            EmbeddingDimension = embeddings.FirstOrDefault()?.Count ?? 0,
            FallbackUsed = response?.FromFallback ?? false,
            FallbackFromProvider = response?.FallbackFromProvider,
            FallbackFromModel = response?.FallbackFromModel,
            FallbackReasonCode = response?.FallbackReasonCode
        };
    }

    private static TaggingUsageSummaryDto BuildTaggingUsageSummary(
        AIResolvedExecutionOptions route,
        IReadOnlyList<AITextResponse> responses,
        int totalCalls,
        int fallbackCalls)
    {
        var snapshots = responses
            .Select(response => AIUsageAccounting.ForText(
                response.Provider ?? route.Provider,
                route.Model,
                response.Model,
                response.InputTokens,
                response.OutputTokens,
                response.TotalTokens,
                response.ReportedCostUsd,
                response.UsageSource,
                response.CostSource))
            .ToList();

        var effectiveModel = snapshots.LastOrDefault()?.EffectiveModel ?? route.Model;
        var anyProviderUsage = snapshots.Any(item => string.Equals(item.UsageSource, "provider_response", StringComparison.OrdinalIgnoreCase));
        var anyProviderCost = snapshots.Any(item => string.Equals(item.CostSource, "provider_response", StringComparison.OrdinalIgnoreCase));

        return new TaggingUsageSummaryDto
        {
            Provider = snapshots.LastOrDefault()?.Provider ?? route.Provider,
            ConfiguredModel = route.Model,
            EffectiveModel = effectiveModel,
            ModelFamily = AIUsageAccounting.NormalizeModelFamily(effectiveModel),
            NormalizedModelKey = AIUsageAccounting.NormalizeModelKey(snapshots.LastOrDefault()?.Provider ?? route.Provider, effectiveModel),
            CallCount = totalCalls,
            SuccessfulCalls = responses.Count,
            FallbackCalls = fallbackCalls,
            InputTokens = snapshots.Sum(item => item.InputTokens),
            OutputTokens = snapshots.Sum(item => item.OutputTokens),
            TotalTokens = snapshots.Sum(item => item.TotalTokens),
            UsageSource = anyProviderUsage ? "provider_response_partial_or_full" : "missing_from_provider_response",
            CostUsd = snapshots.Sum(item => item.CostUsd),
            CostSource = anyProviderCost
                ? "provider_response_partial_or_full"
                : anyProviderUsage
                    ? "estimated_from_provider_usage"
                    : "estimated_catalog",
            FallbackUsed = responses.Any(item => item.FromFallback) || fallbackCalls > 0,
            FallbackFromProvider = responses.FirstOrDefault(item => item.FromFallback)?.FallbackFromProvider,
            FallbackFromModel = responses.FirstOrDefault(item => item.FromFallback)?.FallbackFromModel,
            FallbackReasonCode = responses.FirstOrDefault(item => item.FromFallback)?.FallbackReasonCode
        };
    }

    private static List<ChunkDraft> ChunkContent(string content, ExtractionStructuralHintsDto? structuralHints)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<ChunkDraft>();
        }

        var hintIndex = BuildStructuralHintIndex(structuralHints);

        var blocks = ExtractStructuralBlocks(content, structuralHints)
            .SelectMany(block =>
            {
                if (block.Unit is not null)
                {
                    return new[] { new StructuralBlock(block.Content.Trim(), block.Unit) };
                }

                return Regex.Split(block.Content.Trim(), @"\n\s*\n")
                    .Select(item => new StructuralBlock(item.Trim(), null));
            })
            .Where(block => block.Content.Length > 0)
            .ToList();

        if (blocks.Count == 0)
        {
            blocks.Add(new StructuralBlock(content.Trim(), null));
        }

        var chunks = new List<ChunkDraft>();
        var currentBlocks = new List<string>();
        var currentLength = 0;
        var chapterOrder = 0;
        string? currentChapterKey = null;
        string? currentChapterTitle = null;
        string? currentSectionTitle = null;
        var currentSectionPath = new List<string>();
        int? currentSectionLevel = null;
        var currentUsesAiBackbone = false;

        foreach (var blockEntry in blocks)
        {
            var block = blockEntry.Content;
            var aiHeading = ToHeadingMetadata(blockEntry.Unit);
            var pageHeading = TryExtractPageMarkerHeading(block, hintIndex);
            var heading = aiHeading ?? pageHeading ?? TryParseHeading(block, hintIndex);
            var normalizedBlock = Regex.Replace(block.Trim(), @"\s+", " ");
            var isStructuralOnlyBlock = heading is not null && IsStructuralOnlyBlock(block, normalizedBlock);

            if (heading is not null && currentBlocks.Count > 0)
            {
                chunks.Add(BuildChunkDraft(
                    currentBlocks,
                    currentSectionTitle,
                    currentChapterKey,
                    currentChapterTitle,
                    chapterOrder > 0 ? chapterOrder : null,
                    currentSectionPath,
                    currentSectionLevel,
                    hintIndex,
                    currentUsesAiBackbone));
                currentBlocks.Clear();
                currentLength = 0;
            }

            if (heading is not null)
            {
                if (heading.IsDocumentHeader)
                {
                    continue;
                }
                else if (heading.IsAiStructural &&
                         heading.IsChapterHeading &&
                         ShouldDemoteAiHeadingToCurrentChapter(block, heading.Title, currentChapterTitle, hintIndex))
                {
                    currentSectionPath = BuildSectionPath(
                        currentSectionPath.Count > 0
                            ? currentSectionPath
                            : new List<string> { currentChapterTitle! },
                        heading.Title,
                        Math.Max(2, currentSectionLevel ?? 2));
                }
                else if (IsSectionLikeChildTitle(heading.Title) && !string.IsNullOrWhiteSpace(currentChapterTitle))
                {
                    currentSectionPath = BuildSectionPath(currentSectionPath, heading.Title, Math.Max(2, heading.Level));
                }
                else if (heading.Level <= 1 || heading.IsChapterHeading)
                {
                    if (!ShouldReuseCurrentChapter(currentChapterTitle, heading.Title))
                    {
                        chapterOrder += 1;
                        currentChapterTitle = heading.Title;
                        currentChapterKey = BuildChapterKeyFromTitle(heading.Title, chapterOrder);
                    }
                    currentSectionPath = new List<string> { heading.Title };
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(currentChapterTitle))
                    {
                        chapterOrder += 1;
                        currentChapterTitle = heading.Title;
                        currentChapterKey = BuildChapterKeyFromTitle(heading.Title, chapterOrder);
                        currentSectionPath = new List<string> { heading.Title };
                    }
                    else
                    {
                        currentSectionPath = BuildSectionPath(currentSectionPath, heading.Title, heading.Level);
                    }
                }

                currentSectionTitle = heading.Title;
                currentSectionLevel = heading.Level;
                currentUsesAiBackbone = heading.IsAiStructural;
                if (isStructuralOnlyBlock)
                {
                    continue;
                }
            }

            if (normalizedBlock.Length < 40 && heading is null)
            {
                continue;
            }

            if (heading is null && IsLowValueContentBlock(block))
            {
                continue;
            }

            if (currentLength + normalizedBlock.Length > 900 && currentBlocks.Count > 0)
            {
                chunks.Add(BuildChunkDraft(
                    currentBlocks,
                    currentSectionTitle,
                    currentChapterKey,
                    currentChapterTitle,
                    chapterOrder > 0 ? chapterOrder : null,
                    currentSectionPath,
                    currentSectionLevel,
                    hintIndex,
                    currentUsesAiBackbone));
                currentBlocks.Clear();
                currentLength = 0;
            }

            currentBlocks.Add(block.Trim());
            currentLength += normalizedBlock.Length;
        }

        if (currentBlocks.Count > 0)
        {
            chunks.Add(BuildChunkDraft(
                currentBlocks,
                currentSectionTitle,
                currentChapterKey,
                currentChapterTitle,
                chapterOrder > 0 ? chapterOrder : null,
                currentSectionPath,
                currentSectionLevel,
                hintIndex,
                currentUsesAiBackbone));
        }

        return RefineDenseChunks(chunks, hintIndex);
    }

    private static bool IsStructuralOnlyBlock(string block, string normalizedBlock)
    {
        if (string.IsNullOrWhiteSpace(normalizedBlock))
        {
            return true;
        }

        var meaningfulLines = block
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !IsIgnorableStructuralLine(line))
            .Where(line => !IsSlidePageCounterLine(line))
            .ToList();

        if (meaningfulLines.Count <= 1)
        {
            return true;
        }

        if (!meaningfulLines.Any(line => Regex.IsMatch(line, @"[\.!?]|[a-z]{3,}\s+[a-z]{3,}", RegexOptions.IgnoreCase)))
        {
            return normalizedBlock.Length < 24;
        }

        if (normalizedBlock.Length <= 120)
        {
            var sentenceCount = Regex.Matches(normalizedBlock, @"[\.!?]").Count;
            var bulletCount = normalizedBlock.Count(ch => ch == '•');
            return sentenceCount == 0 && bulletCount <= 4;
        }

        return false;
    }

    private static bool ShouldDemoteAiHeadingToCurrentChapter(
        string block,
        string headingTitle,
        string? currentChapterTitle,
        StructuralHintIndex hintIndex)
    {
        var currentChapter = CleanStructuralTitle(currentChapterTitle);
        var heading = CleanStructuralTitle(headingTitle);
        if (string.IsNullOrWhiteSpace(currentChapter) || string.IsNullOrWhiteSpace(heading))
        {
            return false;
        }

        if (string.Equals(currentChapter, heading, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (LooksLikeReadingReferenceTitle(heading) || IsCourseBannerHeading(heading))
        {
            return true;
        }

        if (LooksLikeChapterTitle(heading) ||
            LooksLikeStrongHintedChapterTitle(heading) ||
            hintIndex.StrongChapterTitleKeys.Contains(NormalizeHintKey(heading)))
        {
            return false;
        }

        var lines = block
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !IsIgnorableStructuralLine(line))
            .Where(line => !IsSlidePageCounterLine(line))
            .ToList();

        if (lines.Count == 0)
        {
            return false;
        }

        var firstLine = CleanStructuralTitle(lines[0]);
        var remainingLines = lines
            .Skip(firstLine is not null && string.Equals(firstLine, heading, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ToList();

        if (remainingLines.Count == 0)
        {
            return false;
        }

        var firstContentLine = remainingLines[0];
        var previewLines = remainingLines
            .Take(3)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
        var combinedPreview = string.Join(" ", previewLines);
        var wordCount = Regex.Matches(heading, @"[A-Za-z][A-Za-z0-9+\-#]*").Count;
        var startsLikeContinuation =
            Regex.IsMatch(firstContentLine, @"^[a-z0-9\(\[\{]", RegexOptions.None) ||
            (firstContentLine.Length <= 2 &&
             previewLines.Count >= 2 &&
             Regex.IsMatch(previewLines[1], @"^[a-z0-9\(\[\{]", RegexOptions.None));
        var sentenceLike = Regex.IsMatch(combinedPreview, @"[\.!?]") ||
                           Regex.IsMatch(combinedPreview, @"\b(is|are|was|were|can|uses?|using|contains?|occurs?|maps?|parts?|divide|split|store|insert)\b", RegexOptions.IgnoreCase);

        return wordCount <= 3 && startsLikeContinuation && sentenceLike;
    }

    private static List<ChunkDraft> RefineDenseChunks(IReadOnlyList<ChunkDraft> chunks, StructuralHintIndex hintIndex)
    {
        var refined = new List<ChunkDraft>();
        foreach (var chunk in chunks)
        {
            var split = TrySplitDenseChunk(chunk, hintIndex);
            if (split.Count == 0)
            {
                refined.Add(chunk);
                continue;
            }

            refined.AddRange(split);
        }

        return refined;
    }

    private static List<ChunkDraft> TrySplitDenseChunk(ChunkDraft chunk, StructuralHintIndex hintIndex)
    {
        if (string.IsNullOrWhiteSpace(chunk.Content))
        {
            return new List<ChunkDraft> { chunk };
        }

        var estimatedTokens = EstimateTokenCount(chunk.Content);
        if (estimatedTokens < DenseChunkSecondarySplitTokenThreshold &&
            chunk.Content.Length < DenseChunkSecondarySplitCharacterThreshold)
        {
            return new List<ChunkDraft> { chunk };
        }

        var lines = chunk.Content
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .ToList();
        if (lines.Count < 6)
        {
            return new List<ChunkDraft> { chunk };
        }

        var segments = new List<ChunkDraft>();
        var buffer = new List<string>();
        var activeSectionTitle = chunk.SectionTitle;
        var activeSectionPath = chunk.SectionPath.ToList();
        var splitCount = 0;

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var nextLine = index + 1 < lines.Count ? lines[index + 1].Trim() : null;

            if (IsSecondarySplitHeading(trimmed, nextLine, chunk, hintIndex))
            {
                if (EstimateDenseSegmentCharacters(buffer) >= DenseChunkMinimumSegmentCharacterThreshold)
                {
                    segments.Add(BuildDerivedChunkDraft(
                        chunk,
                        buffer,
                        activeSectionTitle,
                        activeSectionPath,
                        splitCount));
                    buffer.Clear();
                    splitCount += 1;
                }

                activeSectionTitle = CleanStructuralTitle(trimmed) ?? activeSectionTitle;
                activeSectionPath = BuildDerivedSectionPath(chunk, activeSectionTitle);
            }

            buffer.Add(line);
        }

        if (EstimateDenseSegmentCharacters(buffer) >= DenseChunkMinimumSegmentCharacterThreshold || segments.Count == 0)
        {
            segments.Add(BuildDerivedChunkDraft(
                chunk,
                buffer,
                activeSectionTitle,
                activeSectionPath,
                splitCount));
        }

        var usableSegments = segments
            .Where(item => !string.IsNullOrWhiteSpace(item.Content))
            .ToList();

        if (usableSegments.Count <= 1)
        {
            return new List<ChunkDraft> { chunk };
        }

        return usableSegments;
    }

    private static bool IsSecondarySplitHeading(
        string line,
        string? nextLine,
        ChunkDraft chunk,
        StructuralHintIndex hintIndex)
    {
        if (string.IsNullOrWhiteSpace(line) || IsIgnorableStructuralLine(line))
        {
            return false;
        }

        var cleaned = CleanStructuralTitle(line);
        if (string.IsNullOrWhiteSpace(cleaned) || IsLowValueStructuralTitle(cleaned))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(chunk.ChapterTitle) &&
            string.Equals(cleaned, CleanStructuralTitle(chunk.ChapterTitle), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (cleaned.Length > 90 || LooksCodeHeavy(cleaned))
        {
            return false;
        }

        if (Regex.IsMatch(cleaned, @"^\d+[\.)]\s*$"))
        {
            return false;
        }

        if (Regex.IsMatch(cleaned, @"(?i)^output$"))
        {
            return false;
        }

        if (Regex.IsMatch(cleaned,
                @"(?i)^(objectives?|overloading|overriding|interfaces?|abstract classes?|nested class(?:es)?(?:\s*\(optional\))?|anonymous classes?|implementing abstract methods|summary|example(?:\s*:\s*.*)?|why and when to use interfaces)$"))
        {
            return true;
        }

        if (hintIndex.StrongChapterTitleKeys.Contains(NormalizeHintKey(cleaned)))
        {
            return true;
        }

        if (ExtractStructuralNumberingDepth(cleaned) >= 2 &&
            !IsGenericSectionOnlyHeading(cleaned) &&
            !LooksLikeReadingReferenceTitle(cleaned) &&
            !LooksLikeCodeAssignmentTitle(cleaned) &&
            !string.IsNullOrWhiteSpace(nextLine) &&
            nextLine.Length > 30)
        {
            return true;
        }

        return LooksLikeStandaloneHeading(cleaned, nextLine) && IsCompactConceptHeading(cleaned);
    }

    private static int EstimateDenseSegmentCharacters(IReadOnlyList<string> lines)
        => lines.Sum(line => string.IsNullOrWhiteSpace(line) ? 0 : line.Trim().Length);

    private static ChunkDraft BuildDerivedChunkDraft(
        ChunkDraft parent,
        IReadOnlyList<string> lines,
        string? sectionTitle,
        IReadOnlyList<string> sectionPath,
        int splitIndex)
    {
        var content = string.Join(Environment.NewLine, lines).Trim();
        var cleanedSectionTitle = CleanStructuralTitle(sectionTitle) ??
                                  CleanStructuralTitle(parent.SectionTitle) ??
                                  $"Section {splitIndex + 1}";

        var derivedPath = sectionPath.Count > 0
            ? sectionPath.ToList()
            : BuildDerivedSectionPath(parent, cleanedSectionTitle);

        return new ChunkDraft
        {
            Content = content,
            ChunkType = InferChunkType(content),
            SectionTitle = cleanedSectionTitle,
            ChapterKey = parent.ChapterKey,
            ChapterTitle = parent.ChapterTitle,
            ChapterOrder = parent.ChapterOrder,
            SectionPath = derivedPath,
            SectionLevel = parent.SectionLevel,
            QuestionTypeAffinity = InferQuestionTypeAffinity(content)
        };
    }

    private static List<string> BuildDerivedSectionPath(ChunkDraft parent, string? sectionTitle)
    {
        var cleanedSectionTitle = CleanStructuralTitle(sectionTitle);
        if (string.IsNullOrWhiteSpace(cleanedSectionTitle))
        {
            return parent.SectionPath.ToList();
        }

        var parentPath = parent.SectionPath
            .Select(CleanStructuralTitle)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();

        if (parentPath.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(parent.ChapterTitle))
            {
                parentPath.Add(CleanStructuralTitle(parent.ChapterTitle)!);
            }

            parentPath.Add(cleanedSectionTitle);
            return parentPath.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (!string.Equals(parentPath[^1], cleanedSectionTitle, StringComparison.OrdinalIgnoreCase))
        {
            parentPath.Add(cleanedSectionTitle);
        }

        return parentPath;
    }

    private static List<StructuralBlock> ExtractStructuralBlocks(string content, ExtractionStructuralHintsDto? structuralHints)
    {
        var trimmed = content.Trim();
        var aiUnits = (structuralHints?.StructuralUnits ?? new List<ExtractedStructuralUnitDto>())
            .Where(item => item.StartLine > 0 && !string.IsNullOrWhiteSpace(item.DisplayTitle))
            .OrderBy(item => item.StartLine)
            .ThenBy(item => item.EndLine ?? int.MaxValue)
            .ToList();

        if (aiUnits.Count > 0)
        {
            var lines = trimmed.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var blocks = new List<StructuralBlock>();
            for (var index = 0; index < aiUnits.Count; index++)
            {
                var unit = aiUnits[index];
                var startLine = Math.Max(1, Math.Min(unit.StartLine, lines.Length));
                var endLine = unit.EndLine.HasValue
                    ? Math.Max(startLine, Math.Min(unit.EndLine.Value, lines.Length))
                    : index + 1 < aiUnits.Count
                        ? Math.Max(startLine, aiUnits[index + 1].StartLine - 1)
                        : lines.Length;

                var slice = lines
                    .Skip(startLine - 1)
                    .Take(endLine - startLine + 1)
                    .ToArray();
                var blockContent = string.Join(Environment.NewLine, slice).Trim();
                if (blockContent.Length == 0)
                {
                    continue;
                }

                blocks.Add(new StructuralBlock(blockContent, unit));
            }

            if (blocks.Count > 0)
            {
                return blocks;
            }
        }

        var pageBlocks = Regex.Split(trimmed, @"(?=\[(?:Page|Slide)\s+\d+\])", RegexOptions.IgnoreCase)
            .Select(block => block.Trim())
            .Where(block => block.Length > 0)
            .Select(block => new StructuralBlock(block, null))
            .ToList();

        return pageBlocks.Count > 1
            ? pageBlocks
            : new List<StructuralBlock> { new(trimmed, null) };
    }

    private static HeadingMetadata? ToHeadingMetadata(ExtractedStructuralUnitDto? unit)
    {
        if (unit is null || string.IsNullOrWhiteSpace(unit.DisplayTitle))
        {
            return null;
        }

        var isChapter = unit.Kind is "chapter" or "scope_block" or "topic";
        var level = isChapter ? 1 : 2;
        return new HeadingMetadata(unit.DisplayTitle, level, isChapter, false, true);
    }

    private static ChunkDraft BuildChunkDraft(
        IReadOnlyList<string> blocks,
        string? sectionTitle,
        string? chapterKey,
        string? chapterTitle,
        int? chapterOrder,
        IReadOnlyList<string> sectionPath,
        int? sectionLevel,
        StructuralHintIndex hintIndex,
        bool lockToInheritedStructure)
    {
        var content = string.Join(Environment.NewLine + Environment.NewLine, blocks).Trim();
        var cleanedSectionTitle = CleanStructuralTitle(sectionTitle);
        var detectedSectionTitle = CleanStructuralTitle(DetectSectionTitle(content, hintIndex));
        var fallbackSectionTitle = CleanStructuralTitle(FindFallbackSectionTitle(content));
        var resolvedSectionTitle = lockToInheritedStructure
            ? cleanedSectionTitle ?? detectedSectionTitle ?? fallbackSectionTitle
            : SelectPreferredSectionTitle(
                cleanedSectionTitle,
                detectedSectionTitle,
                fallbackSectionTitle);
        var resolvedSectionPath = sectionPath.Count > 0
            ? sectionPath
                .Select(CleanStructuralTitle)
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Cast<string>()
                .ToList()
            : resolvedSectionTitle is not null
                ? new List<string> { resolvedSectionTitle }
                : new List<string>();
        var resolvedChapterTitle = lockToInheritedStructure
            ? CleanStructuralTitle(chapterTitle) ??
              resolvedSectionPath.FirstOrDefault() ??
              resolvedSectionTitle
            : SelectPreferredChapterTitle(
                chapterTitle,
                resolvedSectionPath,
                resolvedSectionTitle,
                hintIndex);
        var hasSpecificChapterKey = !string.IsNullOrWhiteSpace(chapterKey) &&
                                    !IsLowValueStructuralTitle(chapterKey) &&
                                    (lockToInheritedStructure || !Regex.IsMatch(chapterKey, @"^chapter-\d+$", RegexOptions.IgnoreCase));
        var resolvedChapterKey = hasSpecificChapterKey
            ? chapterKey
            : BuildChapterKeyFromTitle(resolvedChapterTitle ?? resolvedSectionTitle, chapterOrder ?? 1);

        return new ChunkDraft
        {
            Content = content,
            ChunkType = InferChunkType(content),
            SectionTitle = resolvedSectionTitle,
            ChapterKey = resolvedChapterKey,
            ChapterTitle = resolvedChapterTitle,
            ChapterOrder = chapterOrder,
            SectionPath = resolvedSectionPath,
            SectionLevel = sectionLevel ?? InferSectionLevelFromPath(resolvedSectionPath),
            QuestionTypeAffinity = InferQuestionTypeAffinity(content)
        };
    }

    private static string? SelectPreferredSectionTitle(string? inheritedTitle, string? detectedTitle, string? fallbackTitle)
    {
        if (ShouldPreferMoreSpecificSectionTitle(inheritedTitle, detectedTitle))
        {
            return detectedTitle;
        }

        if (ShouldPreferMoreSpecificSectionTitle(inheritedTitle, fallbackTitle))
        {
            return fallbackTitle;
        }

        if (!string.IsNullOrWhiteSpace(inheritedTitle) && !IsGenericSectionOnlyHeading(inheritedTitle))
        {
            return inheritedTitle;
        }

        if (!string.IsNullOrWhiteSpace(detectedTitle) && !IsGenericSectionOnlyHeading(detectedTitle))
        {
            return detectedTitle;
        }

        if (!string.IsNullOrWhiteSpace(fallbackTitle))
        {
            return fallbackTitle;
        }

        if (!string.IsNullOrWhiteSpace(detectedTitle) && !IsGenericSectionOnlyHeading(detectedTitle))
        {
            return detectedTitle;
        }

        if (!string.IsNullOrWhiteSpace(inheritedTitle) && !IsGenericSectionOnlyHeading(inheritedTitle))
        {
            return inheritedTitle;
        }

        return null;
    }

    private static bool ShouldPreferMoreSpecificSectionTitle(string? currentTitle, string? candidateTitle)
    {
        var current = CleanStructuralTitle(currentTitle);
        var candidate = CleanStructuralTitle(candidateTitle);
        if (string.IsNullOrWhiteSpace(candidate) || IsGenericSectionOnlyHeading(candidate))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        if (string.Equals(current, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsGenericSectionOnlyHeading(current))
        {
            return true;
        }

        var currentDepth = ExtractStructuralNumberingDepth(current);
        var candidateDepth = ExtractStructuralNumberingDepth(candidate);
        if (candidateDepth >= 2 && candidateDepth > currentDepth)
        {
            return true;
        }

        if (LooksLikeChapterTitle(current) && !LooksLikeChapterTitle(candidate))
        {
            return true;
        }

        return false;
    }

    private static int ExtractStructuralNumberingDepth(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return 0;
        }

        var match = Regex.Match(title.Trim(), @"^(?<num>\d+(?:\.\d+){0,3})\b");
        if (!match.Success)
        {
            return 0;
        }

        return match.Groups["num"].Value.Count(ch => ch == '.') + 1;
    }

    private static string? SelectPreferredChapterTitle(
        string? inheritedChapterTitle,
        IReadOnlyList<string> resolvedSectionPath,
        string? resolvedSectionTitle,
        StructuralHintIndex hintIndex)
    {
        var inherited = CleanStructuralTitle(inheritedChapterTitle);
        if (!string.IsNullOrWhiteSpace(inherited) &&
            !IsGenericSectionOnlyHeading(inherited) &&
            !LooksLikeReadingReferenceTitle(inherited) &&
            !LooksLikeCodeAssignmentTitle(inherited))
        {
            return inherited;
        }

        var pathCandidate = resolvedSectionPath
            .Select(CleanStructuralTitle)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .FirstOrDefault(item =>
                !IsGenericSectionOnlyHeading(item) &&
                !IsLikelySectionScopedTitle(item) &&
                !LooksLikeReadingReferenceTitle(item) &&
                !LooksLikeCodeAssignmentTitle(item));

        if (!string.IsNullOrWhiteSpace(pathCandidate))
        {
            return pathCandidate;
        }

        var hintedCandidate = hintIndex.DisplayTitles
            .Select(CleanStructuralTitle)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Where(item =>
                !IsGenericSectionOnlyHeading(item) &&
                !LooksLikeReadingReferenceTitle(item) &&
                !LooksLikeCodeAssignmentTitle(item) &&
                !IsCourseBannerHeading(item))
            .OrderByDescending(item => LooksLikeChapterTitle(item))
            .ThenByDescending(item => Regex.IsMatch(item, @"(?i)^(part|module|unit|chapter|section)\b"))
            .ThenBy(item => item.Length)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(hintedCandidate))
        {
            return hintedCandidate;
        }

        var cleanedSectionTitle = CleanStructuralTitle(resolvedSectionTitle);
        if (!string.IsNullOrWhiteSpace(cleanedSectionTitle) &&
            !IsLikelySectionScopedTitle(cleanedSectionTitle))
        {
            return cleanedSectionTitle;
        }

        return inherited;
    }

    private static List<string> ExtractHeuristicTags(string content, string? sectionTitle, string? chapterTitle, IReadOnlyList<string>? allowedTags, string? subjectCode)
    {
        var lowered = content.ToLowerInvariant();
        var structuralText = $"{chapterTitle} {sectionTitle}".ToLowerInvariant();
        var scored = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var candidates = new Dictionary<string, string[]>
        {
            ["c"] = new[] { "printf", "scanf", "char", "int", "float", "double", "#include", "main(", "const" },
            ["java"] = new[] { "class", "object", "inheritance", "polymorphism", "system.out", "public static void", "abstract class", "override" },
            ["arrays"] = new[] { "array", "arrays", "array-based" },
            ["hashing"] = new[] { "hashing", "hash function", "hash functions", "hash table", "hash tables", "hash code" },
            ["collision resolution"] = new[] { "collision resolution", "collision handling", "open addressing", "linear probing", "quadratic probing", "chaining" },
            ["cryptographic hash functions"] = new[] { "cryptographic hash function", "cryptographic hash functions", "message digest", "checksum" },
            ["linked lists"] = new[] { "linked list", "linked lists" },
            ["singly linked lists"] = new[] { "singly linked list", "singly linked lists" },
            ["doubly linked lists"] = new[] { "doubly linked list", "doubly linked lists" },
            ["circular linked lists"] = new[] { "circular linked list", "circular linked lists", "circular list", "circular lists" },
            ["stacks"] = new[] { "stack", "stacks", "lifo" },
            ["queues"] = new[] { "queue", "queues", "fifo" },
            ["deques"] = new[] { "deque", "deques", "double-ended queue", "double-ended queues" },
            ["priority queues"] = new[] { "priority queue", "priority queues" },
            ["round-robin scheduling"] = new[] { "round-robin", "round robin", "round-robin scheduling" },
            ["classes"] = new[] { "class", "classes" },
            ["objects"] = new[] { "object", "objects", "instance" },
            ["methods"] = new[] { "method", "methods" },
            ["inheritance"] = new[] { "inheritance", "extends", "derived class", "base class" },
            ["polymorphism"] = new[] { "polymorphism", "dynamic binding", "dynamic dispatch" },
            ["abstraction"] = new[] { "abstraction", "abstract method" },
            ["abstract classes"] = new[] { "abstract class", "abstract classes" },
            ["method overriding"] = new[] { "override", "overriding", "@override" },
            ["interfaces"] = new[] { "interface", "implements" },
            ["integral types"] = new[] { "integral", "integer", "char", "short", "long", "unsigned", "signed" },
            ["floating-point types"] = new[] { "float", "double", "long double", "floating-point", "real number" },
            ["literals"] = new[] { "literal", "literals", "suffix", "escape sequence" },
            ["constants"] = new[] { "const", "constant", "named constant" },
            ["variables"] = new[] { "variable", "variables", "identifier", "declare", "declaration" },
            ["input/output"] = new[] { "input", "output", "printf", "scanf", "stdin", "stdout" },
            ["arithmetic operators"] = new[] { "arithmetic operator", "quotient", "remainder", "prefix increment", "postfix increment", "prefix decrement", "postfix decrement" },
            ["relational operators"] = new[] { "relational", ">", "<", ">=", "<=", "==", "!=" },
            ["logical operators"] = new[] { "logical", "&&", "||", "!" },
            ["bitwise operators"] = new[] { "bitwise", "&", "|", "^", "~", "<<", ">>", "flip bits" },
            ["assignment operators"] = new[] { "assignment", "+=", "-=", "*=", "/=", "%=" },
            ["operator precedence"] = new[] { "precedence", "associativity", "evaluation order" },
            ["type conversion"] = new[] { "mixing data types", "cast", "conversion", "promote" },
            ["representation"] = new[] { "representation", "binary", "byte", "memory", "endianness", "little-endian", "big-endian" },
            ["ascii"] = new[] { "ascii" }
        };

        foreach (var pair in candidates)
        {
            foreach (var keyword in pair.Value)
            {
                if (lowered.Contains(keyword, StringComparison.Ordinal) || structuralText.Contains(keyword, StringComparison.Ordinal))
                {
                    AddScore(scored, pair.Key, structuralText.Contains(keyword, StringComparison.Ordinal) ? 3 : 2);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(subjectCode))
        {
            AddScore(scored, subjectCode.ToLowerInvariant(), 4);
        }

        foreach (var phrase in ExtractStructuralPhraseTags(sectionTitle, chapterTitle))
        {
            AddScore(scored, phrase, 3);
        }

        foreach (var phrase in ExtractInlineConceptTags(content))
        {
            AddScore(scored, phrase, 2);
        }

        var detected = scored
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => NormalizeTag(item.Key, allowedTags))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        if (allowedTags is { Count: > 0 })
        {
            detected = detected.Where(tag => allowedTags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        detected = PruneSubjectSpecificHeuristicTags(detected, content, sectionTitle, chapterTitle, subjectCode);

        return detected.Count > 0 ? detected : new List<string> { "general" };
    }

    private static List<string> PruneSubjectSpecificHeuristicTags(
        List<string> detected,
        string content,
        string? sectionTitle,
        string? chapterTitle,
        string? subjectCode)
    {
        if (detected.Count == 0 || string.IsNullOrWhiteSpace(subjectCode))
        {
            return detected;
        }

        var subject = NormalizeSubjectKey(subjectCode);
        if (!string.Equals(subject, AISubjectDomainMapper.DsaJava, StringComparison.OrdinalIgnoreCase))
        {
            return detected;
        }

        var combined = $"{chapterTitle} {sectionTitle} {content}".ToLowerInvariant();
        var titleText = $"{chapterTitle} {sectionTitle}".ToLowerInvariant();
        var hashFocused =
            detected.Contains("hashing", StringComparer.OrdinalIgnoreCase) ||
            detected.Contains("collision resolution", StringComparer.OrdinalIgnoreCase) ||
            detected.Contains("cryptographic hash functions", StringComparer.OrdinalIgnoreCase) ||
            Regex.IsMatch(combined, @"(?i)\b(hashing|hash functions?|hash tables?|collision resolution|open addressing|quadratic probing|linear probing|bucket addressing|cryptographic hash functions?)\b");

        if (!hashFocused)
        {
            return detected;
        }

        var linkedListTitleFocused = Regex.IsMatch(titleText, @"(?i)\blinked lists?\b|\bsingly linked lists?\b|\bdoubly linked lists?\b|\bcircular linked lists?\b");
        if (!linkedListTitleFocused)
        {
            detected = detected
                .Where(tag => !string.Equals(tag, "linked lists", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tag, "singly linked lists", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tag, "doubly linked lists", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tag, "circular linked lists", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (detected.Count == 0 && hashFocused)
        {
            detected.Add("hashing");
        }

        return detected;
    }

    private static string DetectLanguage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "en";
        }

        if (Regex.IsMatch(content, @"[ăâđêôơưĂÂĐÊÔƠƯáàảãạắằẳẵặấầẩẫậéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]", RegexOptions.IgnoreCase))
        {
            return "vi";
        }

        if (Regex.IsMatch(content, @"[A-Za-z]"))
        {
            return "en";
        }

        return Regex.IsMatch(content, @"[^\u0000-\u007F]") ? "mixed" : "en";
    }

    private static int CountWords(string content)
    {
        return string.IsNullOrWhiteSpace(content)
            ? 0
            : content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static int EstimateTokenCount(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Round(content.Length / 4.0));
    }

    private static string BuildTaggingPrompt(ChunkDraft block, IReadOnlyList<string>? allowedTags, string? subjectCode, IReadOnlyList<string> heuristicSeedTags)
    {
        var allowed = allowedTags is { Count: > 0 }
            ? string.Join(", ", allowedTags)
            : "not provided";

        var seed = heuristicSeedTags.Count > 0
            ? string.Join(", ", heuristicSeedTags)
            : "none";

        return
            $"Subject: {subjectCode ?? "unknown"}{Environment.NewLine}" +
            $"Chapter: {block.ChapterTitle ?? "unknown"}{Environment.NewLine}" +
            $"Section: {block.SectionTitle ?? "unknown"}{Environment.NewLine}" +
            $"Chunk type: {block.ChunkType}{Environment.NewLine}" +
            $"Question affinity: {string.Join(", ", block.QuestionTypeAffinity)}{Environment.NewLine}" +
            $"Allowed tags: {allowed}{Environment.NewLine}" +
            $"Heuristic seed tags: {seed}{Environment.NewLine}" +
            $"Return JSON only, example: [\"variables\", \"constants\", \"c\"]{Environment.NewLine}{Environment.NewLine}" +
            $"{block.Content}";
    }

    private static string BuildBatchTaggingPrompt(
        IReadOnlyList<IndexedChunkDraft> batch,
        IReadOnlyList<string>? allowedTags,
        string? resolvedSubjectCode,
        IReadOnlyList<TaggedChunkResult> fallbackItems)
    {
        var allowed = allowedTags is { Count: > 0 }
            ? string.Join(", ", allowedTags)
            : "not provided";

        var lines = new List<string>
        {
            $"Subject: {resolvedSubjectCode ?? "unknown"}",
            $"Allowed tags: {allowed}",
            "Return JSON only, format: [{\"index\":0,\"tags\":[\"tag1\",\"tag2\"]}]",
            string.Empty
        };

        foreach (var item in fallbackItems)
        {
            var seed = item.HeuristicSeedTags.Count > 0
                ? string.Join(", ", item.HeuristicSeedTags)
                : "none";

            lines.Add($"[Chunk {item.Index}]");
            lines.Add($"Chapter: {item.Block.ChapterTitle ?? "unknown"}");
            lines.Add($"Section: {item.Block.SectionTitle ?? "unknown"}");
            lines.Add($"Chunk type: {item.Block.ChunkType}");
            lines.Add($"Question affinity: {string.Join(", ", item.Block.QuestionTypeAffinity)}");
            lines.Add($"Heuristic seed tags: {seed}");
            lines.Add(item.Block.Content);
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string? DetectSectionTitle(string content, StructuralHintIndex hintIndex)
    {
        var pageHeading = TryExtractPageMarkerHeading(content, hintIndex);
        if (pageHeading is not null)
        {
            return pageHeading.Title;
        }

        var lines = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !IsIgnorableStructuralLine(line))
            .ToList();
        var firstLine = GetFirstStructuralCandidateLine(lines);
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return null;
        }

        var hinted = TryResolveHintedTitle(lines, hintIndex);
        if (!string.IsNullOrWhiteSpace(hinted))
        {
            return hinted;
        }

        var inline = firstLine.StartsWith("#", StringComparison.Ordinal)
            ? firstLine.TrimStart('#', ' ')
            : firstLine.Length <= 120 ? firstLine : null;
        if (!string.IsNullOrWhiteSpace(inline))
        {
            return inline;
        }

        return FindFallbackSectionTitle(string.Join(Environment.NewLine, lines));
    }

    private static string InferChunkType(string content)
    {
        if (content.Contains("```", StringComparison.Ordinal))
        {
            return "code_block";
        }

        if (content.StartsWith("#", StringComparison.Ordinal))
        {
            return "concept";
        }

        return "content";
    }

    private static List<string> InferQuestionTypeAffinity(string content)
    {
        var lowered = content.ToLowerInvariant();
        var hasCodeSignals =
            content.Contains("```", StringComparison.Ordinal) ||
            Regex.IsMatch(lowered, @"\b(code|program|implement|function|algorithm|output|input|constraint|test case|sample input|sample output)\b") ||
            Regex.IsMatch(content, @"\b(for|while|if|printf|scanf|main|public\s+static\s+void|System\.out)\b", RegexOptions.IgnoreCase);

        var hasConceptSignals =
            Regex.IsMatch(lowered, @"\b(what|which|choose|definition|concept|rule|syntax|explain|correct answer)\b");

        if (hasCodeSignals && !hasConceptSignals)
        {
            return new List<string> { "PE", "BOTH" };
        }

        if (hasConceptSignals && !hasCodeSignals)
        {
            return new List<string> { "FE", "BOTH" };
        }

        return new List<string> { "BOTH" };
    }

    private static HeadingMetadata? TryParseHeading(string block, StructuralHintIndex hintIndex)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return null;
        }

        var lines = block
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            return null;
        }

        var firstLine = GetFirstStructuralCandidateLine(lines);

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return null;
        }

        var markdownMatch = Regex.Match(firstLine, @"^(#{1,6})\s+(.+)$");
        if (markdownMatch.Success)
        {
            var title = markdownMatch.Groups[2].Value.Trim();
            if (IsLowValueStructuralTitle(title))
            {
                return null;
            }

            if (IsSyntheticDocumentHeader(title))
            {
            return new HeadingMetadata(title, 1, false, true, false);
            }

            var level = markdownMatch.Groups[1].Value.Length;
            return new HeadingMetadata(title, level, level == 1 || LooksLikeChapterTitle(title), false, false);
        }

        if (firstLine.Length > 140)
        {
            return null;
        }

        if (IsSyntheticDocumentHeader(firstLine))
        {
            return new HeadingMetadata(firstLine, 1, false, true, false);
        }

        if (LooksLikeChapterTitle(firstLine))
        {
            return new HeadingMetadata(firstLine, 1, true, false, false);
        }

        var canonicalConceptTitle = ExtractCanonicalConceptTitle(firstLine);
        if (!string.IsNullOrWhiteSpace(canonicalConceptTitle) &&
            IsCompactConceptHeading(firstLine))
        {
            return new HeadingMetadata(canonicalConceptTitle, 1, true, false, false);
        }

        var numbered = Regex.Match(firstLine, @"^(?<num>(\d+(\.\d+){0,3}|[IVXLCM]+|[A-Z]))[\)\.\-: ]+(?<title>.+)$", RegexOptions.IgnoreCase);
        if (numbered.Success)
        {
            var numbering = numbered.Groups["num"].Value;
            var title = $"{numbering} {numbered.Groups["title"].Value.Trim()}".Trim();
            if (IsLowValueStructuralTitle(title))
            {
                return null;
            }

            var level = InferHeadingLevelFromNumbering(numbering);
            var isSingleLevelNumberedSection =
                Regex.IsMatch(numbering, @"^\d+$", RegexOptions.IgnoreCase) &&
                !LooksLikeChapterTitle(title) &&
                IsLikelySectionScopedTitle(title);
            var normalizedLevel = isSingleLevelNumberedSection
                ? Math.Max(2, level)
                : level;
            var isChapterHeading = !isSingleLevelNumberedSection &&
                                   (normalizedLevel == 1 || LooksLikeChapterTitle(title));
            return new HeadingMetadata(title, Math.Min(6, Math.Max(1, normalizedLevel)), isChapterHeading, false, false);
        }

        if (LooksLikeStandaloneHeading(firstLine, lines.Count > 1 ? lines[1] : null))
        {
            return new HeadingMetadata(firstLine, 2, false, false, false);
        }

        var hinted = TryResolveHintedTitle(lines, hintIndex);
        if (!string.IsNullOrWhiteSpace(hinted))
        {
            var chapterLike = LooksLikeStrongHintedChapterTitle(hinted) && !IsSectionLikeChildTitle(hinted);
            return new HeadingMetadata(hinted, chapterLike ? 1 : 2, chapterLike, false, false);
        }

        return null;
    }

    private static HeadingMetadata? TryExtractPageMarkerHeading(string block, StructuralHintIndex hintIndex)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return null;
        }

        var lines = block
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        var firstLine = lines.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return null;
        }

        var pageMatch = Regex.Match(firstLine, @"^\[(?<kind>Page|Slide)\s+\d+\]\s*(?<title>[^\[\r\n]{0,120})", RegexOptions.IgnoreCase);
        if (!pageMatch.Success)
        {
            return null;
        }

        var inlineTitle = pageMatch.Groups["title"].Value.Trim();
        var nextCandidate = GetFirstStructuralCandidateLine(lines.Skip(1).ToList());
        var rawTitle = !string.IsNullOrWhiteSpace(inlineTitle)
            ? inlineTitle
            : nextCandidate;
        var hinted = TryResolveHintedTitle(lines.Skip(1).ToList(), hintIndex);
        var title = NormalizeStructuralTitle(!string.IsNullOrWhiteSpace(hinted) ? hinted : rawTitle, preferConceptTitle: true);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var isSlideMarker = string.Equals(pageMatch.Groups["kind"].Value, "Slide", StringComparison.OrdinalIgnoreCase);
        var isGenericSectionHeading = IsGenericSectionOnlyHeading(title);
        var isSectionLikeChild = IsSectionLikeChildTitle(title);
        var isChapterLike = Regex.IsMatch(title, @"(?i)\b(polymorphism|inheritance|interface|abstract classes?|overloading|overriding)\b")
            || LooksLikeChapterTitle(title)
            || hintIndex.StrongChapterTitleKeys.Contains(NormalizeHintKey(title))
            || (!isSlideMarker && !isGenericSectionHeading && !isSectionLikeChild && LooksLikeStandaloneHeading(title, lines.Count > 2 ? lines[2] : null));
        return new HeadingMetadata(title, isChapterLike ? 1 : 2, isChapterLike, false, false);
    }

    private static bool LooksLikeChapterTitle(string title)
        => Regex.IsMatch(title, @"(?i)^(chapter|chap|chuong|chương|bai|bài|phan|phần|part|module|unit|section|lesson|topic)\s*[\.:_-]*\s*[a-z0-9ivx]+");

    private static bool IsGenericSectionOnlyHeading(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalized = Regex.Replace(title.Trim(), @"\s+", " ");
        return Regex.IsMatch(normalized,
            @"(?i)^(classes?|methods?|objects?|objectives?|question|questions|exercise\s*\d*|example(?:\s*:\s*.*)?|examples|summary|overview|practice|quiz|worksheet|remark|note|notes|code example(?:\s*:\s*.*)?|accessors?|update methods?|direct applications?|indirect applications?|page\s+\d+)$");
    }

    private static bool IsSectionLikeChildTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return Regex.IsMatch(title,
            @"(?i)\b(ascii table|table for characters|questions as summary|conversion specifiers|variables and data types|example\s*\d+|exercise\s*\d+)\b");
    }

    private static bool IsLikelySectionScopedTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var cleaned = CleanStructuralTitle(title);
        if (string.IsNullOrWhiteSpace(cleaned) || LooksLikeChapterTitle(cleaned))
        {
            return false;
        }

        if (IsSectionLikeChildTitle(cleaned))
        {
            return true;
        }

        if (Regex.IsMatch(cleaned, @"^\d+[\s\.\)]", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return Regex.IsMatch(cleaned,
            @"(?i)\b(type size specifiers|basic computation|representing values|representation of integral values|negative and positive values|unsigned integers|literals?(?: and constants)?|numbers|arithmetic operators|logical operators|mixing data types|implicit casting|explicit casting|operator precedence)\b");
    }

    private static string? CleanStructuralTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalized = NormalizeStructuralTitle(title, preferConceptTitle: false);
        return IsLowValueStructuralTitle(normalized) ? null : normalized;
    }

    private static string? NormalizeStructuralTitle(string? title, bool preferConceptTitle)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalized = title.Trim();
        normalized = StripPlaceholderArtifacts(normalized);
        normalized = Regex.Replace(normalized, @"!\[[^\]]*\]\([^)]+\)", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"^\[Page\s+\d+\]\s*", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"^\[Slide\s+\d+\]\s*", string.Empty, RegexOptions.IgnoreCase);
        normalized = normalized.Replace('\u2022', '|')
            .Replace('•', '|')
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace('…', ' ');
        normalized = Regex.Replace(normalized, @"^\s*[-*]+\s*", string.Empty);
        normalized = normalized.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal);
        normalized = Regex.Replace(normalized, @"\[\[IMAGE:[^\]]*\]\]", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"(?:^|\s)(?:ref|label|slide|page)\s*=\s*[^\s\]\|]+(?:\]\])?", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\bimage-\d+\b", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim(' ', '|', '-', ':', ';', ',', '.');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var conceptTitle = ExtractCanonicalConceptTitle(normalized);
        if (!string.IsNullOrWhiteSpace(conceptTitle))
        {
            return conceptTitle;
        }

        var primarySegment = ExtractPrimaryStructuralSegment(normalized);
        if (preferConceptTitle)
        {
            var compactSegment = CompactStructuralSegment(primarySegment);
            if (!string.IsNullOrWhiteSpace(compactSegment))
            {
                return compactSegment;
            }
        }

        if (LooksCodeHeavy(primarySegment))
        {
            return null;
        }

        if (primarySegment.Length > 80)
        {
            primarySegment = CompactStructuralSegment(primarySegment) ?? primarySegment[..80].Trim();
        }

        primarySegment = Regex.Replace(primarySegment, @"\s+", " ").Trim(' ', '|', '-', ':', ';', ',', '.');
        return string.IsNullOrWhiteSpace(primarySegment) ? null : primarySegment;
    }

    private static string ExtractPrimaryStructuralSegment(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var segments = Regex.Split(title, @"\s*(?:\||: (?=[A-Z])| - (?=[A-Z])| \. (?=[A-Z]))\s*")
            .Select(segment => Regex.Replace(segment, @"\s+", " ").Trim())
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        if (segments.Count == 0)
        {
            return title.Trim();
        }

        var preferred = segments
            .OrderBy(segment => LooksCodeHeavy(segment) ? 1 : 0)
            .ThenBy(segment => segment.Length > 60 ? 1 : 0)
            .ThenBy(segment => segment.Length)
            .FirstOrDefault();

        return preferred ?? title.Trim();
    }

    private static string? CompactStructuralSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return null;
        }

        var cleaned = Regex.Replace(segment, @"\s+", " ").Trim(' ', '|', '-', ':', ';', ',', '.');
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        var words = Regex.Matches(cleaned, @"[A-Za-z][A-Za-z0-9+\-#]*")
            .Select(match => match.Value)
            .ToList();
        if (words.Count == 0)
        {
            return null;
        }

        if (words.Count <= 6 && cleaned.Length <= 60 && !LooksCodeHeavy(cleaned))
        {
            return cleaned;
        }

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "of", "to", "in", "on", "for", "with", "which", "that", "is", "are", "was", "were",
            "be", "been", "being", "can", "have", "has", "had", "but", "their", "this", "these", "those", "it",
            "by", "from", "or", "and", "as", "at", "into", "than", "then", "allows", "allow", "refers", "provides"
        };

        var compactWords = new List<string>();
        foreach (var word in words)
        {
            if (compactWords.Count >= 4)
            {
                break;
            }

            if (compactWords.Count > 0 && stopWords.Contains(word))
            {
                break;
            }

            compactWords.Add(word);
        }

        if (compactWords.Count == 0)
        {
            return null;
        }

        if (compactWords.Count == 1)
        {
            var genericSingleWordHeadings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "how", "what", "why", "when", "where", "which", "this", "that", "these", "those", "modified"
            };

            if (genericSingleWordHeadings.Contains(compactWords[0]))
            {
                return null;
            }
        }

        var compact = string.Join(" ", compactWords);
        return LooksCodeHeavy(compact) ? null : compact;
    }

    private static string? ExtractCanonicalConceptTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalized = Regex.Replace(title.Trim(), @"\s+", " ");
        if (LooksCodeHeavy(normalized))
        {
            return null;
        }

        var wordCount = Regex.Matches(normalized, @"[A-Za-z][A-Za-z0-9+\-#]*").Count;
        var sentenceLike = Regex.IsMatch(
            normalized,
            @"(?i)\b(is|are|was|were|can|should|must|will|loaded|contains?|refers?|provides?|based|using|implements|extends|returns?|prints?|calls?)\b");

        var conceptPatterns = new (string Pattern, string Label)[]
        {
            (@"(?i)\babstract\s+classes?\b", "Abstract Classes"),
            (@"(?i)\bmethod\s+overriding\b", "Method Overriding"),
            (@"(?i)\bmethod\s+overloading\b", "Method Overloading"),
            (@"(?i)\bconstructor\s+overloading\b", "Constructor Overloading"),
            (@"(?i)\bconstructors?\b", "Constructors"),
            (@"(?i)\binterfaces?\b", "Interfaces"),
            (@"(?i)\binterface\b", "Interface"),
            (@"(?i)\boverriding\b", "Overriding"),
            (@"(?i)\boverloading\b", "Overloading"),
            (@"(?i)\bpolymorphism\b", "Polymorphism"),
            (@"(?i)\binheritance\b", "Inheritance"),
            (@"(?i)\babstraction\b", "Abstraction"),
            (@"(?i)\bencapsulation\b", "Encapsulation")
        };

        foreach (var (pattern, label) in conceptPatterns)
        {
            if (Regex.IsMatch(normalized, $"^{pattern}$"))
            {
                return label;
            }

            if (!sentenceLike &&
                wordCount is >= 1 and <= 4 &&
                normalized.Length <= 40 &&
                Regex.IsMatch(normalized, pattern))
            {
                return label;
            }
        }

        return null;
    }

    private static bool LooksCodeHeavy(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return Regex.IsMatch(text, @"[{}();@#]|public\s+\w+|private\s+\w+|protected\s+\w+|class\s+\w+\s*\{|return\s+|void\s+\w+\s*\(",
                RegexOptions.IgnoreCase) ||
               Regex.Matches(text, @"\b[a-zA-Z_]+\s*\(").Count >= 2;
    }

    private static bool IsLowValueStructuralTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return true;
        }

        var normalized = Regex.Replace(title.Trim(), @"\s+", " ");
        if (IsSyntheticDocumentHeader(normalized))
        {
            return true;
        }

        if (LooksLikeCodeAssignmentTitle(normalized) || LooksLikeReadingReferenceTitle(normalized))
        {
            return true;
        }

        if (IsImageSummaryHeaderLine(normalized) || IsMarkdownImageLine(normalized))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^`{3,}\w*$"))
        {
            return true;
        }

        if (normalized.Length <= 2)
        {
            return true;
        }

        if (IsInstitutionalBannerLine(normalized))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^\[(slide|page)\s+\d+\]$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (IsSlidePageCounterLine(normalized) || LooksLikeDanglingFragmentTitle(normalized))
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

        if (Regex.IsMatch(normalized, @"^\d+(?:[\.,]\d+)+(?:\s+[A-Za-z][A-Za-z0-9]*){0,2}$"))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"(?i)^(code example|accessors?|update methods?|direct applications?|indirect applications?)$"))
        {
            return true;
        }

        var alphaNumericTokens = Regex.Matches(normalized, @"[A-Za-z0-9]+")
            .Select(match => match.Value)
            .ToList();
        if (alphaNumericTokens.Count > 0 && alphaNumericTokens.All(token => Regex.IsMatch(token, @"^[01]{2,}$")))
        {
            return true;
        }

        if (alphaNumericTokens.Count == 1 &&
            Regex.IsMatch(alphaNumericTokens[0], @"^(how|what|why|when|where|which|this|that|these|those|modified)$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static List<string> BuildSectionPath(IReadOnlyList<string> existingPath, string title, int level)
    {
        var path = existingPath.ToList();
        if (level <= 1 || path.Count == 0)
        {
            return new List<string> { title };
        }

        var desiredCount = Math.Max(1, level - 1);
        while (path.Count > desiredCount)
        {
            path.RemoveAt(path.Count - 1);
        }

        if (path.Count == 0)
        {
            path.Add(title);
            return path;
        }

        path.Add(title);
        return path;
    }

    private static int InferSectionLevelFromPath(IReadOnlyList<string> sectionPath)
        => sectionPath.Count == 0 ? 1 : Math.Min(6, sectionPath.Count);

    private static string BuildChapterKeyFromTitle(string? title, int chapterOrder)
    {
        var safeTitle = CleanStructuralTitle(title);
        var normalizedTitle = Regex.Replace((safeTitle ?? $"chapter-{chapterOrder:00}").ToLowerInvariant(), @"[^a-z0-9]+", "-")
            .Trim('-');
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            normalizedTitle = $"chapter-{chapterOrder:00}";
        }

        return normalizedTitle.StartsWith("chapter-", StringComparison.Ordinal)
            ? normalizedTitle
            : $"chapter-{chapterOrder:00}-{normalizedTitle}";
    }

    private static bool ShouldReuseCurrentChapter(string? currentChapterTitle, string? nextHeadingTitle)
    {
        var current = CleanStructuralTitle(currentChapterTitle);
        var next = CleanStructuralTitle(nextHeadingTitle);
        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(next))
        {
            return false;
        }

        return string.Equals(current, next, StringComparison.OrdinalIgnoreCase);
    }

    private static StructuralHintIndex BuildStructuralHintIndex(ExtractionStructuralHintsDto? structuralHints)
    {
        var displayTitles = (structuralHints?.CleanDisplayTitleCandidates ?? new List<string>())
            .Concat((structuralHints?.StructuralUnits ?? new List<ExtractedStructuralUnitDto>())
                .Where(item => item.LearnerFacing)
                .Select(item => item.DisplayTitle))
            .Select(CleanStructuralTitle)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var chapterTitles = displayTitles
            .Where(LooksLikeStrongHintedChapterTitle)
            .Select(NormalizeHintKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var title in (structuralHints?.StructuralUnits ?? new List<ExtractedStructuralUnitDto>())
                     .Where(item => item.Kind is "chapter" or "scope_block" or "topic")
                     .Select(item => NormalizeHintKey(item.DisplayTitle))
                     .Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            chapterTitles.Add(title);
        }

        var titleKeys = displayTitles
            .Select(NormalizeHintKey)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new StructuralHintIndex(displayTitles, titleKeys, chapterTitles);
    }

    private static string? TryResolveHintedTitle(IReadOnlyList<string> lines, StructuralHintIndex hintIndex)
    {
        if (hintIndex.DisplayTitles.Count == 0 || lines.Count == 0)
        {
            return null;
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || IsIgnorableStructuralLine(line))
            {
                continue;
            }

            var cleaned = CleanStructuralTitle(line);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            var key = NormalizeHintKey(cleaned);
            if (hintIndex.TitleKeys.Contains(key))
            {
                return hintIndex.DisplayTitles.FirstOrDefault(item => string.Equals(NormalizeHintKey(item), key, StringComparison.OrdinalIgnoreCase)) ?? cleaned;
            }
        }

        return null;
    }

    private static bool LooksLikeStrongHintedChapterTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        if (LooksLikeChapterTitle(title))
        {
            return true;
        }

        if (Regex.IsMatch(title, @"(?i)\b(example|exercise|question|summary|table|specifier|qualifier|task|questions)\b"))
        {
            return false;
        }

        var wordCount = Regex.Matches(title, @"[A-Za-z][A-Za-z0-9'/_\-]*").Count;
        if (wordCount is 0 or > 6)
        {
            return false;
        }

        return Regex.IsMatch(title,
            @"(?i)\b(computation|representation|values?|integers?|symbols?|characters?|variables?|literals?|constants?|operators?|casting|precedence|memory|data types?)\b");
    }

    private static string NormalizeHintKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
    }

    private static bool IsIgnorableStructuralLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        if (IsImageSummaryHeaderLine(line) || IsMarkdownImageLine(line))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"\[\[IMAGE:image-\d+", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"^(?:ref|label|slide|page)\s*=", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"(?:\|\s*(?:ref|label|slide|page)\s*=)|(?:slide|page)=\d+\]\]?$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return IsInstitutionalBannerLine(line) ||
               Regex.IsMatch(line, @"(?i)^\[(slide|page)\s+\d+\]$") ||
               Regex.IsMatch(line, @"(?i)^(slide|page|trang)\s+\d+$") ||
               IsSlidePageCounterLine(line) ||
               Regex.IsMatch(line, @"(?i)^(figure|fig|image|img|hinh|hình)\s*[\.:#-]?\s*\d*$");
    }

    private static bool IsSlidePageCounterLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = Regex.Replace(line.Trim(), @"\s+", string.Empty);
        return Regex.IsMatch(normalized, @"^/?\d{1,4}$") ||
               Regex.IsMatch(normalized, @"^\d{1,4}/\d{1,4}$");
    }

    private static bool LooksLikeDanglingFragmentTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalized = Regex.Replace(title.Trim(), @"\s+", " ").Trim(' ', '-', ':', ';', ',', '.');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var words = Regex.Matches(normalized, @"[A-Za-z][A-Za-z0-9+\-#]*")
            .Select(match => match.Value)
            .ToList();
        if (words.Count is 0 or > 8)
        {
            return false;
        }

        var lastWord = words[^1];
        if (!Regex.IsMatch(lastWord, @"^(whose|which|that|with|without|for|from|into|onto|than|then|and|or|to|of|by|via|where|when|while|is|are|was|were|has|have|had|can|should|may)$", RegexOptions.IgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string? GetFirstStructuralCandidateLine(IReadOnlyList<string> lines)
    {
        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var line = rawLine.Trim();
            if (IsIgnorableStructuralLine(line) || IsLowValueStructuralTitle(line))
            {
                continue;
            }

            return line;
        }

        return null;
    }

    private static string StripPlaceholderArtifacts(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value;
        normalized = Regex.Replace(normalized, @"\[\[IMAGE:[^\]]*\]\]", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\|\s*(ref|label|slide|page)\s*=\s*[^\|\]]+", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"(?:^|\s)(ref|label|slide|page)\s*=\s*[^\s\]]+\]?", string.Empty, RegexOptions.IgnoreCase);
        normalized = normalized.Replace("]]", string.Empty, StringComparison.Ordinal);
        return normalized;
    }

    private static bool IsLowValueContentBlock(string block)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return true;
        }

        var meaningfulLines = block
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !IsIgnorableStructuralLine(line))
            .Where(line => !IsLowValueStructuralTitle(line))
            .ToList();

        return meaningfulLines.Count == 0;
    }

    private static bool IsImageSummaryHeaderLine(string? line)
        => !string.IsNullOrWhiteSpace(line) &&
           line.StartsWith("[Image Summary:", StringComparison.OrdinalIgnoreCase);

    private static bool IsMarkdownImageLine(string? line)
        => !string.IsNullOrWhiteSpace(line) &&
           Regex.IsMatch(line.Trim(), @"^!\[[^\]]*\]\([^)]+\)$");

    private static bool IsSyntheticDocumentHeader(string line)
        => line.StartsWith("Parsed Document:", StringComparison.OrdinalIgnoreCase) ||
           IsCourseBannerHeading(line);

    private static bool IsCourseBannerHeading(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = Regex.Replace(line.Trim(), @"\s+", " ");
        return Regex.IsMatch(
            normalized,
            @"(?i)^(data structures? and algorithms?(?: in [a-z0-9+#\.\- ]+)?|programming fundamentals|introduction to programming|java oop|object[- ]oriented programming|computer science fundamentals|software engineering fundamentals)$");
    }

    private static bool IsInstitutionalBannerLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = Regex.Replace(line.Trim(), @"\s+", " ");
        return Regex.IsMatch(normalized,
            @"(?i)\b(fpt university|truong dai hoc fpt|trường đại học fpt|qs stars(?:\s+(?:rated|rating|ranking))?\s+for\s+excellence|stars\s+(?:rated|rating|ranking)\s+for\s+excellence|rating\s+for\s+excellence|ranking\s+for\s+excellence)\b");
    }

    private static int InferHeadingLevelFromNumbering(string numbering)
    {
        if (Regex.IsMatch(numbering, @"^[IVXLCM]+$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(numbering, @"^[A-Z]$", RegexOptions.IgnoreCase))
        {
            return 1;
        }

        return numbering.Count(ch => ch == '.') + 1;
    }

    private static bool LooksLikeStandaloneHeading(string line, string? nextLine)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > 100)
        {
            return false;
        }

        if (IsLowValueStructuralTitle(line) || IsSlidePageCounterLine(line) || LooksLikeDanglingFragmentTitle(line))
        {
            return false;
        }

        if (line.EndsWith(".", StringComparison.Ordinal) ||
            line.EndsWith(";", StringComparison.Ordinal) ||
            line.EndsWith(",", StringComparison.Ordinal) ||
            line.EndsWith("?", StringComparison.Ordinal) ||
            line.EndsWith("!", StringComparison.Ordinal))
        {
            return false;
        }

        if (Regex.IsMatch(line, @"(?i)^(note|warning|example|ví dụ|ghi chú|remark)\b"))
        {
            return false;
        }

        if (LooksLikeCodeAssignmentTitle(line) || LooksLikeReadingReferenceTitle(line))
        {
            return false;
        }

        var words = Regex.Matches(line, @"\p{L}[\p{L}\p{N}_\-]*")
            .Select(match => match.Value)
            .ToList();

        if (words.Count is 0 or > 12)
        {
            return false;
        }

        var uppercaseRatio = words.Count(word => word.All(ch => !char.IsLetter(ch) || char.IsUpper(ch))) / (double)words.Count;
        var titleCaseRatio = words.Count(word => char.IsUpper(word[0])) / (double)words.Count;
        var looksStructured = uppercaseRatio >= 0.6 || titleCaseRatio >= 0.6;
        var nextLineLooksLikeBody = !string.IsNullOrWhiteSpace(nextLine) &&
                                    nextLine.Length > 30 &&
                                    !Regex.IsMatch(nextLine, @"^(#{1,6})\s+", RegexOptions.IgnoreCase);

        return looksStructured && nextLineLooksLikeBody;
    }

    private static string? FindFallbackSectionTitle(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var candidates = content
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !IsIgnorableStructuralLine(line))
            .Select(CleanStructuralTitle)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Cast<string>()
            .Where(line => !IsGenericSectionOnlyHeading(line))
            .Where(line => !LooksLikeReadingReferenceTitle(line))
            .Where(line => !LooksLikeCodeAssignmentTitle(line))
            .Where(line => !LooksCodeHeavy(line))
            .Where(line => line.Length <= 100)
            .Where(line => Regex.Matches(line, @"[A-Za-z][A-Za-z0-9+\-#]*").Count is >= 2 and <= 12)
            .Where(line => !Regex.IsMatch(line, @"[{};]"))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var preferred = candidates
            .OrderByDescending(line => !string.IsNullOrWhiteSpace(ExtractCanonicalConceptTitle(line)) || LooksLikeChapterTitle(line))
            .ThenByDescending(line => Regex.IsMatch(line, @"(?i)\b(stack|queue|deque|linked list|linked lists|doubly linked list|singly linked list|circular list|array|arrays|tree|graph|sorting|searching|recursion|complexity)\b"))
            .ThenBy(line => line.Length)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(preferred) ? null : preferred;
    }

    private static bool IsCompactConceptHeading(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (line.Length > 60 || LooksCodeHeavy(line))
        {
            return false;
        }

        if (Regex.IsMatch(line, @"[\.!?,;:{}()\[\]=]"))
        {
            return false;
        }

        var words = Regex.Matches(line, @"\p{L}[\p{L}\p{N}_\-]*")
            .Select(match => match.Value)
            .ToList();

        return words.Count is >= 1 and <= 5;
    }

    private static bool LooksLikeCodeAssignmentTitle(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = Regex.Replace(line.Trim(), @"\s+", " ");
        if (normalized.Length > 80)
        {
            return false;
        }

        if (Regex.IsMatch(normalized, @"(?i)^(return|throw|case|default|break|continue)\b"))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^[A-Za-z_][A-Za-z0-9_\[\]\.]*\s*=\s*[^=].*$"))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^[A-Za-z_][A-Za-z0-9_]*\s*\([^)]*\)\s*\{?$"))
        {
            return true;
        }

        return Regex.IsMatch(normalized, @"[{};]") || LooksCodeHeavy(normalized);
    }

    private static bool LooksLikeReadingReferenceTitle(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = Regex.Replace(line.Trim(), @"\s+", " ");
        if (Regex.IsMatch(normalized, @"(?i)^(reading at home|reading|read at home|homework reading)$"))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"(?i)^\**\d+(\.\d+)+\**\s+.+\s+-\s+page\s+\d+$"))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"(?i)^\**\d+(\.\d+)+\**\s+.+\s+-\s+\d+$"))
        {
            return true;
        }

        return false;
    }

    private sealed record HeadingMetadata(string Title, int Level, bool IsChapterHeading, bool IsDocumentHeader = false, bool IsAiStructural = false);
    private sealed record StructuralBlock(string Content, ExtractedStructuralUnitDto? Unit);
    private sealed record StructuralHintIndex(
        List<string> DisplayTitles,
        HashSet<string> TitleKeys,
        HashSet<string> StrongChapterTitleKeys);

    private sealed class ChunkDraft
    {
        public string Content { get; set; } = string.Empty;
        public string ChunkType { get; set; } = "content";
        public string? SectionTitle { get; set; }
        public string? ChapterKey { get; set; }
        public string? ChapterTitle { get; set; }
        public int? ChapterOrder { get; set; }
        public List<string> SectionPath { get; set; } = new();
        public int? SectionLevel { get; set; }
        public List<string> QuestionTypeAffinity { get; set; } = new();
    }

    private sealed class TagGenerationResult
    {
        public List<string> Tags { get; set; } = new();
        public AITextResponse? Response { get; set; }
        public bool UsedFallback { get; set; }
    }

    private sealed record IndexedChunkDraft(int Index, ChunkDraft Block);
    private sealed record BatchModelTagItem(int Index, List<string> Tags);
    private sealed record TaggedChunkResult(
        int Index,
        ChunkDraft Block,
        string? SubjectCode,
        List<string> HeuristicSeedTags,
        List<string> Tags);

    private sealed class BatchTagGenerationResult
    {
        public List<TaggedChunkResult> Items { get; set; } = new();
        public AITextResponse? Response { get; set; }
        public int FallbackChunkCount { get; set; }
    }

    private static List<string> BuildDeterministicTags(ChunkDraft block, IReadOnlyList<string>? allowedTags, string? subjectCode)
    {
        return MergeTags(
            ExtractInlineConceptTags(block.Content).ToList(),
            ExtractHeuristicTags(block.Content, block.SectionTitle, block.ChapterTitle, allowedTags, subjectCode),
            allowedTags,
            subjectCode);
    }

    private static List<string> ParseModelTags(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return new List<string>();
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(rawContent);
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return document.RootElement.EnumerateArray()
                    .Where(item => item.ValueKind == System.Text.Json.JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList();
            }
        }
        catch
        {
        }

        return rawContent
            .Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Trim().Trim('"', '[', ']', '*', '-', ' '))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static List<BatchModelTagItem> ParseBatchModelTags(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return new List<BatchModelTagItem>();
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(rawContent);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return new List<BatchModelTagItem>();
            }

            var items = new List<BatchModelTagItem>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != System.Text.Json.JsonValueKind.Object ||
                    !element.TryGetProperty("index", out var indexElement) ||
                    !indexElement.TryGetInt32(out var index) ||
                    !element.TryGetProperty("tags", out var tagsElement) ||
                    tagsElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                {
                    continue;
                }

                var tags = tagsElement.EnumerateArray()
                    .Where(item => item.ValueKind == System.Text.Json.JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList();

                items.Add(new BatchModelTagItem(index, tags));
            }

            return items;
        }
        catch
        {
            return new List<BatchModelTagItem>();
        }
    }

    private static List<string> MergeTags(
        IReadOnlyList<string> modelTags,
        IReadOnlyList<string> heuristicTags,
        IReadOnlyList<string>? allowedTags,
        string? subjectCode,
        AITagTaxonomyCatalog? taxonomyCatalog = null)
    {
        var merged = new List<string>();

        if (!string.IsNullOrWhiteSpace(subjectCode))
        {
            merged.Add(subjectCode.ToLowerInvariant());
        }

        merged.AddRange(heuristicTags);
        merged.AddRange(modelTags);

        var normalized = merged
            .Select(tag => NormalizeTag(tag, allowedTags, taxonomyCatalog))
            .Where(tag => !string.IsNullOrWhiteSpace(tag) && !IsLowValueTag(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allowedTags is { Count: > 0 })
        {
            normalized = normalized.Where(tag => allowedTags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        normalized = ApplySubjectSpecificTagFilter(normalized, subjectCode);

        if (normalized.Count > 1)
        {
            normalized = normalized
                .Where(tag => !string.Equals(tag, "general", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (normalized.Count == 0)
        {
            normalized.Add("general");
        }

        return normalized.Take(8).ToList();
    }

    private static List<string> ApplySubjectSpecificTagFilter(List<string> normalized, string? subjectCode)
    {
        if (normalized.Count == 0 || string.IsNullOrWhiteSpace(subjectCode))
        {
            return normalized;
        }

        var subject = NormalizeSubjectKey(subjectCode);
        if (string.Equals(subject, AISubjectDomainMapper.JavaOop, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized
                .Where(tag => !string.Equals(tag, "c", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else if (string.Equals(subject, AISubjectDomainMapper.C, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized
                .Where(tag => !string.Equals(tag, "java", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tag, AISubjectDomainMapper.JavaOop, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return normalized;
    }

    private static bool TagHasEvidence(string tag, ChunkDraft block, string? subjectCode, AITagTaxonomyCatalog taxonomyCatalog)
    {
        var normalized = NormalizeTag(tag, taxonomyCatalog.ResolveBySubject(subjectCode)?.CanonicalTags, taxonomyCatalog);
        if (string.IsNullOrWhiteSpace(normalized) || IsLowValueTag(normalized))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(subjectCode) && string.Equals(normalized, subjectCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var evidenceText = $"{block.ChapterTitle} {block.SectionTitle} {block.Content}".ToLowerInvariant();
        if (evidenceText.Contains(normalized, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var keyword in ExpandTagEvidenceKeywords(normalized))
        {
            if (evidenceText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> ExtractStructuralPhraseTags(string? sectionTitle, string? chapterTitle)
    {
        foreach (var source in new[] { chapterTitle, sectionTitle })
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            var cleaned = Regex.Replace(source, @"^\d+(\.\d+)*\s*", string.Empty).Trim();
            cleaned = Regex.Replace(cleaned, @"\((cont\.?|continued)\)", string.Empty, RegexOptions.IgnoreCase).Trim();
            cleaned = cleaned.Trim('-', ':');
            if (cleaned.Length is > 2 and <= 60)
            {
                yield return cleaned;
            }
        }
    }

    private static IEnumerable<string> ExtractInlineConceptTags(string content)
    {
        var matches = Regex.Matches(
            content,
            @"(?i)\b(named constants?|escape sequences?|integral values?|floating-point types?|bitwise operators?|logical operators?|relational operators?|assignment operators?|operator precedence|mixing data types|input/?output variables?|arithmetic operators?|variables?|literals?|representation|ascii)\b");

        foreach (Match match in matches)
        {
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                yield return match.Value;
            }
        }
    }

    private static string? InferSubjectCode(string content, string? sectionTitle, string? chapterTitle)
    {
        var lowered = $"{chapterTitle} {sectionTitle} {content}".ToLowerInvariant();
        if (Regex.IsMatch(lowered, @"\b(printf|scanf|#include|main\s*\(|char|int|float|double|const)\b"))
        {
            return AISubjectDomainMapper.C;
        }

        if (Regex.IsMatch(lowered, @"\b(public\s+static\s+void|system\.out|class\s+\w+|abstract\s+class|extends|implements|override|polymorphism)\b"))
        {
            return AISubjectDomainMapper.JavaOop;
        }

        return null;
    }

    private static string NormalizeTag(string? rawTag, IReadOnlyList<string>? allowedTags, AITagTaxonomyCatalog? taxonomyCatalog = null)
    {
        if (string.IsNullOrWhiteSpace(rawTag))
        {
            return string.Empty;
        }

        var normalized = rawTag.Trim().Trim('"', '\'', '.', ',', ';', ':', '-', '_', '[', ']', '(', ')');
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim().ToLowerInvariant();
        normalized = normalized switch
        {
            "constant" => "constants",
            "named constant" => "constants",
            "variable" => "variables",
            "literal" => "literals",
            "integral value" => "integral types",
            "integral values" => "integral types",
            "integer types" => "integral types",
            "float" => "floating-point types",
            "double" => "floating-point types",
            "floating point types" => "floating-point types",
            "arithmetic operator" => "arithmetic operators",
            "relational operator" => "relational operators",
            "logical operator" => "logical operators",
            "bitwise operator" => "bitwise operators",
            "assignment operator" => "assignment operators",
            "input output" => "input/output",
            "input/output variables" => "input/output",
            "operator precedence and associativity" => "operator precedence",
            _ => normalized
        };

        if (allowedTags is { Count: > 0 })
        {
            var exact = allowedTags.FirstOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exact))
            {
                return exact.ToLowerInvariant();
            }

            var taxonomy = taxonomyCatalog?.FindCanonicalTag(normalized, allowedTags);
            if (!string.IsNullOrWhiteSpace(taxonomy))
            {
                return taxonomy!;
            }
        }

        return normalized;
    }

    private static bool IsLowValueTag(string tag)
    {
        return tag is "topic" or "concept" or "study content" or "computer science" or "image summary"
            || Regex.IsMatch(tag, @"^\d+$")
            || Regex.IsMatch(tag, @"^image summary: ");
    }

    private static void AddScore(Dictionary<string, int> scores, string tag, int score)
    {
        var normalized = NormalizeTag(tag, null);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        scores[normalized] = scores.TryGetValue(normalized, out var current)
            ? current + score
            : score;
    }

    private static IEnumerable<string> ExpandTagEvidenceKeywords(string tag)
    {
        return tag switch
        {
            "c" => new[] { "printf", "scanf", "#include", "main(", "char", "int", "float", "double", "const" },
            "java" => new[] { "class", "system.out", "public static void" },
            "classes" => new[] { "class", "classes" },
            "objects" => new[] { "object", "objects", "instance" },
            "methods" => new[] { "method", "methods" },
            "inheritance" => new[] { "inheritance", "extends", "base class", "derived class" },
            "polymorphism" => new[] { "polymorphism", "dynamic dispatch", "override", "overriding" },
            "abstraction" => new[] { "abstraction", "abstract method", "abstract class" },
            "abstract classes" => new[] { "abstract class", "abstract classes" },
            "method overriding" => new[] { "override", "overriding", "@override" },
            "interfaces" => new[] { "interface", "implements" },
            "constants" => new[] { "constant", "constants", "const", "named constant" },
            "variables" => new[] { "variable", "variables", "identifier", "declare" },
            "integral types" => new[] { "integral", "integer", "char", "short", "long", "signed", "unsigned" },
            "floating-point types" => new[] { "float", "double", "long double", "floating-point", "real number" },
            "literals" => new[] { "literal", "literals", "suffix", "escape sequence" },
            "input/output" => new[] { "input", "output", "printf", "scanf" },
            "arithmetic operators" => new[] { "arithmetic operator", "quotient", "remainder", "+", "-", "*", "/", "%" },
            "relational operators" => new[] { "relational", "==", "!=", "<", ">", "<=", ">=" },
            "logical operators" => new[] { "logical", "&&", "||", "!" },
            "bitwise operators" => new[] { "bitwise", "<<", ">>", "^", "~", "flip bits" },
            "assignment operators" => new[] { "assignment", "+=", "-=", "*=", "/=", "%=" },
            "operator precedence" => new[] { "precedence", "associativity" },
            "type conversion" => new[] { "mixing data types", "conversion", "cast" },
            "representation" => new[] { "representation", "binary", "byte", "memory", "little-endian", "big-endian" },
            "ascii" => new[] { "ascii" },
            _ => Array.Empty<string>()
        };
    }

    private static string? ResolveSubjectCode(string? subjectCode, string content, AITagTaxonomyCatalog taxonomyCatalog)
    {
        var explicitSubject = NormalizeSubjectKey(subjectCode);
        if (!string.IsNullOrWhiteSpace(explicitSubject))
        {
            return taxonomyCatalog.ResolveBySubject(explicitSubject)?.SubjectCode ?? explicitSubject;
        }

        var inferred = NormalizeSubjectKey(InferSubjectCode(content, null, null));
        return taxonomyCatalog.ResolveBySubject(inferred)?.SubjectCode ?? inferred;
    }

    private static string? NormalizeSubjectKey(string? subjectCode)
    {
        if (string.IsNullOrWhiteSpace(subjectCode))
        {
            return null;
        }

        return AISubjectDomainMapper.NormalizeSubjectOrCourseCode(subjectCode);
    }

}
