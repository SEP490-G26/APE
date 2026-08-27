using System.Text;
using System.Text.RegularExpressions;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public class RetrievalPlannerService : IRetrievalPlannerService
{
    private const int MinimumPackedTokenBudget = 512;
    private const int ShortChunkPassthroughTokenThreshold = 96;
    private const int ShortChunkSectionExpansionTokenThreshold = 72;
    private const int MinimumChunkCharacters = 80;
    private const int MaximumChunksPerSectionKey = 2;
    private const int MaximumShortChunksPerSectionKey = 3;
    private const int MaximumPrimaryChunks = 4;
    private const int MaximumOptionalChunks = 2;
    private const int DenseChunkPackedPreviewCharacterLimit = 1000;
    private const int DenseChunkSnippetCharacterLimit = 220;
    private const int NearPageDistanceThreshold = 3;
    private const int FarPageDistanceThreshold = 12;
    private const decimal CoverageFillMinimumTopicScore = 0.25m;

    private readonly IKnowledgeRetrievalRepository _knowledgeRetrievalRepository;
    private readonly IAIContextPackRepository _contextPackRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IAITagTaxonomyProvider _tagTaxonomyProvider;

    public RetrievalPlannerService(
        IKnowledgeRetrievalRepository knowledgeRetrievalRepository,
        IAIContextPackRepository contextPackRepository,
        IDocumentRepository documentRepository,
        IAITagTaxonomyProvider tagTaxonomyProvider)
    {
        _knowledgeRetrievalRepository = knowledgeRetrievalRepository;
        _contextPackRepository = contextPackRepository;
        _documentRepository = documentRepository;
        _tagTaxonomyProvider = tagTaxonomyProvider;
    }

    public async Task<RetrievalPlanResultDto> PlanAsync(RetrievalPlanRequestDto request, CancellationToken cancellationToken = default)
    {
        NormalizeRequest(request);
        await ValidateRequestAsync(request, cancellationToken);
        var planId = $"rp-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..36];

        if (request.UseCachedPack && !request.ForceRebuildPack)
        {
            var cached = await TryReuseCachedPackAsync(request, cancellationToken);
            if (cached is not null)
            {
                cached.LastRetrievalPlanId = planId;
                cached.LastRetrievedAt = DateTime.UtcNow;
                cached.UpdatedAt = DateTime.UtcNow;
                await _contextPackRepository.UpdateAsync(cached);
                return BuildCachedResult(request, cached, planId);
            }
        }

        var requestedChapterKeys = ResolveRequestedChapterKeys(request.ChapterKey, request.ChapterKeys);
        var primaryChapterKey = requestedChapterKeys.FirstOrDefault();

        var sourceChunks = await _knowledgeRetrievalRepository.SearchKnowledgeChunksAsync(
            request.UserId,
            request.CourseId,
            request.DocumentId,
            requestedChapterKeys.Count <= 1 ? primaryChapterKey : null,
            request.Subject,
            request.SourceScope,
            cancellationToken);
        sourceChunks = FilterChunksByRequestedChapterKeys(sourceChunks, requestedChapterKeys);

        sourceChunks = await ExpandChapterSupportChunksAsync(request, sourceChunks, cancellationToken);

        if (sourceChunks.Count == 0)
        {
            throw new InvalidOperationException("Retrieval planner could not find any chunks for the requested scope.");
        }

        var preFiltered = sourceChunks
            .Where(chunk => string.IsNullOrWhiteSpace(request.Language) ||
                            string.Equals(chunk.Language, request.Language, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(chunk.Language, "mixed", StringComparison.OrdinalIgnoreCase))
            .Where(chunk => IsChunkUsable(chunk, request.TargetTopics, request.QuestionType))
            .ToList();

        var scored = preFiltered
            .Select(chunk => BuildRetrievedChunk(chunk, request.QuestionType, request.TargetTopics, request.RetrievalQuery, request.IncludeChunkText, primaryChapterKey, requestedChapterKeys))
            .Where(chunk => !ShouldExcludeOffTopicAbstractClassSubtopic(chunk, request.TargetTopics))
            .Where(chunk => !ShouldExcludeLowValueChunkForQuestionType(chunk, request.TargetTopics, request.QuestionType))
            .OrderByDescending(chunk => chunk.FinalScore)
            .ThenByDescending(chunk => chunk.TopicScore)
            .ThenByDescending(chunk => chunk.LexicalScore)
            .ThenBy(chunk => chunk.TokenCount)
            .Take(Math.Max(1, request.MaxCandidateCount))
            .ToList();

        var selected = PackChunks(
            scored,
            request.QuestionType,
            request.MaxPackedTokens,
            request.TargetTopics,
            request.RetrievalQuery,
            requestedChapterKeys);
        if (selected.Count == 0)
        {
            selected = scored.Take(Math.Min(3, scored.Count)).ToList();
        }

        var coverage = BuildTopicCoverage(selected, request.TargetTopics);

        var sourceTokenCount = preFiltered.Sum(item => item.TokenCount > 0 ? item.TokenCount : EstimateTokenCount(item.RawText));
        var contextPack = BuildContextPack(request, selected, sourceTokenCount, planId, coverage);
        await _contextPackRepository.CreateAsync(contextPack);

        return new RetrievalPlanResultDto
        {
            PlanId = planId,
            PackId = contextPack.Id,
            UsedCachedPack = false,
            Subject = request.Subject,
            QuestionType = request.QuestionType,
            Difficulty = request.Difficulty,
            GenerationMode = request.GenerationMode,
            TargetTopics = request.TargetTopics,
            RetrievalQuery = request.RetrievalQuery,
            TokenBudget = new RetrievalTokenBudgetDto
            {
                MaxPackedTokens = request.MaxPackedTokens,
                EstimatedPackedTokens = contextPack.TokenCount,
                EstimatedSourceTokens = sourceTokenCount,
                CompressionRatio = sourceTokenCount == 0 ? 1m : Math.Round((decimal)contextPack.TokenCount / sourceTokenCount, 4)
            },
            CandidateStats = new RetrievalCandidateStatsDto
            {
                PreFilterCount = preFiltered.Count,
                TopicMatchCount = preFiltered.Count(chunk => OverlapCount(chunk.TopicTags, request.TargetTopics) > 0),
                RankedCount = scored.Count,
                SelectedChunkCount = selected.Count,
                CoveredTopicCount = coverage.CoveredTopics.Count,
                RequestedTopicCount = request.TargetTopics.Count,
                TopicCoverageRatio = coverage.TopicCoverageRatio
            },
            SelectedChunks = selected.Select(MapRetrievedChunkDto).ToList(),
            ContextPack = MapContextPackDto(contextPack)
        };
    }

    private async Task<List<KnowledgeChunk>> ExpandChapterSupportChunksAsync(
        RetrievalPlanRequestDto request,
        List<KnowledgeChunk> sourceChunks,
        CancellationToken cancellationToken)
    {
        var requestedChapterKeys = ResolveRequestedChapterKeys(request.ChapterKey, request.ChapterKeys);
        var primaryChapterKey = requestedChapterKeys.FirstOrDefault();
        if (sourceChunks.Count == 0 || string.IsNullOrWhiteSpace(primaryChapterKey))
        {
            return sourceChunks;
        }

        var requestedChapterChunks = sourceChunks
            .Where(chunk => requestedChapterKeys.Contains(chunk.ChapterKey ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (requestedChapterChunks.Count == 0)
        {
            return sourceChunks;
        }

        if (ShouldKeepStrictRequestedChapterScope(request, requestedChapterKeys, requestedChapterChunks))
        {
            return sourceChunks;
        }

        var broadChunks = await _knowledgeRetrievalRepository.SearchKnowledgeChunksAsync(
            request.UserId,
            request.CourseId,
            request.DocumentId,
            null,
            request.Subject,
            request.SourceScope,
            cancellationToken);
        if (broadChunks.Count <= sourceChunks.Count)
        {
            return sourceChunks;
        }

        var requestedIds = sourceChunks
            .Select(chunk => chunk.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestedPages = requestedChapterChunks
            .SelectMany(chunk => new[] { chunk.SourcePageFrom, chunk.SourcePageTo })
            .Where(page => page.HasValue)
            .Select(page => page!.Value)
            .Distinct()
            .ToList();
        var requestedOrders = requestedChapterChunks
            .Where(chunk => chunk.ChapterOrder.HasValue)
            .Select(chunk => chunk.ChapterOrder!.Value)
            .Distinct()
            .ToList();

        var supportCandidates = broadChunks
            .Where(chunk => !requestedIds.Contains(chunk.Id))
            .Where(chunk => !requestedChapterKeys.Contains(chunk.ChapterKey ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            .Where(chunk => IsChunkUsable(chunk, request.TargetTopics, request.QuestionType))
            .Select(chunk => new
            {
                Chunk = chunk,
                TopicScore = ComputeTopicScore(chunk, request.TargetTopics),
                ChapterDistance = ComputeChapterOrderDistance(chunk, requestedOrders),
                PageDistance = ComputePageDistance(chunk, requestedPages)
            })
            .Where(item => item.TopicScore >= 0.5m || OverlapCount(item.Chunk.TopicTags ?? new List<string>(), request.TargetTopics) > 0)
            .Where(item => item.ChapterDistance <= 1 || item.PageDistance <= NearPageDistanceThreshold)
            .OrderByDescending(item => item.TopicScore)
            .ThenBy(item => item.ChapterDistance)
            .ThenBy(item => item.PageDistance)
            .ThenBy(item => item.Chunk.TokenCount > 0 ? item.Chunk.TokenCount : EstimateTokenCount(item.Chunk.MarkdownText ?? item.Chunk.NormalizedText ?? item.Chunk.RawText))
            .Take(4)
            .Select(item => item.Chunk)
            .ToList();

        var exactTopicSupport = broadChunks
            .Where(chunk => !requestedIds.Contains(chunk.Id))
            .Where(chunk => !requestedChapterKeys.Contains(chunk.ChapterKey ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            .Where(chunk => IsChunkUsable(chunk, request.TargetTopics, request.QuestionType))
            .Where(chunk => HasExactTopicEvidence(chunk, request.TargetTopics))
            .OrderByDescending(chunk => ComputeTopicScore(chunk, request.TargetTopics))
            .ThenByDescending(chunk => ComputeLexicalScore(chunk, request.RetrievalQuery, request.TargetTopics))
            .ThenBy(chunk => ComputeChapterOrderDistance(chunk, requestedOrders))
            .ThenBy(chunk => ComputePageDistance(chunk, requestedPages))
            .Take(3)
            .ToList();

        var explicitTopicSupport = broadChunks
            .Where(chunk => !requestedIds.Contains(chunk.Id))
            .Where(chunk => !requestedChapterKeys.Contains(chunk.ChapterKey ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            .Where(chunk => IsChunkUsable(chunk, request.TargetTopics, request.QuestionType))
            .Where(chunk => MatchesExplicitTopicAnchor(chunk, request.TargetTopics))
            .OrderByDescending(chunk => ComputeTopicScore(chunk, request.TargetTopics))
            .ThenBy(chunk => ComputeChapterOrderDistance(chunk, requestedOrders))
            .ThenBy(chunk => ComputePageDistance(chunk, requestedPages))
            .Take(2)
            .ToList();

        var expanded = exactTopicSupport
            .Concat(explicitTopicSupport)
            .Concat(supportCandidates)
            .GroupBy(chunk => chunk.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(5)
            .ToList();

        if (expanded.Count == 0)
        {
            return sourceChunks;
        }

        return sourceChunks
            .Concat(expanded)
            .GroupBy(chunk => chunk.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static bool ShouldKeepStrictRequestedChapterScope(
        RetrievalPlanRequestDto request,
        IReadOnlyList<string> requestedChapterKeys,
        IReadOnlyList<KnowledgeChunk> requestedChapterChunks)
    {
        if (requestedChapterKeys.Count != 1)
        {
            return false;
        }

        if (!string.Equals(request.SourceScope, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HasSufficientRequestedChapterEvidence(request, requestedChapterChunks, request.TargetTopics);
    }

    private static bool HasSufficientRequestedChapterEvidence(
        RetrievalPlanRequestDto request,
        IReadOnlyList<KnowledgeChunk> requestedChapterChunks,
        IReadOnlyList<string> targetTopics)
    {
        if (requestedChapterChunks.Count == 0)
        {
            return false;
        }

        var totalTokens = requestedChapterChunks.Sum(chunk => chunk.TokenCount > 0 ? chunk.TokenCount : EstimateTokenCount(chunk.RawText));
        var topicAnchoredChunks = requestedChapterChunks.Count(chunk => OverlapCount(chunk.TopicTags, targetTopics) > 0);
        var structuredChunks = requestedChapterChunks.Count(chunk =>
            !string.IsNullOrWhiteSpace(chunk.SectionTitle) &&
            LooksStructuredSection(chunk.SectionTitle));

        if (topicAnchoredChunks >= 2 && totalTokens >= 180)
        {
            return true;
        }

        var isEasySingleQuestion =
            string.Equals(request.Difficulty, "Easy", StringComparison.OrdinalIgnoreCase) &&
            request.RequestedQuestionCount <= 1;
        if (isEasySingleQuestion &&
            topicAnchoredChunks >= 1 &&
            totalTokens >= 45)
        {
            return true;
        }

        return topicAnchoredChunks >= 1 &&
               structuredChunks >= 2 &&
               totalTokens >= 260;
    }

    public async Task<TopicSummaryResultDto> GetSystemTopicSummaryAsync(string userId, string courseId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(courseId))
        {
            throw new ArgumentException("courseId is required.");
        }

        var chunks = await _knowledgeRetrievalRepository.SearchKnowledgeChunksAsync(
            userId,
            courseId,
            null,
            null,
            string.Empty,
            "SYSTEM",
            cancellationToken);

        return BuildTopicSummary(
            scope: "SYSTEM",
            courseId: courseId,
            documentId: null,
            documentName: null,
            chapterKey: null,
            chapterKeys: null,
            chapterTitle: null,
            subject: chunks.Select(chunk => chunk.SubjectCode).FirstOrDefault(subject => !string.IsNullOrWhiteSpace(subject)),
            chunks: chunks);
    }

    public async Task<TopicSummaryResultDto> GetSystemDocumentChapterTopicSummaryAsync(string userId, string courseId, string documentId, string chapterKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(courseId))
        {
            throw new ArgumentException("courseId is required.");
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("documentId is required.");
        }

        if (string.IsNullOrWhiteSpace(chapterKey))
        {
            throw new ArgumentException("chapterKey is required.");
        }

        var document = await _documentRepository.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (!string.Equals(document.CourseId, courseId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Document does not belong to the requested course.");
        }

        if (string.Equals(document.Source, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("System chapter topic summary does not support BYOS documents.");
        }

        var chunks = await _knowledgeRetrievalRepository.SearchKnowledgeChunksAsync(
            userId,
            null,
            documentId,
            chapterKey,
            string.Empty,
            "SYSTEM",
            cancellationToken);

        return BuildTopicSummary(
            scope: "SYSTEM",
            courseId: courseId,
            documentId: documentId,
            documentName: document.FileName,
            chapterKey: chapterKey,
            chapterKeys: new[] { chapterKey },
            chapterTitle: ResolveRequestedChapterTitle(chunks, chapterKey),
            subject: document.SubjectCode ?? chunks.Select(chunk => chunk.SubjectCode).FirstOrDefault(subject => !string.IsNullOrWhiteSpace(subject)),
            chunks: chunks);
    }

    public async Task<TopicSummaryResultDto> GetSystemDocumentMultiChapterTopicSummaryAsync(string userId, string courseId, string documentId, IReadOnlyList<string> chapterKeys, CancellationToken cancellationToken = default)
    {
        var normalizedChapterKeys = ResolveRequestedChapterKeys(null, chapterKeys);
        if (normalizedChapterKeys.Count == 0)
        {
            throw new ArgumentException("At least one chapterKey is required.");
        }

        var document = await _documentRepository.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (!string.Equals(document.CourseId, courseId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Document does not belong to the requested course.");
        }

        if (string.Equals(document.Source, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("System chapter topic summary does not support BYOS documents.");
        }

        var chunks = await _knowledgeRetrievalRepository.SearchKnowledgeChunksAsync(
            userId,
            null,
            documentId,
            null,
            string.Empty,
            "SYSTEM",
            cancellationToken);
        chunks = FilterChunksByRequestedChapterKeys(chunks, normalizedChapterKeys);

        return BuildTopicSummary(
            scope: "SYSTEM",
            courseId: courseId,
            documentId: documentId,
            documentName: document.FileName,
            chapterKey: normalizedChapterKeys.Count == 1 ? normalizedChapterKeys[0] : null,
            chapterKeys: normalizedChapterKeys,
            chapterTitle: normalizedChapterKeys.Count == 1 ? ResolveRequestedChapterTitle(chunks, normalizedChapterKeys[0]) : $"{normalizedChapterKeys.Count} chapters selected",
            subject: document.SubjectCode ?? chunks.Select(chunk => chunk.SubjectCode).FirstOrDefault(subject => !string.IsNullOrWhiteSpace(subject)),
            chunks: chunks);
    }

    public async Task<List<GenerationSourceDocumentDto>> GetSystemSourceDocumentsAsync(string userId, string courseId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(courseId))
        {
            throw new ArgumentException("courseId is required.");
        }

        var (documents, _) = await _documentRepository.ListByCourseAsync(courseId, 1, 200);
        var systemDocs = documents
            .Where(document =>
                document.IsActive &&
                document.Status == Domain.Enums.DocumentStatus.Completed &&
                !string.Equals(document.Source, "BYOS", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(document => document.LastEmbeddedAt ?? document.CreatedAt)
            .ToList();

        var result = new List<GenerationSourceDocumentDto>(systemDocs.Count);
        foreach (var document in systemDocs)
        {
            var chunks = await _knowledgeRetrievalRepository.SearchKnowledgeChunksAsync(
                userId,
                null,
                document.Id,
                null,
                string.Empty,
                "SYSTEM",
                cancellationToken);

            result.Add(new GenerationSourceDocumentDto
            {
                DocumentId = document.Id,
                CourseId = document.CourseId,
                FileName = document.FileName,
                FileType = document.FileType,
                Source = document.Source ?? string.Empty,
                SubjectCode = document.SubjectCode,
                GatekeeperVerdict = document.GatekeeperVerdict,
                ChunkCount = chunks.Count,
                DistinctChapterCount = chunks
                    .Select(chunk => chunk.ChapterKey)
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                DistinctTopicCount = chunks
                    .SelectMany(chunk => chunk.TopicTags ?? new List<string>())
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                CreatedAt = document.CreatedAt,
                LastEmbeddedAt = document.LastEmbeddedAt
            });
        }

        return result;
    }

    public async Task<TopicSummaryResultDto> GetByosDocumentTopicSummaryAsync(string userId, string documentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("documentId is required.");
        }

        var document = await _documentRepository.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (!string.Equals(document.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You do not have permission to access this document topics summary.");
        }

        if (!string.Equals(document.Source, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Topic summary for this route only supports BYOS documents.");
        }

        var chunks = await _knowledgeRetrievalRepository.SearchKnowledgeChunksAsync(
            userId,
            null,
            documentId,
            null,
            string.Empty,
            "BYOS",
            cancellationToken);

        return BuildTopicSummary(
            scope: "BYOS",
            courseId: document.CourseId,
            documentId: documentId,
            documentName: document.FileName,
            chapterKey: null,
            chapterKeys: null,
            chapterTitle: null,
            subject: document.SubjectCode ?? chunks.Select(chunk => chunk.SubjectCode).FirstOrDefault(subject => !string.IsNullOrWhiteSpace(subject)),
            chunks: chunks);
    }

    public async Task<TopicSummaryResultDto> GetByosDocumentChapterTopicSummaryAsync(string userId, string documentId, string chapterKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("documentId is required.");
        }

        if (string.IsNullOrWhiteSpace(chapterKey))
        {
            throw new ArgumentException("chapterKey is required.");
        }

        var document = await _documentRepository.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (!string.Equals(document.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You do not have permission to access this document topics summary.");
        }

        if (!string.Equals(document.Source, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Topic summary for this route only supports BYOS documents.");
        }

        var chunks = await _knowledgeRetrievalRepository.SearchKnowledgeChunksAsync(
            userId,
            null,
            documentId,
            chapterKey,
            string.Empty,
            "BYOS",
            cancellationToken);

        return BuildTopicSummary(
            scope: "BYOS",
            courseId: document.CourseId,
            documentId: documentId,
            documentName: document.FileName,
            chapterKey: chapterKey,
            chapterKeys: new[] { chapterKey },
            chapterTitle: ResolveRequestedChapterTitle(chunks, chapterKey),
            subject: document.SubjectCode ?? chunks.Select(chunk => chunk.SubjectCode).FirstOrDefault(subject => !string.IsNullOrWhiteSpace(subject)),
            chunks: chunks);
    }

    public async Task<TopicSummaryResultDto> GetByosDocumentMultiChapterTopicSummaryAsync(string userId, string documentId, IReadOnlyList<string> chapterKeys, CancellationToken cancellationToken = default)
    {
        var normalizedChapterKeys = ResolveRequestedChapterKeys(null, chapterKeys);
        if (normalizedChapterKeys.Count == 0)
        {
            throw new ArgumentException("At least one chapterKey is required.");
        }

        var document = await _documentRepository.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (!string.Equals(document.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You do not have permission to access this document topics summary.");
        }

        if (!string.Equals(document.Source, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Topic summary for this route only supports BYOS documents.");
        }

        var chunks = await _knowledgeRetrievalRepository.SearchKnowledgeChunksAsync(
            userId,
            null,
            documentId,
            null,
            string.Empty,
            "BYOS",
            cancellationToken);
        chunks = FilterChunksByRequestedChapterKeys(chunks, normalizedChapterKeys);

        return BuildTopicSummary(
            scope: "BYOS",
            courseId: document.CourseId,
            documentId: documentId,
            documentName: document.FileName,
            chapterKey: normalizedChapterKeys.Count == 1 ? normalizedChapterKeys[0] : null,
            chapterKeys: normalizedChapterKeys,
            chapterTitle: normalizedChapterKeys.Count == 1 ? ResolveRequestedChapterTitle(chunks, normalizedChapterKeys[0]) : $"{normalizedChapterKeys.Count} chapters selected",
            subject: document.SubjectCode ?? chunks.Select(chunk => chunk.SubjectCode).FirstOrDefault(subject => !string.IsNullOrWhiteSpace(subject)),
            chunks: chunks);
    }

    public async Task<ChapterSummaryResultDto> GetSystemDocumentChapterSummaryAsync(string userId, string courseId, string documentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(courseId))
        {
            throw new ArgumentException("courseId is required.");
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("documentId is required.");
        }

        var document = await _documentRepository.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (!string.Equals(document.CourseId, courseId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Document does not belong to the requested course.");
        }

        if (string.Equals(document.Source, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("System chapter summary does not support BYOS documents.");
        }

        var chunks = await _knowledgeRetrievalRepository.SearchKnowledgeChunksAsync(
            userId,
            null,
            documentId,
            null,
            string.Empty,
            "SYSTEM",
            cancellationToken);

        return BuildChapterSummary(
            scope: "SYSTEM",
            courseId: courseId,
            documentId: documentId,
            documentName: document.FileName,
            subject: document.SubjectCode ?? chunks.Select(chunk => chunk.SubjectCode).FirstOrDefault(subject => !string.IsNullOrWhiteSpace(subject)),
            chunks: chunks);
    }

    public async Task<ChapterSummaryResultDto> GetByosDocumentChapterSummaryAsync(string userId, string documentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("documentId is required.");
        }

        var document = await _documentRepository.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (!string.Equals(document.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You do not have permission to access this BYOS document chapters summary.");
        }

        if (!string.Equals(document.Source, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chapter summary for this route only supports BYOS documents.");
        }

        var chunks = await _knowledgeRetrievalRepository.SearchKnowledgeChunksAsync(
            userId,
            null,
            documentId,
            null,
            string.Empty,
            "BYOS",
            cancellationToken);

        return BuildChapterSummary(
            scope: "BYOS",
            courseId: document.CourseId,
            documentId: documentId,
            documentName: document.FileName,
            subject: document.SubjectCode ?? chunks.Select(chunk => chunk.SubjectCode).FirstOrDefault(subject => !string.IsNullOrWhiteSpace(subject)),
            chunks: chunks);
    }

    public async Task<ContextPackDto?> GetContextPackAsync(string userId, string packId, CancellationToken cancellationToken = default)
    {
        var pack = await _contextPackRepository.GetByIdAsync(packId);
        if (pack is null)
        {
            return null;
        }

        if (!string.Equals(pack.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You do not have permission to access this context pack.");
        }

        return MapContextPackDto(pack);
    }

    public async Task<List<ContextPackSummaryDto>> ListContextPacksAsync(
        string userId,
        string? subject,
        string? questionType,
        string? difficulty,
        string? topic,
        string? status,
        int take,
        CancellationToken cancellationToken = default)
    {
        var packs = await _contextPackRepository.ListAsync(userId, subject, questionType, difficulty, topic, status, take);
        return packs.Select(pack => new ContextPackSummaryDto
        {
            PackId = pack.Id,
            CourseId = pack.CourseId,
            DocumentId = pack.DocumentId,
            SourceScope = pack.SourceScope,
            Subject = pack.Subject,
            QuestionType = pack.QuestionType,
            Difficulty = pack.TargetDifficulty,
            TargetTopics = pack.TargetTopics,
            PackStrategy = pack.PackStrategy,
            TokenCount = pack.TokenCount,
            RecommendedQuestionCount = pack.RecommendedQuestionCount,
            UsageCount = pack.UsageCount,
            PackStatus = pack.PackStatus,
            LastGenerationStatus = pack.LastGenerationStatus,
            CoveredTopics = pack.CoveredTopics,
            UncoveredTopics = pack.UncoveredTopics,
            TopicCoverageRatio = pack.TopicCoverageRatio,
            DominantChapterKey = pack.DominantChapterKey,
            SourcePageFrom = pack.SourcePageFrom,
            SourcePageTo = pack.SourcePageTo,
            LastGenerationRunId = pack.LastGenerationRunId,
            LastGeneratedAt = pack.LastGeneratedAt,
            LastUsedAt = pack.LastUsedAt,
            CreatedAt = pack.CreatedAt,
            UpdatedAt = pack.UpdatedAt
        }).ToList();
    }

    public async Task<ContextPackDto> MarkStaleAsync(string userId, string packId, CancellationToken cancellationToken = default)
    {
        var pack = await _contextPackRepository.GetByIdAsync(packId)
            ?? throw new KeyNotFoundException("Context pack not found");

        if (!string.Equals(pack.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You do not have permission to update this context pack.");
        }

        pack.PackStatus = "stale";
        pack.UpdatedAt = DateTime.UtcNow;
        await _contextPackRepository.UpdateAsync(pack);
        return MapContextPackDto(pack);
    }

    private async Task<AIContextPack?> TryReuseCachedPackAsync(RetrievalPlanRequestDto request, CancellationToken cancellationToken)
    {
        var candidates = await _contextPackRepository.ListAsync(
            request.UserId,
            request.Subject,
            request.QuestionType,
            request.Difficulty,
            request.TargetTopics.FirstOrDefault(),
            "active",
            20);

        var match = candidates.FirstOrDefault(pack =>
            string.Equals(pack.CourseId, request.CourseId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pack.DocumentId, request.DocumentId, StringComparison.OrdinalIgnoreCase) &&
            HaveSameRetrievalQuery(pack.RetrievalQuery, request.RetrievalQuery) &&
            HaveSameRequestedChapterKeys(pack.RequestedChapterKeys, request.ChapterKey, request.ChapterKeys) &&
            pack.TargetTopics.Count == request.TargetTopics.Count &&
            pack.TargetTopics.All(topic => request.TargetTopics.Contains(topic, StringComparer.OrdinalIgnoreCase)));

        if (match is null)
        {
            return null;
        }

        if (await ShouldInvalidateCachedPackAsync(match, request, cancellationToken))
        {
            match.PackStatus = "stale";
            match.UpdatedAt = DateTime.UtcNow;
            await _contextPackRepository.UpdateAsync(match);
            return null;
        }

        match.UsageCount += 1;
        match.LastUsedAt = DateTime.UtcNow;
        match.UpdatedAt = DateTime.UtcNow;
        await _contextPackRepository.UpdateAsync(match);
        return match;
    }

    private async Task<bool> ShouldInvalidateCachedPackAsync(AIContextPack pack, RetrievalPlanRequestDto request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (pack.ExpiresAt.HasValue && pack.ExpiresAt.Value <= now)
        {
            return true;
        }

        if (!string.Equals(pack.PackStatus, "active", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(pack.DocumentId))
        {
            return false;
        }

        var document = await _documentRepository.GetByIdAsync(pack.DocumentId);
        if (document is null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentId) &&
            !string.Equals(document.Id, request.DocumentId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var packFreshness = pack.LastRetrievedAt ?? pack.UpdatedAt;
        var documentFreshness = MaxDate(document.LastEmbeddedAt, document.LastExtractedAt, document.CreatedAt);
        if (documentFreshness > packFreshness)
        {
            return true;
        }

        var requestedChapterKeys = ResolveRequestedChapterKeys(request.ChapterKey, request.ChapterKeys);
        if (requestedChapterKeys.Count == 1 &&
            string.Equals(request.SourceScope, "BYOS", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.QuestionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            var requestedChapter = requestedChapterKeys[0];
            if (!string.Equals(pack.DominantChapterKey, requestedChapter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (pack.RetrievedChunks.Any(chunk =>
                    !string.IsNullOrWhiteSpace(chunk.ChapterKey) &&
                    !string.Equals(chunk.ChapterKey, requestedChapter, StringComparison.OrdinalIgnoreCase) &&
                    chunk.TopicScore >= 1m))
            {
                return true;
            }
        }

        return false;
    }

    private static RetrievalPlanResultDto BuildCachedResult(RetrievalPlanRequestDto request, AIContextPack cachedPack, string planId)
    {
        return new RetrievalPlanResultDto
        {
            PlanId = planId,
            PackId = cachedPack.Id,
            UsedCachedPack = true,
            Subject = request.Subject,
            QuestionType = request.QuestionType,
            Difficulty = request.Difficulty,
            GenerationMode = request.GenerationMode,
            TargetTopics = request.TargetTopics,
            RetrievalQuery = request.RetrievalQuery,
            TokenBudget = new RetrievalTokenBudgetDto
            {
                MaxPackedTokens = request.MaxPackedTokens,
                EstimatedPackedTokens = cachedPack.TokenCount,
                EstimatedSourceTokens = cachedPack.SourceTokenCount,
                CompressionRatio = cachedPack.CompressionRatio
            },
            CandidateStats = new RetrievalCandidateStatsDto
            {
                PreFilterCount = cachedPack.SourceChunkIds.Count,
                TopicMatchCount = cachedPack.SourceChunkIds.Count,
                RankedCount = cachedPack.RetrievedChunks.Count,
                SelectedChunkCount = cachedPack.Chunks.Count,
                CoveredTopicCount = cachedPack.CoveredTopics.Count,
                RequestedTopicCount = cachedPack.TargetTopics.Count,
                TopicCoverageRatio = cachedPack.TopicCoverageRatio
            },
            SelectedChunks = cachedPack.RetrievedChunks.Select(MapRetrievedChunkDto).ToList(),
            ContextPack = MapContextPackDto(cachedPack)
        };
    }

    private static RetrievedChunkSnapshot BuildRetrievedChunk(KnowledgeChunk chunk, string questionType, IReadOnlyList<string> targetTopics, string? retrievalQuery, bool includeChunkText, string? requestedChapterKey, IReadOnlyList<string>? requestedChapterKeys)
    {
        var rawContent = chunk.MarkdownText ?? chunk.NormalizedText ?? chunk.RawText;
        var content = AIContentSanitizer.SanitizeChunkContent(rawContent);
        var topicScore = ComputeTopicScore(chunk, targetTopics);
        var lexicalScore = ComputeLexicalScore(chunk, retrievalQuery, targetTopics);
        var sectionScore = ComputeSectionScore(chunk, targetTopics);
        var structureScore = ComputeStructureScore(chunk, content);
        var contentQualityScore = ComputeContentQualityScore(chunk, content);
        var tokenCount = chunk.TokenCount > 0 ? chunk.TokenCount : EstimateTokenCount(content);
        var valueDensityScore = ComputeValueDensityScore(topicScore, lexicalScore, sectionScore, structureScore, contentQualityScore, tokenCount);
        var questionTypeFitScore = ComputeQuestionTypeFitScore(chunk, content, questionType);
        var pedagogicalPenalty = ComputePedagogicalPenalty(chunk, content, questionType, targetTopics);
        var chapterKey = !string.IsNullOrWhiteSpace(chunk.ChapterKey)
            ? chunk.ChapterKey
            : BuildChapterKey(chunk.SectionTitle);
        var locationScore = ComputeLocationScore(chunk, chapterKey);
        var chapterSupportScore = ComputeRequestedChapterSupportScore(chunk, requestedChapterKey, chapterKey, requestedChapterKeys);
        var contextualAdjustment = ComputeRetrievalContextAdjustment(chunk, content, questionType, targetTopics, chapterKey, requestedChapterKey, requestedChapterKeys);
        var selectionReasons = BuildBaseSelectionReasons(chunk, questionType, targetTopics, retrievalQuery, topicScore, lexicalScore, sectionScore, structureScore, contentQualityScore, valueDensityScore, questionTypeFitScore, pedagogicalPenalty, chapterKey, requestedChapterKey, requestedChapterKeys);
        var weightedScore =
            topicScore * 0.38m +
            lexicalScore * 0.16m +
            sectionScore * 0.08m +
            structureScore * 0.10m +
            contentQualityScore * 0.09m +
            valueDensityScore * 0.05m +
            questionTypeFitScore * 0.11m +
            locationScore * 0.02m +
            chapterSupportScore * 0.01m -
            pedagogicalPenalty * 0.16m +
            contextualAdjustment;
        var finalScore = Math.Round(Math.Clamp(weightedScore, 0m, 1m), 4);

        return new RetrievedChunkSnapshot
        {
            ChunkId = chunk.Id,
            DocumentId = chunk.DocumentId,
            ChunkIndex = chunk.ChunkIndex,
            SectionTitle = chunk.SectionTitle,
            ChapterKey = chapterKey,
            SourcePageFrom = chunk.SourcePageFrom,
            SourcePageTo = chunk.SourcePageTo,
            TopicTags = chunk.TopicTags,
            SelectionReasons = selectionReasons,
            TokenCount = tokenCount,
            TopicScore = topicScore,
            LexicalScore = lexicalScore,
            SectionScore = sectionScore,
            FinalScore = finalScore,
            SelectionRole = ResolveSelectionRole(topicScore, lexicalScore, sectionScore, finalScore),
            ContentText = includeChunkText ? content : null
        };
    }

    private static List<RetrievedChunkSnapshot> PackChunks(
        IReadOnlyList<RetrievedChunkSnapshot> ranked,
        string questionType,
        int maxPackedTokens,
        IReadOnlyList<string> targetTopics,
        string? retrievalQuery,
        IReadOnlyList<string> requestedChapterKeys)
    {
        var budget = Math.Max(MinimumPackedTokenBudget, maxPackedTokens);
        var selected = new List<RetrievedChunkSnapshot>();
        var consumed = 0;
        var sectionUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        RetrievedChunkSnapshot? anchor = null;

        var primaryCandidates = ranked
            .Where(item => string.Equals(item.SelectionRole, "primary", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var supportCandidates = ranked
            .Where(item => string.Equals(item.SelectionRole, "support", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var optionalCandidates = ranked
            .Where(item => string.Equals(item.SelectionRole, "optional", StringComparison.OrdinalIgnoreCase))
            .ToList();

        EnsureRequestedChapterCoverage(ranked, selected, requestedChapterKeys, targetTopics, ref consumed, budget, sectionUsage);
        EnsureTopicCoverage(ranked, selected, targetTopics, requestedChapterKeys, ref consumed, budget, sectionUsage);

        AddCandidates(primaryCandidates, "primary", MaximumPrimaryChunks);
        AddCandidates(supportCandidates, "support", int.MaxValue);
        AddCandidates(optionalCandidates, "optional", MaximumOptionalChunks);

        EnsureRequestedChapterCoverage(ranked, selected, requestedChapterKeys, targetTopics, ref consumed, budget, sectionUsage);
        EnsureTopicCoverage(ranked, selected, targetTopics, requestedChapterKeys, ref consumed, budget, sectionUsage);

        if (selected.Count == 0 && ranked.Count > 0)
        {
            selected.Add(ranked[0]);
            anchor ??= ranked[0];
        }

        EnsureAtLeastOneExactTopicHit(ranked, selected, targetTopics, ref consumed, budget, sectionUsage);
        return selected
            .OrderBy(item => GetRoleRank(item.SelectionRole))
            .ThenByDescending(item => item.FinalScore)
            .ThenBy(item => item.TokenCount)
            .ToList();

        void AddCandidates(IReadOnlyList<RetrievedChunkSnapshot> candidates, string passRole, int maxRoleCount)
        {
            foreach (var item in candidates)
            {
                if (CountRole(selected, passRole) >= maxRoleCount)
                {
                    break;
                }

                if (string.Equals(passRole, "optional", StringComparison.OrdinalIgnoreCase))
                {
                    if (selected.Count == 0 || !IsOptionalContextuallyUseful(item, selected, anchor))
                    {
                        continue;
                    }
                }

                if (ShouldSkipPeLooseSupportChunk(item, passRole, questionType, targetTopics, selected))
                {
                    continue;
                }

                var tokenCost = EstimatePackedTokenCost(item, targetTopics, retrievalQuery);
                if (selected.Count > 0 && consumed + tokenCost > budget)
                {
                    continue;
                }

                if (IsRedundant(item, selected))
                {
                    continue;
                }

                if (!IsContextuallyCompatible(item, selected, anchor))
                {
                    continue;
                }

                AddContextualSelectionReasons(item, selected, anchor);

                var sectionKey = BuildSectionKey(item);
                var maxPerSection = GetMaxChunksPerSection(item);
                if (sectionUsage.TryGetValue(sectionKey, out var count) && count >= maxPerSection)
                {
                    continue;
                }

                selected.Add(item);
                AddSelectionReason(item, $"selected_in_{passRole}_pass");
                anchor ??= item;
                if (anchor == item)
                {
                    AddSelectionReason(item, "anchor_context");
                }
                consumed += tokenCost;
                sectionUsage[sectionKey] = count + 1;

                if (consumed >= budget)
                {
                    break;
                }
            }
        }
    }

    private static AIContextPack BuildContextPack(
        RetrievalPlanRequestDto request,
        IReadOnlyList<RetrievedChunkSnapshot> selected,
        int sourceTokenCount,
        string planId,
        TopicCoverageSnapshot coverage)
    {
        var now = DateTime.UtcNow;
        var chunks = selected.Select(item =>
        {
            var packedText = BuildPackedText(item, request.TargetTopics, request.RetrievalQuery);
            return new PackedChunkSnapshot
            {
                ChunkId = item.ChunkId,
                Title = item.SectionTitle ?? $"Chunk {item.ChunkIndex + 1}",
                PackedText = packedText,
                TokenCount = EstimateTokenCount(packedText),
                SelectionRole = item.SelectionRole
            };
        }).ToList();

        var summaryText = BuildSummaryText(selected);
        var contextText = string.Join($"{Environment.NewLine}{Environment.NewLine}---{Environment.NewLine}{Environment.NewLine}", chunks.Select(chunk => chunk.PackedText));
        var packedTokenCount = chunks.Sum(chunk => chunk.TokenCount);

        var selectedPageFrom = selected.Where(item => item.SourcePageFrom.HasValue).Select(item => item.SourcePageFrom!.Value).ToList();
        var selectedPageTo = selected.Where(item => item.SourcePageTo.HasValue).Select(item => item.SourcePageTo!.Value).ToList();

        return new AIContextPack
        {
            UserId = request.UserId,
            CourseId = request.CourseId,
            DocumentId = request.DocumentId,
            Subject = request.Subject,
            QuestionType = request.QuestionType,
            TargetDifficulty = request.Difficulty,
            TargetTopics = request.TargetTopics,
            SourceScope = request.SourceScope,
            RetrievalQuery = request.RetrievalQuery,
            SourceChunkIds = selected.Select(item => item.ChunkId).ToList(),
            RetrievedChunks = selected.ToList(),
            Chunks = chunks,
            SummaryText = summaryText,
            ContextText = contextText,
            RequestedChapterKeys = ResolveRequestedChapterKeys(request.ChapterKey, request.ChapterKeys),
            CoveredTopics = coverage.CoveredTopics,
            UncoveredTopics = coverage.UncoveredTopics,
            TopicCoverageRatio = coverage.TopicCoverageRatio,
            DistinctSectionCount = selected.Select(BuildSectionKey).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            DominantChapterKey = ResolveDominantChapterKey(selected),
            SourcePageFrom = selectedPageFrom.Count == 0 ? null : selectedPageFrom.Min(),
            SourcePageTo = selectedPageTo.Count == 0 ? null : selectedPageTo.Max(),
            TokenCount = packedTokenCount,
            SourceTokenCount = sourceTokenCount,
            CompressionRatio = sourceTokenCount == 0 ? 1m : Math.Round((decimal)packedTokenCount / sourceTokenCount, 4),
            RecommendedQuestionCount = Math.Max(1, request.RequestedQuestionCount),
            MaxQuestionCount = Math.Max(request.RequestedQuestionCount + 2, 3),
            UsageCount = 0,
            LastRetrievalPlanId = planId,
            LastRetrievedAt = now,
            ExpiresAt = now.AddDays(30),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string BuildPackedText(RetrievedChunkSnapshot chunk, IReadOnlyList<string> targetTopics, string? retrievalQuery)
    {
        var content = AIContentSanitizer.SanitizeChunkContent(chunk.ContentText);
        if (chunk.TokenCount <= ShortChunkPassthroughTokenThreshold)
        {
            return $"[{chunk.SelectionRole}] {chunk.SectionTitle ?? $"Chunk {chunk.ChunkIndex + 1}"}{Environment.NewLine}{content}";
        }

        var preview = BuildDenseChunkPreview(content, targetTopics, retrievalQuery);
        return $"[{chunk.SelectionRole}] {chunk.SectionTitle ?? $"Chunk {chunk.ChunkIndex + 1}"}{Environment.NewLine}{preview}";
    }

    private static string BuildSummaryText(IReadOnlyList<RetrievedChunkSnapshot> chunks)
    {
        var lines = chunks.Select(chunk =>
            $"- {chunk.SectionTitle ?? $"Chunk {chunk.ChunkIndex + 1}"} | topics: {string.Join(", ", chunk.TopicTags.Take(4))} | chapter: {chunk.ChapterKey ?? "n/a"} | pages: {FormatPageRange(chunk.SourcePageFrom, chunk.SourcePageTo)} | score: {chunk.FinalScore:0.000}");
        return string.Join(Environment.NewLine, lines);
    }

    private static decimal ComputeTopicScore(KnowledgeChunk chunk, IReadOnlyList<string> targetTopics)
    {
        if (targetTopics.Count == 0)
        {
            return 0.5m;
        }

        var chunkTags = chunk.TopicTags ?? new List<string>();
        var exactOverlap = OverlapCount(chunkTags, targetTopics);
        if (exactOverlap == targetTopics.Count)
        {
            return 1m;
        }

        var partialHits = 0;
        foreach (var topic in targetTopics)
        {
            if (chunkTags.Any(tag => IsPartialTopicMatch(tag, topic)))
            {
                partialHits += 1;
            }
        }

        var score = ((decimal)exactOverlap + (decimal)Math.Max(0, partialHits - exactOverlap) * 0.5m) / Math.Max(1, targetTopics.Count);
        return Math.Round(Math.Min(1m, score), 4);
    }

    private static decimal ComputeLexicalScore(KnowledgeChunk chunk, string? retrievalQuery, IReadOnlyList<string> targetTopics)
    {
        var body = chunk.MarkdownText ?? chunk.NormalizedText ?? chunk.RawText;
        var section = chunk.SectionTitle?.ToLowerInvariant() ?? string.Empty;
        var content = body.ToLowerInvariant();
        var terms = ExtractTerms(retrievalQuery)
            .Concat(targetTopics.SelectMany(ExtractTerms))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (terms.Count == 0)
        {
            return 0.25m;
        }

        decimal weightedHits = 0m;
        foreach (var term in terms)
        {
            if (section.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                weightedHits += 1.25m;
                continue;
            }

            if (content.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                weightedHits += 1m;
            }
        }

        return Math.Round(Math.Min(1m, weightedHits / terms.Count), 4);
    }

    private static decimal ComputeSectionScore(KnowledgeChunk chunk, IReadOnlyList<string> targetTopics)
    {
        if (string.IsNullOrWhiteSpace(chunk.SectionTitle) || targetTopics.Count == 0)
        {
            return 0m;
        }

        var section = chunk.SectionTitle.ToLowerInvariant();
        var hits = targetTopics.Count(topic => section.Contains(topic, StringComparison.OrdinalIgnoreCase));
        return Math.Round((decimal)hits / targetTopics.Count, 4);
    }

    private static decimal ComputeStructureScore(KnowledgeChunk chunk, string content)
    {
        decimal score = 0.15m;

        if (!string.IsNullOrWhiteSpace(chunk.SectionTitle))
        {
            score += 0.25m;
        }

        if (Regex.IsMatch(content, @"(?im)^(example|algorithm|syntax|note|rule|output|input)\b"))
        {
            score += 0.2m;
        }

        if (content.Contains("```", StringComparison.Ordinal) ||
            content.Contains("printf", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("System.out", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("public class", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.2m;
        }

        var tokenCount = chunk.TokenCount > 0 ? chunk.TokenCount : EstimateTokenCount(content);
        if (tokenCount >= 60 && tokenCount <= 260)
        {
            score += 0.2m;
        }

        return Math.Round(Math.Min(1m, score), 4);
    }

    private static decimal ComputeContentQualityScore(KnowledgeChunk chunk, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0m;
        }

        var normalized = Regex.Replace(content, @"\s+", " ").Trim();
        var lengthScore = normalized.Length switch
        {
            < 80 => 0.2m,
            <= 300 => 0.9m,
            <= 900 => 1m,
            <= 1600 => 0.8m,
            _ => 0.6m
        };

        var noisyCharacters = normalized.Count(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch) && ch is not '_' and not '-' and not '.' and not ',' and not ':' and not ';' and not '(' and not ')' and not '[' and not ']');
        var noiseRatio = normalized.Length == 0 ? 0m : (decimal)noisyCharacters / normalized.Length;
        var noisePenalty = noiseRatio > 0.18m ? 0.25m : noiseRatio > 0.10m ? 0.10m : 0m;

        var repeatedWordsPenalty = HasHighRepetition(normalized) ? 0.15m : 0m;
        var final = Math.Max(0.1m, lengthScore - noisePenalty - repeatedWordsPenalty);
        return Math.Round(Math.Min(1m, final), 4);
    }

    private static decimal ComputeValueDensityScore(decimal topicScore, decimal lexicalScore, decimal sectionScore, decimal structureScore, decimal contentQualityScore, int tokenCount)
    {
        var rawValue = topicScore * 0.45m + lexicalScore * 0.2m + sectionScore * 0.1m + structureScore * 0.1m + contentQualityScore * 0.15m;
        var efficiency = tokenCount switch
        {
            <= 80 => 1m,
            <= 160 => 0.95m,
            <= 280 => 0.85m,
            <= 420 => 0.70m,
            _ => 0.55m
        };
        return Math.Round(Math.Min(1m, rawValue * efficiency), 4);
    }

    private static decimal ComputeLocationScore(KnowledgeChunk chunk, string? chapterKey)
    {
        decimal score = 0.2m;

        if (!string.IsNullOrWhiteSpace(chapterKey))
        {
            score += 0.35m;
        }

        if (chunk.SourcePageFrom.HasValue || chunk.SourcePageTo.HasValue)
        {
            score += 0.25m;
        }

        if (!string.IsNullOrWhiteSpace(chunk.SectionTitle) && LooksStructuredSection(chunk.SectionTitle))
        {
            score += 0.2m;
        }

        return Math.Round(Math.Min(1m, score), 4);
    }

    private static decimal ComputeRequestedChapterSupportScore(KnowledgeChunk chunk, string? requestedChapterKey, string? resolvedChapterKey, IReadOnlyList<string>? requestedChapterKeys)
    {
        var keys = ResolveRequestedChapterKeys(requestedChapterKey, requestedChapterKeys);
        if (keys.Count == 0)
        {
            return 0.5m;
        }

        if (!string.IsNullOrWhiteSpace(resolvedChapterKey) &&
            keys.Contains(resolvedChapterKey, StringComparer.OrdinalIgnoreCase))
        {
            return 1m;
        }

        if (chunk.ChapterOrder.HasValue && chunk.SourcePageFrom.HasValue)
        {
            return 0.75m;
        }

        return 0.55m;
    }

    private static bool IsChunkUsable(KnowledgeChunk chunk, IReadOnlyList<string> targetTopics, string questionType)
    {
        var content = chunk.MarkdownText ?? chunk.NormalizedText ?? chunk.RawText;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        if (LooksLikeReferenceOnlyChunk(chunk.SectionTitle, content, chunk.TopicTags))
        {
            return false;
        }

        if (ShouldExcludeOffTopicAbstractClassSubtopic(chunk, targetTopics))
        {
            return false;
        }

        if (ShouldExcludeLowValueChunkForQuestionType(chunk, targetTopics, questionType))
        {
            return false;
        }

        if (content.Trim().Length >= MinimumChunkCharacters)
        {
            return true;
        }

        if (ComputeTopicScore(chunk, targetTopics) >= 0.75m)
        {
            return true;
        }

        return targetTopics.Count > 0 && OverlapCount(chunk.TopicTags ?? new List<string>(), targetTopics) > 0;
    }

    private static decimal ComputeQuestionTypeFitScore(KnowledgeChunk chunk, string content, string questionType)
    {
        var normalizedType = questionType?.Trim().ToUpperInvariant() ?? "BOTH";
        var affinity = chunk.QuestionTypeAffinity ?? new List<string>();
        decimal score = 0.35m;

        if (normalizedType == "PE")
        {
            if (affinity.Any(item => string.Equals(item, "PE", StringComparison.OrdinalIgnoreCase)))
            {
                score += 0.35m;
            }
            else if (affinity.Any(item => string.Equals(item, "BOTH", StringComparison.OrdinalIgnoreCase)))
            {
                score += 0.12m;
            }

            if (LooksLikePracticalCodeChunk(chunk, content))
            {
                score += 0.25m;
            }

            if (LooksLikeExampleSection(chunk.SectionTitle))
            {
                score += 0.10m;
            }
        }
        else if (normalizedType == "FE")
        {
            if (affinity.Any(item => string.Equals(item, "FE", StringComparison.OrdinalIgnoreCase)))
            {
                score += 0.35m;
            }
            else if (affinity.Any(item => string.Equals(item, "BOTH", StringComparison.OrdinalIgnoreCase)))
            {
                score += 0.12m;
            }

            if (LooksLikeConceptualTheoryChunk(chunk, content))
            {
                score += 0.18m;
            }
        }

        return Math.Round(Math.Clamp(score, 0m, 1m), 4);
    }

    private static decimal ComputePedagogicalPenalty(KnowledgeChunk chunk, string content, string questionType, IReadOnlyList<string> targetTopics)
    {
        if (!string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        decimal penalty = 0m;
        if (LooksLikeLowValueTheorySectionForPe(chunk, content))
        {
            penalty += 0.30m;
        }

        if (LooksLikeOverloadingChunkWithoutTarget(chunk, targetTopics))
        {
            penalty += 0.28m;
        }

        if (LooksLikeInterfaceChunkWithoutTarget(chunk, targetTopics))
        {
            penalty += 0.24m;
        }

        if (LooksLikeAnonymousOrNestedChunkWithoutTarget(chunk, targetTopics))
        {
            penalty += 0.30m;
        }

        if (LooksLikeHashingApplicationsOverviewChunkWithoutTarget(chunk.SectionTitle, chunk.ChapterTitle, content, chunk.TopicTags, targetTopics))
        {
            penalty += 0.18m;
        }

        if (!LooksLikePracticalCodeChunk(chunk, content) && chunk.TokenCount <= 120 && ComputeTopicScore(chunk, targetTopics) <= 0.5m)
        {
            penalty += 0.10m;
        }

        return Math.Round(Math.Clamp(penalty, 0m, 1m), 4);
    }

    private static bool ShouldExcludeLowValueChunkForQuestionType(KnowledgeChunk chunk, IReadOnlyList<string> targetTopics, string questionType)
    {
        var content = chunk.MarkdownText ?? chunk.NormalizedText ?? chunk.RawText ?? string.Empty;
        if (!string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (LooksLikeOverloadingChunkWithoutTarget(chunk, targetTopics) ||
            LooksLikeInterfaceChunkWithoutTarget(chunk, targetTopics) ||
            LooksLikeAnonymousOrNestedChunkWithoutTarget(chunk, targetTopics) ||
            LooksLikeOffTargetQueueVariantChunk(chunk.SectionTitle, chunk.ChapterTitle, content, chunk.TopicTags, targetTopics))
        {
            return true;
        }

        if (IsQueueFocusedRequest(targetTopics) &&
            LooksLikeCoreQueueImplementationChunk(chunk.SectionTitle, chunk.ChapterTitle, content, chunk.TopicTags))
        {
            return false;
        }

        if (!LooksLikeLowValueTheorySectionForPe(chunk, content))
        {
            return false;
        }

        if (HasStrongTopicAnchorFallback(chunk.SectionTitle, content, chunk.TopicTags, targetTopics))
        {
            return false;
        }

        var topicScore = ComputeTopicScore(chunk, targetTopics);
        if (topicScore >= 0.75m && LooksLikePracticalCodeChunk(chunk, content))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldExcludeOffTopicAbstractClassSubtopic(KnowledgeChunk chunk, IReadOnlyList<string> targetTopics)
    {
        if (!IsAbstractClassFocusedRequest(targetTopics))
        {
            return false;
        }

        if (AllowsAbstractClassSubtopic(targetTopics, "anonymous") ||
            AllowsAbstractClassSubtopic(targetTopics, "nested") ||
            AllowsAbstractClassSubtopic(targetTopics, "local") ||
            AllowsAbstractClassSubtopic(targetTopics, "inner"))
        {
            return false;
        }

        var title = $"{chunk.SectionTitle} {chunk.ChapterTitle}".Trim();
        var tagText = string.Join(" ", chunk.TopicTags ?? new List<string>());
        var content = chunk.MarkdownText ?? chunk.NormalizedText ?? chunk.RawText ?? string.Empty;
        var combined = $"{title} {tagText} {content}";

        var mentionsAnonymousOrNested =
            Regex.IsMatch(combined, @"(?i)\b(anonymous class|anonymous classes|nested class|nested classes|local class|local classes|inner class|inner classes)\b");
        if (!mentionsAnonymousOrNested)
        {
            return false;
        }

        var stillPrimarilyAbstractCore =
            Regex.IsMatch(title, @"(?i)\babstract classes?\b|\bimplementing abstract methods\b") ||
            Regex.IsMatch(tagText, @"(?i)\bimplementing abstract methods\b");

        return !stillPrimarilyAbstractCore;
    }

    private static bool ShouldExcludeOffTopicAbstractClassSubtopic(RetrievedChunkSnapshot chunk, IReadOnlyList<string> targetTopics)
    {
        if (!IsAbstractClassFocusedRequest(targetTopics))
        {
            return false;
        }

        if (AllowsAbstractClassSubtopic(targetTopics, "anonymous") ||
            AllowsAbstractClassSubtopic(targetTopics, "nested") ||
            AllowsAbstractClassSubtopic(targetTopics, "local") ||
            AllowsAbstractClassSubtopic(targetTopics, "inner"))
        {
            return false;
        }

        var title = $"{chunk.SectionTitle} {chunk.ChapterKey}".Trim();
        var tagText = string.Join(" ", chunk.TopicTags ?? new List<string>());
        var content = chunk.ContentText ?? string.Empty;
        var combined = $"{title} {tagText} {content}";

        var mentionsAnonymousOrNested =
            Regex.IsMatch(combined, @"(?i)\b(anonymous class|anonymous classes|nested class|nested classes|local class|local classes|inner class|inner classes)\b");
        if (!mentionsAnonymousOrNested)
        {
            return false;
        }

        var titleSignalsAbstractCore =
            Regex.IsMatch(chunk.SectionTitle ?? string.Empty, @"(?i)^\s*abstract classes?\s*$|^\s*implementing abstract methods\s*$");

        return !titleSignalsAbstractCore;
    }

    private static bool ShouldExcludeLowValueChunkForQuestionType(RetrievedChunkSnapshot chunk, IReadOnlyList<string> targetTopics, string questionType)
    {
        if (LooksLikeReferenceOnlyChunk(chunk.SectionTitle, chunk.ContentText ?? string.Empty, chunk.TopicTags))
        {
            return true;
        }

        if (!string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (LooksLikeOverloadingChunkWithoutTarget(chunk, targetTopics) ||
            LooksLikeInterfaceChunkWithoutTarget(chunk, targetTopics) ||
            LooksLikeAnonymousOrNestedChunkWithoutTarget(chunk, targetTopics) ||
            LooksLikeOffTargetQueueVariantChunk(chunk.SectionTitle, chunk.ChapterKey, chunk.ContentText ?? string.Empty, chunk.TopicTags, targetTopics))
        {
            return true;
        }

        if (IsQueueFocusedRequest(targetTopics) &&
            LooksLikeCoreQueueImplementationChunk(chunk.SectionTitle, chunk.ChapterKey, chunk.ContentText ?? string.Empty, chunk.TopicTags))
        {
            return false;
        }

        if (!LooksLikeLowValueTheorySectionForPe(chunk.SectionTitle, chunk.ContentText ?? string.Empty, chunk.TopicTags))
        {
            return false;
        }

        if (HasStrongTopicAnchorFallback(chunk.SectionTitle, chunk.ContentText ?? string.Empty, chunk.TopicTags, targetTopics))
        {
            return false;
        }

        if (chunk.TopicScore >= 0.75m &&
            LooksLikePracticalCodeChunk(chunk.SectionTitle, chunk.ContentText ?? string.Empty, chunk.TopicTags))
        {
            return false;
        }

        return true;
    }

    private static bool IsAbstractClassFocusedRequest(IReadOnlyList<string> targetTopics)
    {
        if (targetTopics.Count == 0)
        {
            return false;
        }

        return targetTopics.Any(topic =>
            !string.IsNullOrWhiteSpace(topic) &&
            Regex.IsMatch(topic, @"(?i)\babstract classes?\b|\babstraction\b"));
    }

    private static bool AllowsAbstractClassSubtopic(IReadOnlyList<string> targetTopics, string marker)
    {
        return targetTopics.Any(topic =>
            !string.IsNullOrWhiteSpace(topic) &&
            topic.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRedundant(RetrievedChunkSnapshot candidate, IReadOnlyList<RetrievedChunkSnapshot> selected)
    {
        var candidateSectionKey = BuildSectionKey(candidate);
        var candidateTerms = ExtractTerms(candidate.ContentText)
            .Where(term => term.Length >= 4)
            .Take(20)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var existing in selected)
        {
            if (string.Equals(existing.ChunkId, candidate.ChunkId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(BuildSectionKey(existing), candidateSectionKey, StringComparison.OrdinalIgnoreCase) &&
                OverlapCount(existing.TopicTags, candidate.TopicTags) > 0)
            {
                if (candidate.TokenCount > ShortChunkSectionExpansionTokenThreshold ||
                    existing.TokenCount > ShortChunkSectionExpansionTokenThreshold)
                {
                    return true;
                }
            }

            var existingTerms = ExtractTerms(existing.ContentText)
                .Where(term => term.Length >= 4)
                .Take(20)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (candidateTerms.Count > 0 && existingTerms.Count > 0)
            {
                var allowComplementaryCoreQueuePair =
                    SameDocument(existing, candidate) &&
                    IsCoreQueueFamilyChunk(existing) &&
                    IsCoreQueueFamilyChunk(candidate) &&
                    !string.Equals(BuildSectionKey(existing), candidateSectionKey, StringComparison.OrdinalIgnoreCase);
                if (allowComplementaryCoreQueuePair)
                {
                    continue;
                }

                var overlap = candidateTerms.Count(term => existingTerms.Contains(term));
                var ratio = (decimal)overlap / Math.Max(1, Math.Min(candidateTerms.Count, existingTerms.Count));
                var redundancyThreshold = candidate.TokenCount <= ShortChunkSectionExpansionTokenThreshold &&
                                          existing.TokenCount <= ShortChunkSectionExpansionTokenThreshold
                    ? 0.85m
                    : 0.7m;
                if (ratio >= redundancyThreshold)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string BuildSectionKey(RetrievedChunkSnapshot chunk)
    {
        var title = string.IsNullOrWhiteSpace(chunk.SectionTitle)
            ? $"chunk-{chunk.ChunkIndex / 2}"
            : Regex.Replace(chunk.SectionTitle.Trim().ToLowerInvariant(), @"\s+", " ");
        return title;
    }

    private static string? BuildChapterKey(string? sectionTitle)
    {
        if (string.IsNullOrWhiteSpace(sectionTitle))
        {
            return null;
        }

        var normalized = Regex.Replace(sectionTitle.Trim().ToLowerInvariant(), @"\s+", " ");
        var numbered = Regex.Match(normalized, @"^(chapter|chap|chuong|section|sec|part|module|unit)\s*[\.:_-]*\s*[a-z0-9ivx]+");
        if (numbered.Success)
        {
            return numbered.Value;
        }

        var leadingNumber = Regex.Match(normalized, @"^\d+(\.\d+){0,2}");
        if (leadingNumber.Success)
        {
            return leadingNumber.Value;
        }

        var keywords = Regex.Match(normalized, @"^(introduction|overview|fundamentals|basics|arrays|loops|variables|functions|pointers|classes|inheritance|polymorphism|sorting|searching)\b");
        return keywords.Success ? keywords.Value : normalized.Split(':')[0].Trim();
    }

    private static int CountPrimary(IReadOnlyList<RetrievedChunkSnapshot> chunks)
        => chunks.Count(item => string.Equals(item.SelectionRole, "primary", StringComparison.OrdinalIgnoreCase));

    private static int CountRole(IReadOnlyList<RetrievedChunkSnapshot> chunks, string role)
        => chunks.Count(item => string.Equals(item.SelectionRole, role, StringComparison.OrdinalIgnoreCase));

    private static int GetRoleRank(string? role)
        => role?.Trim().ToLowerInvariant() switch
        {
            "primary" => 0,
            "support" => 1,
            "optional" => 2,
            _ => 3
        };

    private static string ResolveSelectionRole(decimal topicScore, decimal lexicalScore, decimal sectionScore, decimal finalScore)
    {
        if (topicScore >= 0.75m || (topicScore >= 0.5m && lexicalScore >= 0.45m))
        {
            return "primary";
        }

        if (topicScore >= 0.35m ||
            lexicalScore >= 0.35m ||
            (sectionScore >= 0.45m && finalScore >= 0.45m))
        {
            return "support";
        }

        return "optional";
    }

    private static void EnsureAtLeastOneExactTopicHit(
        IReadOnlyList<RetrievedChunkSnapshot> ranked,
        List<RetrievedChunkSnapshot> selected,
        IReadOnlyList<string> targetTopics,
        ref int consumed,
        int budget,
        IDictionary<string, int> sectionUsage)
    {
        if (targetTopics.Count == 0 || selected.Any(item => item.TopicScore >= 1m))
        {
            return;
        }

        var bestTopicHit = ranked
            .Where(item => item.TopicScore >= 1m || item.TopicScore >= 0.75m)
            .OrderByDescending(item => item.TopicScore)
            .ThenByDescending(item => item.FinalScore)
            .FirstOrDefault();

        if (bestTopicHit is null || selected.Any(item => string.Equals(item.ChunkId, bestTopicHit.ChunkId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var tokenCost = EstimatePackedTokenCost(bestTopicHit, targetTopics, null);
        if (selected.Count > 0 && consumed + tokenCost > budget)
        {
            return;
        }

        AddContextualSelectionReasons(bestTopicHit, selected, selected.FirstOrDefault());
        bestTopicHit.SelectionRole = "primary";
        AddSelectionReason(bestTopicHit, "forced_exact_topic_hit");
        selected.Add(bestTopicHit);
        consumed += tokenCost;
        var sectionKey = BuildSectionKey(bestTopicHit);
        sectionUsage[sectionKey] = sectionUsage.TryGetValue(sectionKey, out var count) ? count + 1 : 1;
    }

    private static void EnsureTopicCoverage(
        IReadOnlyList<RetrievedChunkSnapshot> ranked,
        List<RetrievedChunkSnapshot> selected,
        IReadOnlyList<string> targetTopics,
        IReadOnlyList<string> requestedChapterKeys,
        ref int consumed,
        int budget,
        IDictionary<string, int> sectionUsage)
    {
        if (targetTopics.Count <= 1)
        {
            return;
        }

        foreach (var topic in targetTopics)
        {
            if (selected.Any(item => ChunkCoversTopic(item, topic)))
            {
                continue;
            }

            var candidate = ranked
                .Where(item => ChunkCoversTopic(item, topic))
                .Where(item => !selected.Any(existing => string.Equals(existing.ChunkId, item.ChunkId, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(item => item.TopicScore)
                .ThenByDescending(item => item.FinalScore)
                .FirstOrDefault();

            if (candidate is null)
            {
                continue;
            }

            var tokenCost = EstimatePackedTokenCost(candidate, targetTopics, null);
            if (selected.Count > 0 && consumed + tokenCost > budget)
            {
                continue;
            }

            if (IsRedundant(candidate, selected))
            {
                continue;
            }

            var forceTopicCoverage = ShouldForceTopicCoverage(candidate, topic, requestedChapterKeys);
            if (!forceTopicCoverage && !IsContextuallyCompatible(candidate, selected, selected.FirstOrDefault()))
            {
                continue;
            }

            AddContextualSelectionReasons(candidate, selected, selected.FirstOrDefault());
            var sectionKey = BuildSectionKey(candidate);
            var maxPerSection = GetMaxChunksPerSection(candidate);
            if (sectionUsage.TryGetValue(sectionKey, out var count) && count >= maxPerSection)
            {
                continue;
            }

            candidate.SelectionRole = "primary";
            AddSelectionReason(candidate, $"topic_coverage_fill:{topic}");
            selected.Add(candidate);
            consumed += tokenCost;
            sectionUsage[sectionKey] = count + 1;
        }
    }

    private static void EnsureRequestedChapterCoverage(
        IReadOnlyList<RetrievedChunkSnapshot> ranked,
        List<RetrievedChunkSnapshot> selected,
        IReadOnlyList<string> requestedChapterKeys,
        IReadOnlyList<string> targetTopics,
        ref int consumed,
        int budget,
        IDictionary<string, int> sectionUsage)
    {
        if (requestedChapterKeys.Count <= 1)
        {
            return;
        }

        foreach (var chapterKey in requestedChapterKeys)
        {
            if (string.IsNullOrWhiteSpace(chapterKey) ||
                selected.Any(item => string.Equals(item.ChapterKey, chapterKey, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var candidate = ranked
                .Where(item => string.Equals(item.ChapterKey, chapterKey, StringComparison.OrdinalIgnoreCase))
                .Where(item => !selected.Any(existing => string.Equals(existing.ChunkId, item.ChunkId, StringComparison.OrdinalIgnoreCase)))
                .Where(item => item.TopicScore >= CoverageFillMinimumTopicScore || HasTopicTitleAnchor(item, targetTopics))
                .OrderByDescending(item => CountRequestedTopicHits(item, targetTopics))
                .ThenByDescending(item => item.TopicScore)
                .ThenByDescending(item => item.FinalScore)
                .ThenBy(item => item.TokenCount)
                .FirstOrDefault();

            if (candidate is null)
            {
                continue;
            }

            var tokenCost = EstimatePackedTokenCost(candidate, targetTopics, null);
            if (selected.Count > 0 && consumed + tokenCost > budget)
            {
                continue;
            }

            if (IsRedundant(candidate, selected))
            {
                continue;
            }

            var sectionKey = BuildSectionKey(candidate);
            var maxPerSection = GetMaxChunksPerSection(candidate);
            if (sectionUsage.TryGetValue(sectionKey, out var count) && count >= maxPerSection)
            {
                continue;
            }

            AddContextualSelectionReasons(candidate, selected, selected.FirstOrDefault());
            candidate.SelectionRole = "primary";
            AddSelectionReason(candidate, $"requested_chapter_coverage_fill:{chapterKey}");
            selected.Add(candidate);
            consumed += tokenCost;
            sectionUsage[sectionKey] = count + 1;
        }
    }

    private static bool ShouldForceTopicCoverage(
        RetrievedChunkSnapshot candidate,
        string topic,
        IReadOnlyList<string> requestedChapterKeys)
    {
        if (!ChunkCoversTopic(candidate, topic))
        {
            return false;
        }

        if (candidate.TopicScore >= 0.5m)
        {
            return true;
        }

        if (candidate.TopicTags.Any(tag => IsExactTopicMatch(tag, topic)))
        {
            return true;
        }

        if (HasTopicTitleAnchor(candidate, new[] { topic }))
        {
            return true;
        }

        return requestedChapterKeys.Count > 1 &&
               requestedChapterKeys.Contains(candidate.ChapterKey ?? string.Empty, StringComparer.OrdinalIgnoreCase) &&
               candidate.TopicScore >= CoverageFillMinimumTopicScore;
    }

    private static int CountRequestedTopicHits(RetrievedChunkSnapshot chunk, IReadOnlyList<string> targetTopics)
        => targetTopics.Count(topic => ChunkCoversTopic(chunk, topic));

    private static List<string> BuildBaseSelectionReasons(
        KnowledgeChunk chunk,
        string questionType,
        IReadOnlyList<string> targetTopics,
        string? retrievalQuery,
        decimal topicScore,
        decimal lexicalScore,
        decimal sectionScore,
        decimal structureScore,
        decimal contentQualityScore,
        decimal valueDensityScore,
        decimal questionTypeFitScore,
        decimal pedagogicalPenalty,
        string? chapterKey,
        string? requestedChapterKey,
        IReadOnlyList<string>? requestedChapterKeys)
    {
        var reasons = new List<string>();
        var keys = ResolveRequestedChapterKeys(requestedChapterKey, requestedChapterKeys);

        foreach (var topic in targetTopics)
        {
            if (chunk.TopicTags.Any(tag => IsExactTopicMatch(tag, topic)))
            {
                reasons.Add($"exact_topic_match:{topic}");
            }
            else if (chunk.TopicTags.Any(tag => IsPartialTopicMatch(tag, topic)))
            {
                reasons.Add($"partial_topic_match:{topic}");
            }
        }

        var retrievalTerms = ExtractTerms(retrievalQuery).ToList();
        if (retrievalTerms.Count > 0 && retrievalTerms.Any(term => (chunk.MarkdownText ?? chunk.NormalizedText ?? chunk.RawText).Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            reasons.Add("retrieval_query_hit");
        }

        if (!string.IsNullOrWhiteSpace(chunk.SectionTitle) &&
            targetTopics.Any(topic => chunk.SectionTitle.Contains(topic, StringComparison.OrdinalIgnoreCase)))
        {
            reasons.Add("section_title_match");
        }

        if (chunk.TokenCount <= ShortChunkPassthroughTokenThreshold)
        {
            reasons.Add("short_chunk_passthrough");
        }
        else
        {
            reasons.Add("dense_chunk_salient_excerpt");
        }

        if (!string.IsNullOrWhiteSpace(chapterKey))
        {
            reasons.Add($"chapter_key:{chapterKey}");
        }

        if (keys.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(chapterKey) && keys.Contains(chapterKey, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add("requested_chapter_exact_hit");
            }
            else
            {
                reasons.Add($"cross_chapter_topic_support_from:{chapterKey}");
            }
        }

        if (chunk.SourcePageFrom.HasValue || chunk.SourcePageTo.HasValue)
        {
            reasons.Add($"page_range:{FormatPageRange(chunk.SourcePageFrom, chunk.SourcePageTo)}");
        }

        if (topicScore >= 0.75m)
        {
            reasons.Add("primary_due_to_topic_strength");
        }
        else if (topicScore >= 0.5m && lexicalScore >= 0.45m)
        {
            reasons.Add("primary_due_to_topic_and_lexical_balance");
        }
        else
        {
            reasons.Add("support_due_to_secondary_relevance");
        }

        if (sectionScore >= 0.5m)
        {
            reasons.Add("high_section_alignment");
        }

        if (structureScore >= 0.7m)
        {
            reasons.Add("good_structure_signal");
        }

        if (contentQualityScore >= 0.8m)
        {
            reasons.Add("clean_content_signal");
        }

        if (valueDensityScore >= 0.7m)
        {
            reasons.Add("high_value_density");
        }

        if (questionTypeFitScore >= 0.7m)
        {
            reasons.Add($"question_type_fit:{questionType.ToUpperInvariant()}");
        }

        if (pedagogicalPenalty >= 0.25m)
        {
            reasons.Add("penalized_low_value_for_question_type");
        }

        if (string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase) &&
            LooksLikeHashingApplicationsOverviewChunkWithoutTarget(
                chunk.SectionTitle,
                chunk.ChapterTitle,
                chunk.MarkdownText ?? chunk.NormalizedText ?? chunk.RawText ?? string.Empty,
                chunk.TopicTags,
                targetTopics))
        {
            reasons.Add("penalized_hashing_applications_overview_for_pe");
        }

        return reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static int ComputeChapterOrderDistance(KnowledgeChunk chunk, IReadOnlyList<int> requestedOrders)
    {
        if (!chunk.ChapterOrder.HasValue || requestedOrders.Count == 0)
        {
            return int.MaxValue;
        }

        return requestedOrders.Min(order => Math.Abs(order - chunk.ChapterOrder!.Value));
    }

    private static int ComputePageDistance(KnowledgeChunk chunk, IReadOnlyList<int> requestedPages)
    {
        if (requestedPages.Count == 0)
        {
            return int.MaxValue;
        }

        var chunkPages = new[] { chunk.SourcePageFrom, chunk.SourcePageTo }
            .Where(page => page.HasValue)
            .Select(page => page!.Value)
            .Distinct()
            .ToList();
        if (chunkPages.Count == 0)
        {
            return int.MaxValue;
        }

        return chunkPages.Min(page => requestedPages.Min(anchor => Math.Abs(anchor - page)));
    }

    private static bool LooksLikePracticalCodeChunk(KnowledgeChunk chunk, string content)
        => LooksLikePracticalCodeChunk(chunk.SectionTitle, content, chunk.TopicTags);

    private static bool LooksLikePracticalCodeChunk(string? sectionTitle, string content, IReadOnlyList<string>? topicTags)
    {
        var title = ExtractLeadingMarkdownHeading(content) ?? sectionTitle ?? string.Empty;
        var tags = string.Join(" ", topicTags ?? Array.Empty<string>());
        var combined = $"{title} {tags} {content}";
        return Regex.IsMatch(combined, @"(?i)\b(example|implementing|inheritance|override|overriding|test case|input|output)\b") ||
               content.Contains("```", StringComparison.Ordinal) ||
               Regex.IsMatch(content, @"(?i)\b(public|private|protected)\s+class\b") ||
               content.Contains("System.out", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("Scanner", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeConceptualTheoryChunk(KnowledgeChunk chunk, string content)
    {
        var title = chunk.SectionTitle ?? string.Empty;
        return Regex.IsMatch(title, @"(?i)\b(definition|overview|summary|objectives?)\b") ||
               (Regex.IsMatch(content, @"(?i)^\s*-\s", RegexOptions.Multiline) && !LooksLikePracticalCodeChunk(chunk, content));
    }

    private static decimal ComputeRetrievalContextAdjustment(
        KnowledgeChunk chunk,
        string content,
        string questionType,
        IReadOnlyList<string> targetTopics,
        string? chapterKey,
        string? requestedChapterKey,
        IReadOnlyList<string>? requestedChapterKeys)
    {
        if (!string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        decimal adjustment = 0m;
        var keys = ResolveRequestedChapterKeys(requestedChapterKey, requestedChapterKeys);
        var isRequestedChapterExactHit =
            !string.IsNullOrWhiteSpace(chapterKey) &&
            keys.Contains(chapterKey, StringComparer.OrdinalIgnoreCase);

        if (isRequestedChapterExactHit &&
            IsHashingFocusedRequest(targetTopics) &&
            HasHashingFamilySignals(chunk.SectionTitle, chunk.ChapterTitle, content, chunk.TopicTags))
        {
            adjustment += 0.16m;
        }

        if (!isRequestedChapterExactHit &&
            LooksLikeHashingApplicationsOverviewChunkWithoutTarget(chunk.SectionTitle, chunk.ChapterTitle, content, chunk.TopicTags, targetTopics))
        {
            adjustment -= 0.12m;
        }

        if (!isRequestedChapterExactHit &&
            LooksLikeSpecializedHashingVariantOutsideRequestedScope(chunk.SectionTitle, chunk.ChapterTitle, content, chunk.TopicTags, targetTopics))
        {
            adjustment -= 0.14m;
        }

        if (isRequestedChapterExactHit &&
            IsQueueFocusedRequest(targetTopics) &&
            HasQueueFamilySignals(chunk.SectionTitle, chunk.ChapterTitle, content, chunk.TopicTags))
        {
            adjustment += 0.18m;
        }

        if (IsQueueFocusedRequest(targetTopics) &&
            LooksLikeCoreQueueImplementationChunk(chunk.SectionTitle, chunk.ChapterTitle, content, chunk.TopicTags) &&
            !LooksLikeSpecializedQueueVariantOutsideRequestedScope(chunk.SectionTitle, chunk.ChapterTitle, content, chunk.TopicTags, targetTopics))
        {
            adjustment += 0.30m;
        }

        if (!isRequestedChapterExactHit &&
            LooksLikeQueueApplicationsOverviewOutsideRequestedScope(chunk.SectionTitle, chunk.ChapterTitle, content, chunk.TopicTags, targetTopics))
        {
            adjustment -= 0.35m;
        }

        if (!isRequestedChapterExactHit &&
            LooksLikeSpecializedQueueVariantOutsideRequestedScope(chunk.SectionTitle, chunk.ChapterTitle, content, chunk.TopicTags, targetTopics))
        {
            adjustment -= 0.45m;
        }

        return adjustment;
    }

    private static bool IsHashingFocusedRequest(IReadOnlyList<string> targetTopics)
        => targetTopics.Any(topic =>
            !string.IsNullOrWhiteSpace(topic) &&
            Regex.IsMatch(topic, @"(?i)\b(hashing|collision resolution|open addressing|linear probing|quadratic probing|hash tables?)\b"));

    private static bool IsQueueFocusedRequest(IReadOnlyList<string> targetTopics)
        => targetTopics.Any(topic =>
            !string.IsNullOrWhiteSpace(topic) &&
            Regex.IsMatch(topic, @"(?i)\b(queues?|array-based queue|circular queue|queue interface)\b"));

    private static bool LooksLikeHashingApplicationsOverviewChunkWithoutTarget(
        string? sectionTitle,
        string? chapterTitle,
        string content,
        IReadOnlyList<string>? topicTags,
        IReadOnlyList<string> targetTopics)
    {
        if (targetTopics.Any(topic =>
                !string.IsNullOrWhiteSpace(topic) &&
                Regex.IsMatch(topic, @"(?i)\b(application|applications|use case|use cases|routing|database|symbol table)\b")))
        {
            return false;
        }

        var title = $"{chapterTitle} {sectionTitle}".Trim();
        if (!HasHashingFamilySignals(sectionTitle, chapterTitle, content, topicTags))
        {
            return false;
        }

        var applicationsFocused =
            Regex.IsMatch(title, @"(?i)\bapplications? of hashing\b|\bapplications?\b|\buse cases?\b|\boverview\b") ||
            Regex.IsMatch(content, @"(?i)\b(efficient retrieval of records|symbol tables?|lookup board configuration|quick command lookup|ip routing)\b");

        if (!applicationsFocused)
        {
            return false;
        }

        return !LooksLikePracticalCodeChunk(sectionTitle, content, topicTags);
    }

    private static bool HasHashingFamilySignals(string? sectionTitle, string? chapterTitle, string content, IReadOnlyList<string>? topicTags)
    {
        var title = $"{chapterTitle} {sectionTitle}".Trim();
        var tags = string.Join(" ", topicTags ?? Array.Empty<string>());
        var combined = $"{title} {tags} {content}";
        return Regex.IsMatch(combined, @"(?i)\b(hashing|hash function|hash functions|hash table|hash tables|collision resolution|open addressing|linear probing|quadratic probing|chaining)\b");
    }

    private static bool HasQueueFamilySignals(string? sectionTitle, string? chapterTitle, string content, IReadOnlyList<string>? topicTags)
    {
        var title = $"{chapterTitle} {sectionTitle}".Trim();
        var tags = string.Join(" ", topicTags ?? Array.Empty<string>());
        var combined = $"{title} {tags} {content}";
        return Regex.IsMatch(combined, @"(?i)\b(queue|queues|fifo|enqueue|dequeue|front|rear|circular queue|array-based queue|priority queue|deque|double-?ended queue|round robin)\b");
    }

    private static bool LooksLikeSpecializedHashingVariantOutsideRequestedScope(
        string? sectionTitle,
        string? chapterTitle,
        string content,
        IReadOnlyList<string>? topicTags,
        IReadOnlyList<string> targetTopics)
    {
        if (!IsHashingFocusedRequest(targetTopics) ||
            targetTopics.Any(topic =>
                !string.IsNullOrWhiteSpace(topic) &&
                Regex.IsMatch(topic, @"(?i)\b(collision resolution|open addressing|linear probing|quadratic probing|perfect hash|extendible|extendable|cryptographic)\b")))
        {
            return false;
        }

        var title = $"{chapterTitle} {sectionTitle}".Trim();
        var tags = string.Join(" ", topicTags ?? Array.Empty<string>());
        var combined = $"{title} {tags} {content}";

        return Regex.IsMatch(
            combined,
            @"(?i)\b(extendible|extendable|dynamic hashing|cryptographic hash|message digest|checksum|perfect hash|perfect hashing)\b");
    }

    private static bool LooksLikeQueueApplicationsOverviewOutsideRequestedScope(
        string? sectionTitle,
        string? chapterTitle,
        string content,
        IReadOnlyList<string>? topicTags,
        IReadOnlyList<string> targetTopics)
    {
        if (!IsQueueFocusedRequest(targetTopics) ||
            targetTopics.Any(topic =>
                !string.IsNullOrWhiteSpace(topic) &&
                Regex.IsMatch(topic, @"(?i)\b(round robin|scheduler|scheduling|applications?)\b")))
        {
            return false;
        }

        var title = $"{chapterTitle} {sectionTitle}".Trim();
        var tags = string.Join(" ", topicTags ?? Array.Empty<string>());
        var combined = $"{title} {tags} {content}";

        return Regex.IsMatch(title, @"(?i)\bapplications? of queues?\b|\bapplications?\b") ||
               Regex.IsMatch(combined, @"(?i)\b(waiting lists?|shared resources?|multiprogramming|round robin|scheduler|scheduling)\b");
    }

    private static bool LooksLikeSpecializedQueueVariantOutsideRequestedScope(
        string? sectionTitle,
        string? chapterTitle,
        string content,
        IReadOnlyList<string>? topicTags,
        IReadOnlyList<string> targetTopics)
    {
        if (!IsQueueFocusedRequest(targetTopics) ||
            targetTopics.Any(topic =>
                !string.IsNullOrWhiteSpace(topic) &&
                Regex.IsMatch(topic, @"(?i)\b(priority queue|priority queues|deque|double-?ended queue|round robin|scheduler|scheduling)\b")))
        {
            return false;
        }

        var title = $"{chapterTitle} {sectionTitle}".Trim();
        var tags = string.Join(" ", topicTags ?? Array.Empty<string>());
        var combined = $"{title} {tags} {content}";

        return HasSpecializedQueueVariantSignals(combined);
    }

    private static bool LooksLikeCoreQueueImplementationChunk(
        string? sectionTitle,
        string? chapterTitle,
        string content,
        IReadOnlyList<string>? topicTags)
    {
        var title = $"{chapterTitle} {sectionTitle}".Trim();
        var tags = string.Join(" ", topicTags ?? Array.Empty<string>());
        var combined = $"{title} {tags} {content}";

        if (HasSpecializedQueueVariantSignals(combined))
        {
            return false;
        }

        var strongCoreSignals = Regex.IsMatch(
            combined,
            @"(?i)\b(array-based queue|circular queue|queue interface|queue adt|fifo|first in first out|front element|rear element|front index|rear index|circular fashion|first and last)\b");
        var apiSurfaceSignals = Regex.IsMatch(
            combined,
            @"(?i)\b(enqueue|dequeue|front\(\)|isempty|isfull|size\(\)|clear\(\))\b");

        return strongCoreSignals || apiSurfaceSignals;
    }

    private static bool HasSpecializedQueueVariantSignals(string combined)
        => Regex.IsMatch(
            combined,
            @"(?i)\b(priority queue|priority queues|deque|double-?ended queue|round robin|scheduler|scheduling)\b");

    private static bool LooksLikeOffTargetQueueVariantChunk(
        string? sectionTitle,
        string? chapterTitle,
        string content,
        IReadOnlyList<string>? topicTags,
        IReadOnlyList<string> targetTopics)
    {
        if (!IsQueueFocusedRequest(targetTopics) ||
            targetTopics.Any(topic =>
                !string.IsNullOrWhiteSpace(topic) &&
                Regex.IsMatch(topic, @"(?i)\b(priority queue|priority queues|deque|double-?ended queue|round robin|scheduler|scheduling|applications?)\b")))
        {
            return false;
        }

        if (LooksLikeCoreQueueImplementationChunk(sectionTitle, chapterTitle, content, topicTags))
        {
            return false;
        }

        return LooksLikeQueueApplicationsOverviewOutsideRequestedScope(sectionTitle, chapterTitle, content, topicTags, targetTopics) ||
               LooksLikeSpecializedQueueVariantOutsideRequestedScope(sectionTitle, chapterTitle, content, topicTags, targetTopics);
    }

    private static bool LooksLikeLowValueTheorySectionForPe(KnowledgeChunk chunk, string content)
        => LooksLikeLowValueTheorySectionForPe(chunk.SectionTitle, content, chunk.TopicTags);

    private static bool LooksLikeLowValueTheorySectionForPe(string? sectionTitle, string content, IReadOnlyList<string>? topicTags)
    {
        var title = (ExtractLeadingMarkdownHeading(content) ?? sectionTitle ?? string.Empty).Trim();
        var tags = string.Join(" ", topicTags ?? Array.Empty<string>());

        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        if (Regex.IsMatch(title, @"(?i)^\s*objectives?\s*$|^\s*summary\s*$|^\s*definition of\b|^\s*key points?\s*$"))
        {
            return true;
        }

        if (Regex.IsMatch(title, @"(?i)^\s*theorem\.?\s*$|^\s*lemma\.?\s*$|^\s*corollary\.?\s*$"))
        {
            return true;
        }

        if (Regex.IsMatch(title, @"(?i)^why and when to use\b"))
        {
            return true;
        }

        if (Regex.IsMatch(title, @"(?i)\b(interface|interfaces)\b") &&
            !LooksLikePracticalCodeChunk(sectionTitle, content, topicTags))
        {
            return true;
        }

        var bulletOnlyTheory = Regex.Matches(content, @"(?m)^\s*-\s").Count >= 3 &&
                               !LooksLikePracticalCodeChunk(sectionTitle, content, topicTags);
        if (bulletOnlyTheory && !Regex.IsMatch(tags, @"(?i)\b(method overriding|constructors|arrays|stacks|queues|trees|sorting|searching)\b"))
        {
            return true;
        }

        return false;
    }

    private static bool LooksLikeReferenceOnlyChunk(string? sectionTitle, string content, IReadOnlyList<string>? topicTags)
    {
        var title = (ExtractLeadingMarkdownHeading(content) ?? sectionTitle ?? string.Empty).Trim();
        var normalizedTitle = NormalizeWhitespaceForDetection(title);
        var normalizedContent = NormalizeWhitespaceForDetection(content);
        var tags = NormalizeWhitespaceForDetection(string.Join(" ", topicTags ?? Array.Empty<string>()));

        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            return true;
        }

        var titleLooksReferenceOnly =
            Regex.IsMatch(normalizedTitle, @"(?i)^\s*text\s*book\s*$") ||
            Regex.IsMatch(normalizedTitle, @"(?i)^\s*reading at home\s*$") ||
            Regex.IsMatch(normalizedTitle, @"(?i)^\s*(references?|further reading|bibliography|recommended reading)\s*$");

        var containsReferenceHeading =
            Regex.IsMatch(normalizedContent, @"(?i)\btext\s*book\s*:") ||
            Regex.IsMatch(normalizedContent, @"(?i)\breading at home\b") ||
            Regex.IsMatch(normalizedContent, @"(?i)\b(references?|bibliography|recommended reading|further reading)\b");

        var bulletPageReferenceCount = Regex.Matches(content, @"(?m)^\s*[-*]\s*\d+(?:\.\d+){0,2}\s+.+?\s*-\s*\d+\s*$").Count;
        var lineCount = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
        var containsMostlyChapterReferences =
            bulletPageReferenceCount >= 2 &&
            Regex.IsMatch(normalizedContent, @"(?i)\b(queues?|deques?|priority queues?|stacks?|trees?|graphs?|sorting|searching|inheritance|polymorphism)\b");
        var isBareReferenceListChunk =
            bulletPageReferenceCount >= 2 &&
            lineCount <= 5 &&
            normalizedContent.Length <= 180 &&
            !Regex.IsMatch(normalizedContent, @"(?i)\b(is|are|can|will|should|must|returns?|removes?|adds?|dequeues?|enqueues?)\b");

        var contentLooksReferenceOnly =
            Regex.IsMatch(normalizedContent, @"(?i)^reading at home\b.*\btext\s*book\s*:", RegexOptions.Singleline) ||
            Regex.IsMatch(normalizedContent, @"(?i)\bchapter\s+\d+\b") ||
            containsMostlyChapterReferences ||
            isBareReferenceListChunk;

        var tagsAreOnlyBroadNavigation = !string.IsNullOrWhiteSpace(tags) &&
                                         !Regex.IsMatch(tags, @"(?i)\b(example|implementation|algorithm|operations?|methods?|complexity|application)\b");

        return ((titleLooksReferenceOnly || containsReferenceHeading) &&
                (contentLooksReferenceOnly || containsMostlyChapterReferences || tagsAreOnlyBroadNavigation)) ||
               isBareReferenceListChunk;
    }

    private static string NormalizeWhitespaceForDetection(string? content)
        => Regex.Replace(content ?? string.Empty, @"\s+", " ").Trim();

    private static bool HasStrongTopicAnchorFallback(string? sectionTitle, string content, IReadOnlyList<string>? topicTags, IReadOnlyList<string> targetTopics)
    {
        if (targetTopics.Count == 0)
        {
            return false;
        }

        var normalizedTags = topicTags ?? Array.Empty<string>();
        var title = sectionTitle ?? string.Empty;
        var combined = $"{title} {content}";
        var exactTopicHits = OverlapCount(normalizedTags, targetTopics);
        var anchoredTopics = targetTopics.Count(topic =>
            (!string.IsNullOrWhiteSpace(title) && title.Contains(topic, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(content) && content.Contains(topic, StringComparison.OrdinalIgnoreCase)) ||
            normalizedTags.Any(tag => IsExactTopicMatch(tag, topic) || IsPartialTopicMatch(tag, topic)));

        if (exactTopicHits == 0 && anchoredTopics == 0)
        {
            return false;
        }

        if (Regex.IsMatch(combined, @"(?i)\binterface\b|\binterfaces\b|\bimplements\b|\boverloading\b|\banonymous class\b|\bnested class\b|\blocal class\b|\binner class\b"))
        {
            return false;
        }

        var normalizedLength = Regex.Replace(content, @"\s+", " ").Trim().Length;
        return exactTopicHits > 0 && anchoredTopics > 0 && normalizedLength >= 80;
    }

    private static string? ExtractLeadingMarkdownHeading(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var match = Regex.Match(content, @"(?m)^\s{0,3}#{1,6}\s+(.+?)\s*$");
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[1].Value.Trim();
    }

    private static bool LooksLikeOverloadingChunkWithoutTarget(KnowledgeChunk chunk, IReadOnlyList<string> targetTopics)
    {
        if (HasExplicitSubtopicTarget(targetTopics, "overloading", "constructor"))
        {
            return false;
        }

        var combined = $"{chunk.SectionTitle} {chunk.ChapterTitle} {string.Join(" ", chunk.TopicTags ?? new List<string>())} {chunk.MarkdownText ?? chunk.NormalizedText ?? chunk.RawText ?? string.Empty}";
        return Regex.IsMatch(combined, @"(?i)\boverloading\b|\bconstructor overloading\b");
    }

    private static bool LooksLikeOverloadingChunkWithoutTarget(RetrievedChunkSnapshot chunk, IReadOnlyList<string> targetTopics)
    {
        if (HasExplicitSubtopicTarget(targetTopics, "overloading", "constructor"))
        {
            return false;
        }

        var combined = $"{chunk.SectionTitle} {chunk.ChapterKey} {string.Join(" ", chunk.TopicTags ?? new List<string>())} {chunk.ContentText ?? string.Empty}";
        return Regex.IsMatch(combined, @"(?i)\boverloading\b|\bconstructor overloading\b");
    }

    private static bool LooksLikeInterfaceChunkWithoutTarget(KnowledgeChunk chunk, IReadOnlyList<string> targetTopics)
    {
        if (HasExplicitSubtopicTarget(targetTopics, "interface", "interfaces"))
        {
            return false;
        }

        var combined = $"{chunk.SectionTitle} {chunk.ChapterTitle} {string.Join(" ", chunk.TopicTags ?? new List<string>())} {chunk.MarkdownText ?? chunk.NormalizedText ?? chunk.RawText ?? string.Empty}";
        return Regex.IsMatch(combined, @"(?i)\binterface\b|\binterfaces\b|\bimplements\b|\bmultiple interfaces\b");
    }

    private static bool LooksLikeInterfaceChunkWithoutTarget(RetrievedChunkSnapshot chunk, IReadOnlyList<string> targetTopics)
    {
        if (HasExplicitSubtopicTarget(targetTopics, "interface", "interfaces"))
        {
            return false;
        }

        var combined = $"{chunk.SectionTitle} {chunk.ChapterKey} {string.Join(" ", chunk.TopicTags ?? new List<string>())} {chunk.ContentText ?? string.Empty}";
        return Regex.IsMatch(combined, @"(?i)\binterface\b|\binterfaces\b|\bimplements\b|\bmultiple interfaces\b");
    }

    private static bool LooksLikeAnonymousOrNestedChunkWithoutTarget(KnowledgeChunk chunk, IReadOnlyList<string> targetTopics)
    {
        if (HasExplicitSubtopicTarget(targetTopics, "anonymous", "nested", "local", "inner"))
        {
            return false;
        }

        var combined = $"{chunk.SectionTitle} {chunk.ChapterTitle} {string.Join(" ", chunk.TopicTags ?? new List<string>())} {chunk.MarkdownText ?? chunk.NormalizedText ?? chunk.RawText ?? string.Empty}";
        return Regex.IsMatch(combined, @"(?i)\banonymous class\b|\banonymous classes\b|\bnested class\b|\bnested classes\b|\blocal class\b|\binner class\b");
    }

    private static bool LooksLikeAnonymousOrNestedChunkWithoutTarget(RetrievedChunkSnapshot chunk, IReadOnlyList<string> targetTopics)
    {
        if (HasExplicitSubtopicTarget(targetTopics, "anonymous", "nested", "local", "inner"))
        {
            return false;
        }

        var combined = $"{chunk.SectionTitle} {chunk.ChapterKey} {string.Join(" ", chunk.TopicTags ?? new List<string>())} {chunk.ContentText ?? string.Empty}";
        return Regex.IsMatch(combined, @"(?i)\banonymous class\b|\banonymous classes\b|\bnested class\b|\bnested classes\b|\blocal class\b|\binner class\b");
    }

    private static bool HasExplicitSubtopicTarget(IReadOnlyList<string> targetTopics, params string[] markers)
        => targetTopics.Any(topic =>
            !string.IsNullOrWhiteSpace(topic) &&
            markers.Any(marker => topic.Contains(marker, StringComparison.OrdinalIgnoreCase)));

    private static bool LooksLikeExampleSection(string? sectionTitle)
        => !string.IsNullOrWhiteSpace(sectionTitle) &&
           Regex.IsMatch(sectionTitle, @"(?i)\b(example|implementing|inheritance|override|overriding|algorithm|exercise)\b");

    private static bool MatchesExplicitTopicAnchor(KnowledgeChunk chunk, IReadOnlyList<string> targetTopics)
    {
        var titleText = $"{chunk.SectionTitle} {chunk.ChapterTitle}".Trim();
        if (string.IsNullOrWhiteSpace(titleText))
        {
            return false;
        }

        var loweredTitle = titleText.ToLowerInvariant();
        foreach (var topic in targetTopics)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                continue;
            }

            var normalizedTopic = topic.Trim().ToLowerInvariant();
            if (loweredTitle.Contains(normalizedTopic, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var anchor = ExtractTopicAnchor(normalizedTopic);
            if (!string.IsNullOrWhiteSpace(anchor) &&
                loweredTitle.Contains(anchor, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasExactTopicEvidence(KnowledgeChunk chunk, IReadOnlyList<string> targetTopics)
    {
        if (targetTopics.Count == 0)
        {
            return false;
        }

        var titleText = $"{chunk.SectionTitle} {chunk.ChapterTitle}".Trim();
        var content = chunk.MarkdownText ?? chunk.NormalizedText ?? chunk.RawText ?? string.Empty;
        var tags = chunk.TopicTags ?? new List<string>();

        foreach (var topic in targetTopics)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                continue;
            }

            if (tags.Any(tag => IsExactTopicMatch(tag, topic)))
            {
                return true;
            }

            var normalizedTopic = NormalizeTopicLabel(topic);
            if ((!string.IsNullOrWhiteSpace(titleText) && titleText.Contains(normalizedTopic, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(content) && content.Contains(normalizedTopic, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string ExtractTopicAnchor(string topic)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "java", "oop", "method", "class", "classes", "object", "objects"
        };

        var words = Regex.Matches(topic, @"[a-zA-Z]+")
            .Select(match => match.Value.ToLowerInvariant())
            .Where(word => !stopWords.Contains(word))
            .ToList();

        return words.Count == 0 ? string.Empty : words[^1];
    }

    private static TopicCoverageSnapshot BuildTopicCoverage(IReadOnlyList<RetrievedChunkSnapshot> selected, IReadOnlyList<string> targetTopics)
    {
        var coveredTopics = targetTopics
            .Where(topic => selected.Any(chunk => ChunkCoversTopic(chunk, topic)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var uncoveredTopics = targetTopics
            .Where(topic => !coveredTopics.Contains(topic, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ratio = targetTopics.Count == 0
            ? 1m
            : Math.Round((decimal)coveredTopics.Count / targetTopics.Count, 4);

        return new TopicCoverageSnapshot(coveredTopics, uncoveredTopics, ratio);
    }

    private static bool ChunkCoversTopic(RetrievedChunkSnapshot chunk, string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        return chunk.TopicTags.Any(tag =>
            IsExactTopicMatch(tag, topic) ||
            IsPartialTopicMatch(tag, topic));
    }

    private static bool IsExactTopicMatch(string? tag, string? topic)
    {
        var normalizedTag = NormalizeTopicLabel(tag);
        var normalizedTopic = NormalizeTopicLabel(topic);
        if (string.IsNullOrWhiteSpace(normalizedTag) || string.IsNullOrWhiteSpace(normalizedTopic))
        {
            return false;
        }

        return string.Equals(normalizedTag, normalizedTopic, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPartialTopicMatch(string? tag, string? topic)
    {
        var normalizedTag = NormalizeTopicLabel(tag);
        var normalizedTopic = NormalizeTopicLabel(topic);
        if (string.IsNullOrWhiteSpace(normalizedTag) || string.IsNullOrWhiteSpace(normalizedTopic))
        {
            return false;
        }

        if (string.Equals(normalizedTag, normalizedTopic, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalizedTag.Contains(normalizedTopic, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tagTokens = TokenizeTopicLabel(normalizedTag);
        var topicTokens = TokenizeTopicLabel(normalizedTopic);
        if (tagTokens.Count == 1 && topicTokens.Count == 1)
        {
            return normalizedTag.Contains(normalizedTopic, StringComparison.OrdinalIgnoreCase) ||
                   normalizedTopic.Contains(normalizedTag, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string NormalizeTopicLabel(string? value)
        => Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), @"\s+", " ");

    private static List<string> TokenizeTopicLabel(string value)
        => Regex.Matches(value, @"[a-z0-9_]+")
            .Select(match => match.Value)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();

    private static int EstimatePackedTokenCost(RetrievedChunkSnapshot chunk, IReadOnlyList<string> targetTopics, string? retrievalQuery)
    {
        var packedText = BuildPackedText(chunk, targetTopics, retrievalQuery);
        return Math.Max(1, EstimateTokenCount(packedText));
    }

    private static int GetMaxChunksPerSection(RetrievedChunkSnapshot chunk)
        => chunk.TokenCount <= ShortChunkSectionExpansionTokenThreshold
            ? MaximumShortChunksPerSectionKey
            : MaximumChunksPerSectionKey;

    private static void AddContextualSelectionReasons(RetrievedChunkSnapshot chunk, IReadOnlyList<RetrievedChunkSnapshot> selected, RetrievedChunkSnapshot? anchor)
    {
        if (anchor is not null)
        {
            if (SameChapter(anchor, chunk))
            {
                AddSelectionReason(chunk, "same_chapter_as_anchor");
            }

            if (HasNearPageDistance(anchor, chunk, NearPageDistanceThreshold))
            {
                AddSelectionReason(chunk, "near_anchor_page");
            }

            if (SameDocument(anchor, chunk) &&
                IsHashingFamilyChunk(anchor) &&
                IsHashingFamilyChunk(chunk))
            {
                AddSelectionReason(chunk, "same_hashing_family_as_anchor");
            }
        }

        if (selected.Any(existing => SameChapter(existing, chunk)))
        {
            AddSelectionReason(chunk, "same_chapter_as_selected_context");
        }

        if (selected.Any(existing => HasNearPageDistance(existing, chunk, NearPageDistanceThreshold)))
        {
            AddSelectionReason(chunk, "near_selected_context_page");
        }

        if (selected.Any(existing =>
                SameDocument(existing, chunk) &&
                IsHashingFamilyChunk(existing) &&
                IsHashingFamilyChunk(chunk)))
        {
            AddSelectionReason(chunk, "same_hashing_family_as_selected_context");
        }

        if (selected.Any(existing =>
                SameDocument(existing, chunk) &&
                IsCoreQueueFamilyChunk(existing) &&
                IsCoreQueueFamilyChunk(chunk)))
        {
            AddSelectionReason(chunk, "same_queue_family_as_selected_context");
        }

        if (chunk.TokenCount <= ShortChunkSectionExpansionTokenThreshold && selected.Any(existing => string.Equals(BuildSectionKey(existing), BuildSectionKey(chunk), StringComparison.OrdinalIgnoreCase)))
        {
            AddSelectionReason(chunk, "short_chunk_allowed_same_section");
        }
    }

    private static bool IsContextuallyCompatible(RetrievedChunkSnapshot candidate, IReadOnlyList<RetrievedChunkSnapshot> selected, RetrievedChunkSnapshot? anchor)
    {
        if (selected.Count == 0)
        {
            return true;
        }

        if (anchor is null)
        {
            anchor = selected[0];
        }

        if (SameChapter(anchor, candidate))
        {
            return true;
        }

        if (HasNearPageDistance(anchor, candidate, NearPageDistanceThreshold))
        {
            return true;
        }

        if (selected.Any(existing => SameChapter(existing, candidate) || HasNearPageDistance(existing, candidate, NearPageDistanceThreshold)))
        {
            return true;
        }

        if (HasHashingFamilyContextSupport(candidate, selected, anchor))
        {
            return true;
        }

        if (HasQueueFamilyContextSupport(candidate, selected, anchor))
        {
            return true;
        }

        var hasStrongTopicNeed = candidate.TopicScore >= 1m || (candidate.TopicScore >= 0.75m && candidate.FinalScore >= 0.7m);
        if (!hasStrongTopicNeed)
        {
            return false;
        }

        if (anchor.SourcePageFrom.HasValue && candidate.SourcePageFrom.HasValue && !HasNearPageDistance(anchor, candidate, FarPageDistanceThreshold))
        {
            return false;
        }

        return true;
    }

    private static string BuildDenseChunkPreview(string content, IReadOnlyList<string> targetTopics, string? retrievalQuery)
    {
        var normalized = NormalizeWhitespace(content);
        if (normalized.Length <= DenseChunkPackedPreviewCharacterLimit)
        {
            return normalized;
        }

        var terms = ExtractTerms(retrievalQuery)
            .Concat(targetTopics.SelectMany(ExtractTerms))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var segments = SplitIntoSegments(content)
            .Select(segment => segment.Trim())
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (segments.Count == 0)
        {
            return normalized[..Math.Min(normalized.Length, DenseChunkPackedPreviewCharacterLimit)];
        }

        var selected = new List<string>();

        foreach (var segment in segments
                     .OrderByDescending(segment => ComputeSegmentPriority(segment, terms))
                     .ThenBy(segment => segment.Length))
        {
            if (selected.Contains(segment, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            selected.Add(TrimSegment(segment));
            var candidate = string.Join(Environment.NewLine, selected);
            if (candidate.Length >= DenseChunkPackedPreviewCharacterLimit)
            {
                break;
            }
        }

        if (selected.Count == 0)
        {
            selected.AddRange(segments.Take(4).Select(TrimSegment));
        }

        var preview = string.Join(Environment.NewLine, selected);
        if (preview.Length > DenseChunkPackedPreviewCharacterLimit)
        {
            preview = $"{preview[..DenseChunkPackedPreviewCharacterLimit]}...";
        }

        return preview;
    }

    private static IEnumerable<string> SplitIntoSegments(string content)
    {
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count >= 3)
        {
            return lines;
        }

        return Regex.Split(content, @"(?<=[\.\!\?;:])\s+")
            .Select(segment => segment.Trim())
            .Where(segment => !string.IsNullOrWhiteSpace(segment));
    }

    private static int ComputeSegmentPriority(string segment, IReadOnlyList<string> terms)
    {
        var priority = 0;
        foreach (var term in terms)
        {
            if (segment.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                priority += 3;
            }
        }

        if (Regex.IsMatch(segment, @"(?i)\b(example|rule|syntax|algorithm|output|input|note|constraint)\b"))
        {
            priority += 2;
        }

        if (segment.Contains("```", StringComparison.Ordinal) ||
            segment.Contains("printf", StringComparison.OrdinalIgnoreCase) ||
            segment.Contains("System.out", StringComparison.OrdinalIgnoreCase) ||
            segment.Contains("public class", StringComparison.OrdinalIgnoreCase))
        {
            priority += 2;
        }

        if (segment.Length is >= 40 and <= 260)
        {
            priority += 1;
        }

        return priority;
    }

    private static string TrimSegment(string segment)
    {
        var normalized = NormalizeWhitespace(segment);
        return normalized.Length <= DenseChunkSnippetCharacterLimit
            ? normalized
            : $"{normalized[..DenseChunkSnippetCharacterLimit]}...";
    }

    private static string NormalizeWhitespace(string content)
        => Regex.Replace(content ?? string.Empty, @"[ \t]+", " ").Trim();

    private static DateTime MaxDate(params DateTime?[] values)
        => values
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

    private static void AddSelectionReason(RetrievedChunkSnapshot chunk, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        if (!chunk.SelectionReasons.Contains(reason, StringComparer.OrdinalIgnoreCase))
        {
            chunk.SelectionReasons.Add(reason);
        }
    }

    private static bool SameChapter(RetrievedChunkSnapshot left, RetrievedChunkSnapshot right)
        => !string.IsNullOrWhiteSpace(left.ChapterKey) &&
           !string.IsNullOrWhiteSpace(right.ChapterKey) &&
           string.Equals(left.ChapterKey, right.ChapterKey, StringComparison.OrdinalIgnoreCase);

    private static bool SameDocument(RetrievedChunkSnapshot left, RetrievedChunkSnapshot right)
        => !string.IsNullOrWhiteSpace(left.DocumentId) &&
           !string.IsNullOrWhiteSpace(right.DocumentId) &&
           string.Equals(left.DocumentId, right.DocumentId, StringComparison.OrdinalIgnoreCase);

    private static bool IsOptionalContextuallyUseful(
        RetrievedChunkSnapshot candidate,
        IReadOnlyList<RetrievedChunkSnapshot> selected,
        RetrievedChunkSnapshot? anchor)
    {
        if (anchor is not null && (SameChapter(anchor, candidate) || HasNearPageDistance(anchor, candidate, NearPageDistanceThreshold)))
        {
            return true;
        }

        if (HasHashingFamilyContextSupport(candidate, selected, anchor))
        {
            return true;
        }

        return selected.Any(existing =>
            SameChapter(existing, candidate) ||
            HasNearPageDistance(existing, candidate, NearPageDistanceThreshold));
    }

    private static bool HasHashingFamilyContextSupport(
        RetrievedChunkSnapshot candidate,
        IReadOnlyList<RetrievedChunkSnapshot> selected,
        RetrievedChunkSnapshot? anchor)
    {
        if (!IsHashingFamilyChunk(candidate) || candidate.TopicScore < 0.75m)
        {
            return false;
        }

        if (anchor is not null &&
            SameDocument(anchor, candidate) &&
            IsHashingFamilyChunk(anchor))
        {
            return true;
        }

        return selected.Any(existing =>
            SameDocument(existing, candidate) &&
            IsHashingFamilyChunk(existing));
    }

    private static bool IsHashingFamilyChunk(RetrievedChunkSnapshot chunk)
    {
        var combined = $"{chunk.ChapterKey} {chunk.SectionTitle} {string.Join(" ", chunk.TopicTags ?? new List<string>())} {chunk.ContentText}".Trim();
        if (string.IsNullOrWhiteSpace(combined))
        {
            return false;
        }

        return Regex.IsMatch(
            combined,
            @"(?i)\b(hashing|hash function|hash functions|hash table|hash tables|collision resolution|collision handling|open addressing|linear probing|quadratic probing|separate chaining|chaining|bucket addressing|perfect hash|extendable hash|cryptographic hash)\b");
    }

    private static bool HasQueueFamilyContextSupport(
        RetrievedChunkSnapshot candidate,
        IReadOnlyList<RetrievedChunkSnapshot> selected,
        RetrievedChunkSnapshot? anchor)
    {
        if (!IsCoreQueueFamilyChunk(candidate) || candidate.TopicScore < 0.75m)
        {
            return false;
        }

        if (anchor is not null &&
            SameDocument(anchor, candidate) &&
            IsCoreQueueFamilyChunk(anchor))
        {
            return true;
        }

        return selected.Any(existing =>
            SameDocument(existing, candidate) &&
            IsCoreQueueFamilyChunk(existing));
    }

    private static bool IsCoreQueueFamilyChunk(RetrievedChunkSnapshot chunk)
        => LooksLikeCoreQueueImplementationChunk(chunk.SectionTitle, chunk.ChapterKey, chunk.ContentText ?? string.Empty, chunk.TopicTags);

    private static bool ShouldSkipPeLooseSupportChunk(
        RetrievedChunkSnapshot candidate,
        string passRole,
        string questionType,
        IReadOnlyList<string> targetTopics,
        IReadOnlyList<RetrievedChunkSnapshot> selected)
    {
        if (!string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (targetTopics.Count == 0)
        {
            return false;
        }

        if (string.Equals(passRole, "primary", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var exactTopicPrimaryExists = selected.Any(item =>
            string.Equals(item.SelectionRole, "primary", StringComparison.OrdinalIgnoreCase) &&
            item.TopicScore >= 1m);

        if (!exactTopicPrimaryExists)
        {
            return false;
        }

        if (candidate.TopicScore >= 0.75m)
        {
            return false;
        }

        if (HasTopicTitleAnchor(candidate, targetTopics))
        {
            return false;
        }

        return true;
    }

    private static bool HasTopicTitleAnchor(RetrievedChunkSnapshot chunk, IReadOnlyList<string> targetTopics)
    {
        var title = chunk.SectionTitle ?? string.Empty;
        var content = chunk.ContentText ?? string.Empty;

        foreach (var topic in targetTopics)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                continue;
            }

            var normalizedTopic = NormalizeTopicLabel(topic);
            if ((!string.IsNullOrWhiteSpace(title) && title.Contains(normalizedTopic, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(content) && content.Contains(normalizedTopic, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNearPageDistance(RetrievedChunkSnapshot left, RetrievedChunkSnapshot right, int threshold)
    {
        var leftPage = left.SourcePageFrom ?? left.SourcePageTo;
        var rightPage = right.SourcePageFrom ?? right.SourcePageTo;
        if (!leftPage.HasValue || !rightPage.HasValue)
        {
            return false;
        }

        return Math.Abs(leftPage.Value - rightPage.Value) <= threshold;
    }

    private static bool LooksStructuredSection(string sectionTitle)
        => Regex.IsMatch(sectionTitle, @"(?i)^(chapter|chap|chuong|section|sec|part|module|unit|\d+(\.\d+){0,2})");

    private static string? ResolveDominantChapterKey(IReadOnlyList<RetrievedChunkSnapshot> selected)
        => selected
            .Where(item => !string.IsNullOrWhiteSpace(item.ChapterKey))
            .GroupBy(item => item.ChapterKey!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Sum(item => item.FinalScore))
            .Select(group => group.Key)
            .FirstOrDefault();

    private static string FormatPageRange(int? from, int? to)
    {
        if (!from.HasValue && !to.HasValue)
        {
            return "n/a";
        }

        if (from.HasValue && to.HasValue)
        {
            return from.Value == to.Value ? from.Value.ToString() : $"{from}-{to}";
        }

        return (from ?? to)!.Value.ToString();
    }

    private static bool HasHighRepetition(string content)
    {
        var words = Regex.Matches(content.ToLowerInvariant(), @"[a-z0-9_]{2,}")
            .Select(match => match.Value)
            .ToList();

        if (words.Count < 12)
        {
            return false;
        }

        var topCount = words.GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Count())
            .DefaultIfEmpty(0)
            .Max();

        return (decimal)topCount / words.Count > 0.25m;
    }

    private static int OverlapCount(IReadOnlyList<string> left, IReadOnlyList<string> right)
        => left.Count(item => right.Contains(item, StringComparer.OrdinalIgnoreCase));

    private static IEnumerable<string> ExtractTerms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Enumerable.Empty<string>();
        }

        return Regex.Matches(text.ToLowerInvariant(), @"[a-z0-9_]{2,}")
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static int EstimateTokenCount(string content)
        => string.IsNullOrWhiteSpace(content) ? 0 : Math.Max(1, (int)Math.Round(content.Length / 4.0));

    private static List<string> ResolveRequestedChapterKeys(string? chapterKey, IReadOnlyList<string>? chapterKeys)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(chapterKey))
        {
            values.Add(chapterKey.Trim());
        }

        if (chapterKeys is not null)
        {
            values.AddRange(chapterKeys.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()));
        }

        return values
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<KnowledgeChunk> FilterChunksByRequestedChapterKeys(IReadOnlyList<KnowledgeChunk> chunks, IReadOnlyList<string> requestedChapterKeys)
    {
        if (requestedChapterKeys.Count == 0)
        {
            return chunks.ToList();
        }

        return chunks
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.ChapterKey) &&
                            requestedChapterKeys.Contains(chunk.ChapterKey, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool HaveSameRequestedChapterKeys(IReadOnlyList<string>? existingKeys, string? chapterKey, IReadOnlyList<string>? chapterKeys)
    {
        var left = ResolveRequestedChapterKeys(chapterKey, chapterKeys);
        var right = ResolveRequestedChapterKeys(null, existingKeys);
        return left.Count == right.Count &&
               left.All(item => right.Contains(item, StringComparer.OrdinalIgnoreCase));
    }

    private static bool HaveSameRetrievalQuery(string? existingQuery, string? requestedQuery)
        => string.Equals(
            NormalizeWhitespace(existingQuery ?? string.Empty),
            NormalizeWhitespace(requestedQuery ?? string.Empty),
            StringComparison.OrdinalIgnoreCase);

    private static void NormalizeRequest(RetrievalPlanRequestDto request)
    {
        request.Subject = request.Subject?.Trim() ?? string.Empty;
        request.ChapterKey = string.IsNullOrWhiteSpace(request.ChapterKey) ? null : request.ChapterKey.Trim();
        request.ChapterKeys = ResolveRequestedChapterKeys(request.ChapterKey, request.ChapterKeys);
        request.ChapterKey = request.ChapterKeys.FirstOrDefault();
        request.QuestionType = request.QuestionType?.Trim() ?? string.Empty;
        request.Difficulty = request.Difficulty?.Trim() ?? string.Empty;
        request.GenerationMode = string.IsNullOrWhiteSpace(request.GenerationMode) ? "Single" : request.GenerationMode.Trim();
        request.SourceScope = string.IsNullOrWhiteSpace(request.SourceScope) ? "SYSTEM" : request.SourceScope.Trim().ToUpperInvariant();
        request.TargetTopics = request.TargetTopics?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
        request.MaxCandidateCount = Math.Max(4, request.MaxCandidateCount);
        request.MaxPackedTokens = Math.Max(MinimumPackedTokenBudget, request.MaxPackedTokens);
        request.RequestedQuestionCount = Math.Max(1, request.RequestedQuestionCount);
    }

    private async Task ValidateRequestAsync(RetrievalPlanRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new InvalidOperationException("Retrieval planner requires userId.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            throw new InvalidOperationException("Retrieval planner requires subject.");
        }

        if (string.IsNullOrWhiteSpace(request.QuestionType))
        {
            throw new InvalidOperationException("Retrieval planner requires questionType.");
        }

        if (request.TargetTopics.Count == 0)
        {
            throw new InvalidOperationException("Retrieval planner requires at least one target topic.");
        }

        if (!request.AllowExtendedScope && request.ChapterKeys.Count > 3)
        {
            throw new InvalidOperationException("Retrieval planner supports at most 3 chapters per request in this phase.");
        }

        if (!string.Equals(request.SourceScope, "SYSTEM", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.SourceScope, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Retrieval planner currently supports only SYSTEM or BYOS sourceScope.");
        }

        if (string.Equals(request.SourceScope, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.CourseId))
            {
                throw new InvalidOperationException("SYSTEM retrieval requires courseId.");
            }

            if (!string.IsNullOrWhiteSpace(request.DocumentId))
            {
                var resolvedDocument = await _documentRepository.GetByIdAsync(request.DocumentId)
                    ?? throw new KeyNotFoundException("Document not found.");

                if (string.Equals(resolvedDocument.Source, "BYOS", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("SYSTEM retrieval cannot use a BYOS document.");
                }

                if (!string.Equals(resolvedDocument.CourseId, request.CourseId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("SYSTEM retrieval documentId does not belong to the requested courseId.");
                }
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(request.DocumentId))
        {
            throw new InvalidOperationException("BYOS retrieval requires documentId.");
        }

        var document = await _documentRepository.GetByIdAsync(request.DocumentId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (!string.Equals(document.UserId, request.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You do not have permission to retrieve chunks from this BYOS document.");
        }

        if (!string.Equals(document.Source, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("BYOS retrieval requires a document whose source is BYOS.");
        }

        if (string.IsNullOrWhiteSpace(request.CourseId) && !string.IsNullOrWhiteSpace(document.CourseId))
        {
            request.CourseId = document.CourseId;
        }
    }

    private static RetrievedChunkDto MapRetrievedChunkDto(RetrievedChunkSnapshot item)
        => new()
        {
            ChunkId = item.ChunkId,
            DocumentId = item.DocumentId,
            ChunkIndex = item.ChunkIndex,
            SectionTitle = item.SectionTitle,
            ChapterKey = item.ChapterKey,
            SourcePageFrom = item.SourcePageFrom,
            SourcePageTo = item.SourcePageTo,
            TopicTags = item.TopicTags,
            SelectionReasons = item.SelectionReasons,
            TokenCount = item.TokenCount,
            TopicScore = item.TopicScore,
            LexicalScore = item.LexicalScore,
            SectionScore = item.SectionScore,
            FinalScore = item.FinalScore,
            SelectionRole = item.SelectionRole,
            ContentText = item.ContentText
        };

    private static ContextPackDto MapContextPackDto(AIContextPack pack)
        => new()
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
            RetrievedChunks = pack.RetrievedChunks.Select(MapRetrievedChunkDto).ToList(),
            Chunks = pack.Chunks.Select(chunk => new PackedChunkDto
            {
                ChunkId = chunk.ChunkId,
                Title = chunk.Title,
                PackedText = chunk.PackedText,
                TokenCount = chunk.TokenCount,
                SelectionRole = chunk.SelectionRole
            }).ToList(),
            SummaryText = pack.SummaryText,
            ContextText = pack.ContextText,
            DominantChapterKey = pack.DominantChapterKey,
            RequestedChapterKeys = pack.RequestedChapterKeys,
            SourcePageFrom = pack.SourcePageFrom,
            SourcePageTo = pack.SourcePageTo,
            CoveredTopics = pack.CoveredTopics,
            UncoveredTopics = pack.UncoveredTopics,
            TopicCoverageRatio = pack.TopicCoverageRatio,
            DistinctSectionCount = pack.DistinctSectionCount,
            TokenCount = pack.TokenCount,
            SourceTokenCount = pack.SourceTokenCount,
            CompressionRatio = pack.CompressionRatio,
            RecommendedQuestionCount = pack.RecommendedQuestionCount,
            MaxQuestionCount = pack.MaxQuestionCount,
            UsageCount = pack.UsageCount,
            LastRetrievalPlanId = pack.LastRetrievalPlanId,
            LastRetrievedAt = pack.LastRetrievedAt,
            LastGenerationRunId = pack.LastGenerationRunId,
            LastGenerationStatus = pack.LastGenerationStatus,
            LastPersistedQuestionIds = pack.LastPersistedQuestionIds,
            LastGeneratedAt = pack.LastGeneratedAt,
            LastUsedAt = pack.LastUsedAt,
            ExpiresAt = pack.ExpiresAt,
            CreatedAt = pack.CreatedAt,
            UpdatedAt = pack.UpdatedAt
        };

    private sealed record TopicCoverageSnapshot(
        List<string> CoveredTopics,
        List<string> UncoveredTopics,
        decimal TopicCoverageRatio);

    private TopicSummaryResultDto BuildTopicSummary(
        string scope,
        string? courseId,
        string? documentId,
        string? documentName,
        string? chapterKey,
        IReadOnlyList<string>? chapterKeys,
        string? chapterTitle,
        string? subject,
        IReadOnlyList<KnowledgeChunk> chunks)
    {
        var taxonomy = _tagTaxonomyProvider.GetCatalog().ResolveBySubject(subject);
        var topics = chunks
            .SelectMany(chunk => (chunk.TopicTags ?? new List<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => new
                {
                    Tag = CanonicalizeTag(tag.Trim(), taxonomy),
                    Chunk = chunk
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Tag)))
            .GroupBy(item => item.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TopicSummaryItemDto
            {
                Tag = group.First().Tag,
                ChunkCount = group.Select(item => item.Chunk.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                DocumentCount = group.Select(item => item.Chunk.DocumentId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                EstimatedTokens = group.Sum(item => item.Chunk.TokenCount > 0 ? item.Chunk.TokenCount : EstimateTokenCount(item.Chunk.RawText)),
                SampleSectionTitles = group.Select(item => item.Chunk.SectionTitle)
                    .Where(title => !string.IsNullOrWhiteSpace(title))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .Cast<string>()
                    .ToList(),
                SourceChapterKeys = group.Select(item => item.Chunk.ChapterKey)
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Cast<string>()
                    .ToList()
            })
            .Where(item => !IsLowValueTopicSummaryTag(item.Tag))
            .OrderByDescending(item => ComputeTopicSummarySpecificityScore(item.Tag))
            .ThenByDescending(item => item.ChunkCount)
            .ThenByDescending(item => item.EstimatedTokens)
            .ThenBy(item => item.Tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TopicSummaryResultDto
        {
            Scope = scope,
            CourseId = courseId,
            DocumentId = documentId,
            DocumentName = documentName,
            ChapterKey = chapterKey,
            ChapterKeys = chapterKeys?.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>(),
            ChapterTitle = chapterTitle,
            Subject = subject,
            TotalChunks = chunks.Count,
            DistinctTopicCount = topics.Count,
            Topics = topics
        };
    }

    private ChapterSummaryResultDto BuildChapterSummary(
        string scope,
        string? courseId,
        string documentId,
        string? documentName,
        string? subject,
        IReadOnlyList<KnowledgeChunk> chunks)
    {
        var taxonomy = _tagTaxonomyProvider.GetCatalog().ResolveBySubject(subject);
        var chapters = DocumentChapterSummaryBuilder.Build(chunks)
            .Select(item => MapChapterSummaryItem(item, taxonomy))
            .ToList();

        return new ChapterSummaryResultDto
        {
            Scope = scope,
            CourseId = courseId,
            DocumentId = documentId,
            DocumentName = documentName,
            Subject = subject,
            TotalChunks = chunks.Count,
            DistinctChapterCount = chapters.Count,
            Chapters = chapters
        };
    }

    private ChapterSummaryResultDto BuildChapterSummaryFromSnapshots(
        string scope,
        string? courseId,
        string documentId,
        string? documentName,
        string? subject,
        IReadOnlyList<DocumentChapterSummary> chapters)
    {
        var taxonomy = _tagTaxonomyProvider.GetCatalog().ResolveBySubject(subject);
        var mappedChapters = chapters
            .OrderBy(item => item.ChapterOrder ?? int.MaxValue)
            .ThenBy(item => item.ChapterTitle, StringComparer.OrdinalIgnoreCase)
            .Select(item => MapChapterSummaryItem(item, taxonomy))
            .ToList();

        return new ChapterSummaryResultDto
        {
            Scope = scope,
            CourseId = courseId,
            DocumentId = documentId,
            DocumentName = documentName,
            Subject = subject,
            TotalChunks = chapters.Sum(item => item.ChunkCount),
            DistinctChapterCount = mappedChapters.Count,
            Chapters = mappedChapters
        };
    }

    private static ChapterSummaryItemDto MapChapterSummaryItem(DocumentChapterSummary item, AITagTaxonomySubject? taxonomy)
    {
        var coveredTopics = item.CoveredTopics
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => CanonicalizeTag(tag, taxonomy))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ChapterSummaryItemDto
        {
            ChapterKey = item.ChapterKey,
            ChapterTitle = item.ChapterTitle,
            ChapterOrder = item.ChapterOrder,
            ChunkCount = item.ChunkCount,
            EstimatedTokens = item.EstimatedTokens,
            CoveredTopics = coveredTopics,
            SampleSectionTitles = item.SampleSectionTitles
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            OverviewShort = string.IsNullOrWhiteSpace(item.OverviewShort)
                ? DocumentChapterSummaryBuilder.BuildOverviewShort(item.ChapterTitle, coveredTopics, item.SampleSectionTitles)
                : item.OverviewShort
        };
    }

    private static string? ResolveRequestedChapterTitle(IReadOnlyList<KnowledgeChunk> chunks, string chapterKey)
        => chunks
            .Select(DocumentChapterSummaryBuilder.ResolveChapterTitle)
            .FirstOrDefault(title => !string.IsNullOrWhiteSpace(title))
           ?? chapterKey;

    private static string CanonicalizeTag(string tag, AITagTaxonomySubject? taxonomy)
    {
        var normalized = tag.Trim().ToLowerInvariant();
        if (taxonomy is not null && taxonomy.TryResolveCanonical(normalized, out var canonical) && !string.IsNullOrWhiteSpace(canonical))
        {
            return canonical!;
        }

        return normalized;
    }

    private static bool IsLowValueTopicSummaryTag(string tag)
    {
        var normalized = tag.Trim().ToLowerInvariant();
        return normalized is "general" or "topic" or "concept" or "study content" or "computer science" or "image summary";
    }

    private static int ComputeTopicSummarySpecificityScore(string tag)
    {
        var normalized = tag.Trim().ToLowerInvariant();
        if (normalized is "c" or "java" or "java_oop" or "dsa_java")
        {
            return -4;
        }

        if (normalized is "classes" or "methods" or "objects")
        {
            return -3;
        }

        if (normalized.Contains(' '))
        {
            return 3;
        }

        if (normalized.Contains('_'))
        {
            return 2;
        }

        return 1;
    }

}
