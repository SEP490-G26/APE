using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class DocumentService
{
    private readonly IDocumentRepository _repo;
    private readonly ICourseRepository _courseRepo;
    private readonly IUserRepository _userRepo;
    private readonly IFileExtractionService _extractor;
    private readonly IAIExtractedContentService _extractedContentService;
    private readonly IAIGatekeeperService _gatekeeper;
    private readonly IAIEmbeddingTaggingService _embeddingTaggingService;
    private readonly IKnowledgeChunkRepository _chunkRepo;
    private readonly IAIExtractionDraftRepository _draftRepo;
    private readonly IAIUsageLogRepository _aiUsageLogRepository;
    private readonly IAIVndBillingService _vndBillingService;
    private readonly ILogger<DocumentService> _logger;
    private readonly IFileStorageService _fileStorage;
    private const long MaxFileSizeBytes = 20 * 1024 * 1024;

    public DocumentService(
        IDocumentRepository repo,
        ICourseRepository courseRepo,
        IUserRepository userRepo,
        IFileExtractionService extractor,
        IAIExtractedContentService extractedContentService,
        IAIGatekeeperService gatekeeper,
        IAIEmbeddingTaggingService embeddingTaggingService,
        IKnowledgeChunkRepository chunkRepo,
        IAIExtractionDraftRepository draftRepo,
        IAIUsageLogRepository aiUsageLogRepository,
        IAIVndBillingService vndBillingService,
        IFileStorageService fileStorage,
        ILogger<DocumentService> logger)
    {
        _repo = repo;
        _courseRepo = courseRepo;
        _userRepo = userRepo;
        _extractor = extractor;
        _extractedContentService = extractedContentService;
        _gatekeeper = gatekeeper;
        _embeddingTaggingService = embeddingTaggingService;
        _chunkRepo = chunkRepo;
        _draftRepo = draftRepo;
        _aiUsageLogRepository = aiUsageLogRepository;
        _vndBillingService = vndBillingService;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public Task<Stream?> OpenFileReadAsync(string relativePath, CancellationToken cancellationToken = default)
        => _fileStorage.OpenReadAsync(relativePath, cancellationToken);

    public Task<bool> FileExistsAsync(string relativePath, CancellationToken cancellationToken = default)
        => _fileStorage.ExistsAsync(relativePath, cancellationToken);

    public async Task<DocumentDto> UploadAsync(
        Stream fileStream,
        string fileName,
        string userId,
        string courseId,
        bool isByos = false,
        AIRuntimeOverride? extractedContentRuntimeOverride = null,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stageTimings = new List<string>();

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("User identity is required");
        }

        if (string.IsNullOrWhiteSpace(courseId))
        {
            throw new ArgumentException("courseId is required");
        }

        var course = await _courseRepo.GetByIdAsync(courseId);
        if (course == null)
        {
            throw new KeyNotFoundException("Course not found");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not ".pdf" and not ".docx" and not ".txt" and not ".pptx")
        {
            throw new ArgumentException("Only PDF, DOCX, PPTX, and TXT files are supported");
        }

        if (fileStream.CanSeek && fileStream.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException("File size must not exceed 2 MB");
        }

        var stageStopwatch = Stopwatch.StartNew();
        await using var bufferedStream = await BufferInputAsync(fileStream, cancellationToken);
        stageTimings.Add($"buffer_input_ms={stageStopwatch.ElapsedMilliseconds}");
        if (bufferedStream.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException("File size must not exceed 2 MB");
        }

        if (isByos)
        {
            if (await _repo.CountByUserAsync(userId) >= 10)
            {
                throw new InvalidOperationException("Maximum 10 personal documents reached");
            }

            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
            {
                throw new UnauthorizedAccessException("User not found");
            }

            await _vndBillingService.EnsureMinimumBalanceAsync(userId, cancellationToken);

            _logger.LogInformation("BYOS upload requested by user {UserId}", userId);
        }

        var source = isByos ? "BYOS" : "SystemSyllabus";
        var fileChecksum = ComputeStableHash(bufferedStream.ToArray());
        var scopeCourseId = isByos ? null : courseId;
        var scopeUserId = isByos ? userId : null;
        stageStopwatch.Restart();
        var duplicateByFile = await _repo.FindReusableByFileChecksumAsync(source, scopeCourseId, scopeUserId, fileChecksum);
        stageTimings.Add($"duplicate_file_lookup_ms={stageStopwatch.ElapsedMilliseconds}");
        if (duplicateByFile is not null)
        {
            _logger.LogInformation(
                "Skipped duplicate upload by file checksum. Source={Source}, CourseId={CourseId}, UserId={UserId}, ExistingDocumentId={DocumentId}",
                source,
                courseId,
                userId,
                duplicateByFile.Id);
            return await BuildDocumentDtoAsync(
                duplicateByFile,
                duplicateDetected: true,
                duplicateMatchType: "file_checksum",
                duplicateOfDocumentId: duplicateByFile.Id);
        }

        stageStopwatch.Restart();
        ResetStreamPosition(bufferedStream);
        var precheckExtraction = await _extractor.ExtractPreviewTextAsync(bufferedStream, fileName);
        stageTimings.Add($"preview_extraction_ms={stageStopwatch.ElapsedMilliseconds}");
        var precheckContent = precheckExtraction.RawText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(precheckContent))
        {
            throw new InvalidOperationException("Document content is empty after precheck extraction.");
        }

        stageStopwatch.Restart();
        var gate = await _gatekeeper.ValidateAsync(precheckContent, new GatekeeperRequestContext
        {
            FileName = fileName,
            SubjectHint = AISubjectDomainMapper.ResolveFromCourseCodeOrFallback(course.Code, course.Code),
            Language = "en"
        });
        stageTimings.Add($"gatekeeper_ms={stageStopwatch.ElapsedMilliseconds}");

        if (!gate.IsSupported)
        {
            throw new InvalidOperationException($"Document content is not supported by AI gatekeeper. Verdict: {gate.Verdict}. Reason: {gate.Reason}");
        }

        var expectedSubject = AISubjectDomainMapper.ResolveFromCourseCodeOrFallback(course.Code, course.Code);
        if (!string.IsNullOrWhiteSpace(expectedSubject) &&
            !string.Equals(gate.PrimaryDomain, expectedSubject, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Document content does not match the selected course. " +
                $"Selected course {course.Code} expects subject '{expectedSubject}', " +
                $"but gatekeeper detected '{gate.PrimaryDomain}'.");
        }

        stageStopwatch.Restart();
        ResetStreamPosition(bufferedStream);
        var extraction = await _extractor.ExtractTextAsync(bufferedStream, fileName);
        stageTimings.Add($"full_extraction_ms={stageStopwatch.ElapsedMilliseconds}");

        stageStopwatch.Restart();
        var normalized = await _extractedContentService.NormalizeAsync(extraction, extractedContentRuntimeOverride, cancellationToken);
        stageTimings.Add($"normalize_and_structure_ms={stageStopwatch.ElapsedMilliseconds}");
        var normalizedContent = normalized.Content?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            throw new InvalidOperationException("Document content is empty after extraction and normalization");
        }

        var normalizedChecksum = ComputeStableHash(normalized.NormalizedMarkdown);
        stageStopwatch.Restart();
        var duplicateByNormalized = await _repo.FindReusableByNormalizedChecksumAsync(source, scopeCourseId, scopeUserId, normalizedChecksum);
        stageTimings.Add($"duplicate_normalized_lookup_ms={stageStopwatch.ElapsedMilliseconds}");
        if (duplicateByNormalized is not null)
        {
            _logger.LogInformation(
                "Skipped duplicate upload by normalized checksum. Source={Source}, CourseId={CourseId}, UserId={UserId}, ExistingDocumentId={DocumentId}",
                source,
                courseId,
                userId,
                duplicateByNormalized.Id);
            return await BuildDocumentDtoAsync(
                duplicateByNormalized,
                duplicateDetected: true,
                duplicateMatchType: "normalized_content_checksum",
                duplicateOfDocumentId: duplicateByNormalized.Id);
        }

        stageStopwatch.Restart();
        ResetStreamPosition(bufferedStream);
        var relativePath = await _fileStorage.SaveAsync(bufferedStream, fileName);
        stageTimings.Add($"file_save_ms={stageStopwatch.ElapsedMilliseconds}");
        var now = DateTime.UtcNow;
        var doc = new Document
        {
            CourseId = courseId,
            UserId = userId,
            FileName = fileName,
            FilePath = relativePath,
            FileType = extension,
            FileSizeBytes = bufferedStream.Length,
            FileChecksum = fileChecksum,
            NormalizedContentChecksum = normalizedChecksum,
            Source = source,
            SubjectCode = gate.PrimaryDomain,
            GatekeeperVerdict = gate.Verdict,
            Status = DocumentStatus.Processing,
            IsActive = false,
            CreatedAt = now,
            LastExtractedAt = now
        };
        stageStopwatch.Restart();
        await _repo.CreateAsync(doc);
        stageTimings.Add($"document_create_ms={stageStopwatch.ElapsedMilliseconds}");

        var draft = BuildExtractionDraft(doc, relativePath, extraction, normalized, gate, isByos);
        draft.TotalSegments = CountLogicalSegments(normalized.NormalizedMarkdown);
        stageStopwatch.Restart();
        await _draftRepo.CreateAsync(draft);
        stageTimings.Add($"draft_create_ms={stageStopwatch.ElapsedMilliseconds}");
        doc.LastExtractionDraftId = draft.Id;

        stageStopwatch.Restart();
        var run = await _embeddingTaggingService.CreateChunksAsync(
            doc.Id,
            doc.CourseId,
            doc.UserId,
            normalized.NormalizedMarkdown,
            normalized.SourceType,
            doc.SubjectCode,
            new ExtractionStructuralHintsDto
            {
                CandidateTitles = normalized.CandidateTitles,
                CandidateChapterMarkers = normalized.CandidateChapterMarkers,
                RejectedHeadingCandidates = normalized.RejectedHeadingCandidates,
                CleanDisplayTitleCandidates = normalized.CleanDisplayTitleCandidates,
                StructuralUnits = normalized.StructuralUnits
            },
            cancellationToken: cancellationToken);
        stageTimings.Add($"chunk_embed_tag_ms={stageStopwatch.ElapsedMilliseconds}");

        var chunks = run.Chunks.ToList();
        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("No knowledge chunks were created from the extracted document.");
        }

        stageStopwatch.Restart();
        await _chunkRepo.ReplaceForDocumentAsync(doc.Id, chunks);
        stageTimings.Add($"chunk_replace_ms={stageStopwatch.ElapsedMilliseconds}");

        var chapterSummaries = DocumentChapterSummaryBuilder.Build(chunks);

        var embeddedAt = DateTime.UtcNow;
        draft.ChunkCount = chunks.Count;
        draft.ChunkingReady = true;
        draft.LastEmbeddingRunId = $"embed-{embeddedAt:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];
        draft.ChapterSummaries = DocumentChapterSummaryBuilder.Clone(chapterSummaries);
        draft.ReviewStatus = "auto_ingested";
        draft.ApprovedMarkdownPreview = draft.CleanMarkdownPreview;
        draft.ApprovedSegments = draft.TotalSegments;
        draft.RejectedSegments = 0;
        draft.UpdatedAt = embeddedAt;
        stageStopwatch.Restart();
        await _draftRepo.UpdateAsync(draft);
        stageTimings.Add($"draft_update_ms={stageStopwatch.ElapsedMilliseconds}");

        doc.LastEmbeddedAt = embeddedAt;
        doc.ChapterSummaries = chapterSummaries;
        doc.Status = DocumentStatus.Completed;
        doc.IsActive = true;
        stageStopwatch.Restart();
        await _repo.UpdateAsync(doc);
        stageTimings.Add($"document_update_ms={stageStopwatch.ElapsedMilliseconds}");

        var totalReportedCostUsd =
            (gate.CostUsd ?? 0m) +
            (normalized.CostUsd ?? 0m) +
            (run.EmbeddingUsage?.CostUsd ?? 0m) +
            (run.TaggingUsage?.CostUsd ?? 0m);
        var usageLogs = BuildIngestionUsageLogs(doc, draft, gate, normalized, run, embeddedAt);
        stageStopwatch.Restart();
        var charge = isByos
            ? await _vndBillingService.ChargeAsync(
                new AIVndChargeRequest
                {
                    UserId = userId,
                    FeatureKey = AICreditFeatureKeys.ByosIngest,
                    FeatureName = "BYOS ingestion",
                    SourceEntityType = "Document",
                    SourceEntityId = doc.Id,
                    CreatedBy = userId,
                    ReportedCostUsd = totalReportedCostUsd,
                    UsageLogIds = usageLogs.Select(item => item.Id).ToList(),
                    UsageSnapshot = new Dictionary<string, object?>
                    {
                        ["gatekeeper_cost_usd"] = gate.CostUsd ?? 0m,
                        ["embedding_cost_usd"] = run.EmbeddingUsage?.CostUsd ?? 0m,
                        ["extracted_content_cost_usd"] = normalized.CostUsd ?? 0m,
                        ["tagging_cost_usd"] = run.TaggingUsage?.CostUsd ?? 0m,
                        ["gatekeeper_usage_source"] = gate.UsageSource,
                        ["gatekeeper_cost_source"] = gate.CostSource,
                        ["embedding_usage_source"] = run.EmbeddingUsage?.UsageSource,
                        ["embedding_cost_source"] = run.EmbeddingUsage?.CostSource,
                        ["extracted_content_usage_source"] = normalized.UsageSource,
                        ["extracted_content_cost_source"] = normalized.CostSource,
                        ["tagging_usage_source"] = run.TaggingUsage?.UsageSource,
                        ["tagging_cost_source"] = run.TaggingUsage?.CostSource,
                        ["chunk_count"] = chunks.Count
                    }
                },
                cancellationToken)
            : await _vndBillingService.RecordUsageAsync(
                new AIVndChargeRequest
                {
                    UserId = userId,
                    FeatureKey = "system_ingest",
                    FeatureName = "System ingestion",
                    SourceEntityType = "Document",
                    SourceEntityId = doc.Id,
                    CreatedBy = userId,
                    ReportedCostUsd = totalReportedCostUsd,
                    UsageLogIds = usageLogs.Select(item => item.Id).ToList(),
                    UsageSnapshot = new Dictionary<string, object?>
                    {
                        ["gatekeeper_cost_usd"] = gate.CostUsd ?? 0m,
                        ["embedding_cost_usd"] = run.EmbeddingUsage?.CostUsd ?? 0m,
                        ["extracted_content_cost_usd"] = normalized.CostUsd ?? 0m,
                        ["tagging_cost_usd"] = run.TaggingUsage?.CostUsd ?? 0m,
                        ["gatekeeper_usage_source"] = gate.UsageSource,
                        ["gatekeeper_cost_source"] = gate.CostSource,
                        ["embedding_usage_source"] = run.EmbeddingUsage?.UsageSource,
                        ["embedding_cost_source"] = run.EmbeddingUsage?.CostSource,
                        ["extracted_content_usage_source"] = normalized.UsageSource,
                        ["extracted_content_cost_source"] = normalized.CostSource,
                        ["tagging_usage_source"] = run.TaggingUsage?.UsageSource,
                        ["tagging_cost_source"] = run.TaggingUsage?.CostSource,
                        ["chunk_count"] = chunks.Count,
                        ["billing_mode"] = "actual_usage_only"
                    }
                },
                cancellationToken);
        stageTimings.Add($"billing_ms={stageStopwatch.ElapsedMilliseconds}");

        try
        {
            stageStopwatch.Restart();
            draft.CreditChargeStatus = isByos ? (charge.ActualDeductedVnd > 0 || charge.ChargedVnd > 0 ? "charged" : "waived") : "waived";
            draft.CreditChargeAmount = charge.ActualDeductedVnd;
            draft.UpdatedAt = DateTime.UtcNow;
            await _draftRepo.UpdateAsync(draft);

            await LogEmbeddingUsageAsync(usageLogs);
            stageTimings.Add($"usage_finalize_ms={stageStopwatch.ElapsedMilliseconds}");

            totalStopwatch.Stop();
            _logger.LogInformation(
                "Document upload pipeline completed. DocumentId={DocumentId}, FileName={FileName}, Source={Source}, ChunkCount={ChunkCount}, TotalMs={TotalMs}, StageTimings={StageTimings}",
                doc.Id,
                fileName,
                source,
                chunks.Count,
                totalStopwatch.ElapsedMilliseconds,
                string.Join(", ", stageTimings));

            return await BuildDocumentDtoAsync(
                doc,
                duplicateDetected: false,
                duplicateMatchType: null,
                duplicateOfDocumentId: null,
                usdToVndRate: charge.UsdToVndRate,
                actualCostVnd: charge.ActualCostVnd,
                chargedVnd: charge.ChargedVnd,
                actualDeductedVnd: charge.ActualDeductedVnd,
                absorbedVnd: charge.AbsorbedVnd,
                remainingBalanceVnd: charge.BalanceAfterVnd,
                knownChunkCount: chunks.Count);
        }
        catch
        {
            if (isByos && charge.ActualDeductedVnd > 0 && !string.IsNullOrWhiteSpace(charge.TransactionId))
            {
                try
                {
                    var refund = await _vndBillingService.RefundAsync(
                        new AIVndRefundRequest
                        {
                            TransactionId = charge.TransactionId,
                            RefundedBy = userId,
                            RefundReason = "Technical failure after BYOS charge while finalizing ingest state."
                        },
                        cancellationToken);
                    draft.CreditChargeStatus = "refunded";
                    draft.CreditChargeAmount = refund.RefundedVnd;
                    draft.UpdatedAt = DateTime.UtcNow;
                    await _draftRepo.UpdateAsync(draft);
                }
                catch (Exception refundEx)
                {
                    _logger.LogError(refundEx, "Failed to auto-refund BYOS ingestion charge for document {DocumentId}.", doc.Id);
                }
            }

            throw;
        }
    }

    public Task<Document?> GetByIdAsync(string id)
        => _repo.GetByIdAsync(id);

    public async Task DeleteByStudentAsync(string documentId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("User identity is required");
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("documentId is required");
        }

        var doc = await _repo.GetByIdAsync(documentId);
        if (doc == null || doc.Status == DocumentStatus.Deleted)
        {
            throw new KeyNotFoundException("Document not found");
        }

        if (!string.Equals(doc.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this document");
        }

        if (!string.Equals(doc.Source, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only personal BYOS documents can be deleted by students");
        }

        if (!string.IsNullOrWhiteSpace(doc.FilePath))
        {
            try
            {
                await _fileStorage.DeleteAsync(doc.FilePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete document file from storage. DocumentId={DocumentId}, FilePath={FilePath}", doc.Id, doc.FilePath);
                throw new InvalidOperationException("Could not delete the document file from storage.");
            }
        }

        await _chunkRepo.DeleteByDocumentIdAsync(doc.Id);
        await _draftRepo.DeleteByDocumentIdAsync(doc.Id);

        doc.Status = DocumentStatus.Deleted;
        doc.IsActive = false;
        doc.DeletedAt = DateTime.UtcNow;
        doc.DeletedBy = userId;
        doc.LastEmbeddedAt = null;
        doc.LastExtractedAt = null;
        doc.LastExtractionDraftId = null;
        doc.ChapterSummaries = new List<DocumentChapterSummary>();

        await _repo.UpdateAsync(doc);
    }

    public async Task DeleteByAdminAsync(string documentId, string adminId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adminId))
        {
            throw new UnauthorizedAccessException("Admin identity is required");
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("documentId is required");
        }

        var doc = await _repo.GetByIdAsync(documentId);
        if (doc == null || doc.Status == DocumentStatus.Deleted)
        {
            throw new KeyNotFoundException("Document not found");
        }

        if (!string.Equals(doc.Source, "SystemSyllabus", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only system documents can be deleted from the admin library");
        }

        if (!string.IsNullOrWhiteSpace(doc.FilePath))
        {
            try
            {
                await _fileStorage.DeleteAsync(doc.FilePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete admin document file from storage. DocumentId={DocumentId}, FilePath={FilePath}", doc.Id, doc.FilePath);
                throw new InvalidOperationException("Could not delete the document file from storage.");
            }
        }

        await _chunkRepo.DeleteByDocumentIdAsync(doc.Id);
        await _draftRepo.DeleteByDocumentIdAsync(doc.Id);

        doc.Status = DocumentStatus.Deleted;
        doc.IsActive = false;
        doc.DeletedAt = DateTime.UtcNow;
        doc.DeletedBy = adminId;
        doc.LastEmbeddedAt = null;
        doc.LastExtractedAt = null;
        doc.LastExtractionDraftId = null;
        doc.ChapterSummaries = new List<DocumentChapterSummary>();

        await _repo.UpdateAsync(doc);
    }

    public async Task<DocumentPreviewResultDto?> GetPreviewAsync(string id, string userId, bool isAdmin = false)
    {
        var doc = await _repo.GetByIdAsync(id);
        if (doc == null)
        {
            return null;
        }

        if (doc.Status == DocumentStatus.Deleted)
        {
            return null;
        }

        if (!isAdmin && !string.Equals(doc.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You do not have permission to view this document preview");
        }

        var chunks = await _chunkRepo.GetByDocumentIdAsync(id);
        if (chunks.Count > 0)
        {
            var previewText = string.Join(Environment.NewLine + Environment.NewLine, chunks.Select(chunk => chunk.ContentText).Take(3));
            var tags = chunks.SelectMany(chunk => chunk.TopicTags ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var stage = ResolveIngestionStage(doc, hasDraft: !string.IsNullOrWhiteSpace(doc.LastExtractionDraftId), hasEmbeddedChunks: true);
            return new DocumentPreviewResultDto
            {
                DocumentId = doc.Id,
                FileName = doc.FileName,
                FileType = doc.FileType,
                FileSizeBytes = await ResolveDocumentFileSizeAsync(doc, null),
                Source = doc.Source,
                SubjectCode = doc.SubjectCode,
                GatekeeperVerdict = doc.GatekeeperVerdict,
                IngestionStatus = doc.Status.ToString(),
                IngestionStage = stage,
                LastExtractionDraftId = doc.LastExtractionDraftId,
                PreviewText = previewText,
                Tags = tags,
                ChunkCount = chunks.Count,
                HasExtractionDraft = !string.IsNullOrWhiteSpace(doc.LastExtractionDraftId),
                HasEmbeddedChunks = true,
                IsReadyForGeneration = true,
                CreatedAt = doc.CreatedAt,
                LastExtractedAt = doc.LastExtractedAt,
                LastEmbeddedAt = doc.LastEmbeddedAt,
                CleanupWarnings = new List<string>(),
                Chapters = MapChapterSummaryDtos(doc.ChapterSummaries)
            };
        }

        if (string.IsNullOrWhiteSpace(doc.LastExtractionDraftId))
        {
            return null;
        }

        var draft = await _draftRepo.GetByIdAsync(doc.LastExtractionDraftId);
        if (draft == null)
        {
            return null;
        }

        var preview = draft.CleanMarkdownPreview ?? draft.RawTextPreview ?? string.Empty;
        var draftReady = draft.ChunkingReady && draft.ChunkCount > 0;
        var stageFromDraft = ResolveIngestionStage(doc, hasDraft: true, hasEmbeddedChunks: draftReady);
        return new DocumentPreviewResultDto
        {
            DocumentId = doc.Id,
            FileName = doc.FileName,
            FileType = doc.FileType,
            FileSizeBytes = await ResolveDocumentFileSizeAsync(doc, draft),
            Source = doc.Source,
            SubjectCode = doc.SubjectCode,
            GatekeeperVerdict = doc.GatekeeperVerdict,
            IngestionStatus = doc.Status.ToString(),
            IngestionStage = stageFromDraft,
            LastExtractionDraftId = doc.LastExtractionDraftId,
            PreviewText = preview,
            Tags = new List<string>(),
            ChunkCount = draft.ChunkCount,
            HasExtractionDraft = true,
            HasEmbeddedChunks = draftReady,
            IsReadyForGeneration = draftReady && doc.Status == DocumentStatus.Completed,
            CreatedAt = doc.CreatedAt,
            LastExtractedAt = doc.LastExtractedAt,
            LastEmbeddedAt = doc.LastEmbeddedAt,
            CleanupWarnings = draft.CleanupWarnings ?? new List<string>(),
            Chapters = MapChapterSummaryDtos(doc.ChapterSummaries.Count > 0 ? doc.ChapterSummaries : draft.ChapterSummaries)
        };
    }

    private static List<DocumentChapterSummaryDto> MapChapterSummaryDtos(IEnumerable<DocumentChapterSummary>? chapters)
    {
        if (chapters is null)
        {
            return new List<DocumentChapterSummaryDto>();
        }

        return chapters
            .Where(chapter => !string.IsNullOrWhiteSpace(chapter.ChapterKey) || !string.IsNullOrWhiteSpace(chapter.ChapterTitle))
            .Select(chapter => new DocumentChapterSummaryDto
            {
                ChapterKey = chapter.ChapterKey,
                ChapterTitle = chapter.ChapterTitle,
                ChapterOrder = chapter.ChapterOrder,
                ChunkCount = chapter.ChunkCount,
                EstimatedTokens = chapter.EstimatedTokens,
                CoveredTopics = chapter.CoveredTopics ?? new List<string>(),
                SampleSectionTitles = chapter.SampleSectionTitles ?? new List<string>(),
                OverviewShort = chapter.OverviewShort
            })
            .ToList();
    }

    public async Task<PaginatedResult<DocumentListItemDto>> ListByUserAsync(string userId, int page = 1, int limit = 20)
    {
        page = page <= 0 ? 1 : page;
        limit = limit <= 0 ? 20 : limit;

        var (items, total) = await _repo.ListByUserAsync(userId, page, limit);
        var result = new List<DocumentListItemDto>(items.Count);

        foreach (var doc in items)
        {
            var chunks = await _chunkRepo.GetByDocumentIdAsync(doc.Id);
            var draft = string.IsNullOrWhiteSpace(doc.LastExtractionDraftId)
                ? null
                : await _draftRepo.GetByIdAsync(doc.LastExtractionDraftId);
            var hasEmbeddedChunks = chunks.Count > 0 || (draft?.ChunkingReady == true && draft.ChunkCount > 0);
            var tagCount = chunks.SelectMany(chunk => chunk.TopicTags ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            result.Add(new DocumentListItemDto
            {
                Id = doc.Id,
                CourseId = doc.CourseId,
                FileName = doc.FileName,
                FileType = doc.FileType,
                FileSizeBytes = await ResolveDocumentFileSizeAsync(doc, draft),
                Source = doc.Source,
                SubjectCode = doc.SubjectCode,
                GatekeeperVerdict = doc.GatekeeperVerdict,
                IngestionStatus = doc.Status.ToString(),
                IngestionStage = ResolveIngestionStage(doc, draft is not null, hasEmbeddedChunks),
                IsActive = doc.IsActive,
                HasExtractionDraft = draft is not null,
                HasEmbeddedChunks = hasEmbeddedChunks,
                IsReadyForGeneration = doc.Status == DocumentStatus.Completed && hasEmbeddedChunks,
                HasTopics = tagCount > 0,
                ChunkCount = chunks.Count > 0 ? chunks.Count : draft?.ChunkCount ?? 0,
                TagCount = tagCount,
                CreatedAt = doc.CreatedAt,
                LastExtractedAt = doc.LastExtractedAt,
                LastEmbeddedAt = doc.LastEmbeddedAt
            });
        }

        return new PaginatedResult<DocumentListItemDto>
        {
            Items = result,
            Total = total,
            Page = page,
            Limit = limit,
            TotalPages = (total + limit - 1) / limit
        };
    }

    public async Task<PaginatedResult<DocumentListItemDto>> ListSystemAsync(int page = 1, int limit = 20, string? courseId = null)
    {
        page = page <= 0 ? 1 : page;
        limit = limit <= 0 ? 20 : limit;

        var (items, total) = await _repo.ListSystemAsync(page, limit, courseId);
        var result = new List<DocumentListItemDto>(items.Count);

        foreach (var doc in items)
        {
            var chunks = await _chunkRepo.GetByDocumentIdAsync(doc.Id);
            var draft = string.IsNullOrWhiteSpace(doc.LastExtractionDraftId)
                ? null
                : await _draftRepo.GetByIdAsync(doc.LastExtractionDraftId);
            var hasEmbeddedChunks = chunks.Count > 0 || (draft?.ChunkingReady == true && draft.ChunkCount > 0);
            var tagCount = chunks.SelectMany(chunk => chunk.TopicTags ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            result.Add(new DocumentListItemDto
            {
                Id = doc.Id,
                CourseId = doc.CourseId,
                FileName = doc.FileName,
                FileType = doc.FileType,
                FileSizeBytes = await ResolveDocumentFileSizeAsync(doc, draft),
                Source = doc.Source,
                SubjectCode = doc.SubjectCode,
                GatekeeperVerdict = doc.GatekeeperVerdict,
                IngestionStatus = doc.Status.ToString(),
                IngestionStage = ResolveIngestionStage(doc, draft is not null, hasEmbeddedChunks),
                IsActive = doc.IsActive,
                HasExtractionDraft = draft is not null,
                HasEmbeddedChunks = hasEmbeddedChunks,
                IsReadyForGeneration = doc.Status == DocumentStatus.Completed && hasEmbeddedChunks,
                HasTopics = tagCount > 0,
                ChunkCount = chunks.Count > 0 ? chunks.Count : draft?.ChunkCount ?? 0,
                TagCount = tagCount,
                CreatedAt = doc.CreatedAt,
                LastExtractedAt = doc.LastExtractedAt,
                LastEmbeddedAt = doc.LastEmbeddedAt
            });
        }

        return new PaginatedResult<DocumentListItemDto>
        {
            Items = result,
            Total = total,
            Page = page,
            Limit = limit,
            TotalPages = (total + limit - 1) / limit
        };
    }

    public async Task EditContentAsync(string id, string newContent, string adminId)
    {
        var chunk = await _chunkRepo.GetFirstByDocumentIdAsync(id);
        if (chunk != null)
        {
            await _chunkRepo.UpdateContentAsync(chunk.Id, newContent);
            return;
        }

        var draft = await _draftRepo.GetLatestByDocumentIdAsync(id);
        if (draft == null)
        {
            throw new KeyNotFoundException("Document content not found");
        }
        draft.CleanMarkdownPreview = BuildPreview(newContent);
        draft.ApprovedMarkdownPreview = BuildPreview(newContent);
        draft.ReviewStatus = "manually_adjusted";
        draft.ReviewedBy = adminId;
        draft.ReviewedAt = DateTime.UtcNow;
        draft.ApprovalVersion += 1;
        draft.UpdatedAt = DateTime.UtcNow;
        await _draftRepo.UpdateAsync(draft);
    }

    public async Task ToggleActiveAsync(string documentId)
    {
        var doc = await _repo.GetByIdAsync(documentId);
        if (doc == null || doc.Status == DocumentStatus.Deleted)
        {
            throw new KeyNotFoundException("Document not found");
        }

        doc.IsActive = !doc.IsActive;
        await _repo.UpdateAsync(doc);
    }

    public async Task<ExtractionDraftDto?> GetExtractionDraftAsync(string documentId, string requesterId, bool isAdmin = false)
    {
        var doc = await _repo.GetByIdAsync(documentId);
        if (doc == null || doc.Status == DocumentStatus.Deleted)
        {
            return null;
        }

        if (!isAdmin && !string.Equals(doc.UserId, requesterId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You do not have permission to view this extraction draft.");
        }

        if (string.IsNullOrWhiteSpace(doc.LastExtractionDraftId))
        {
            return null;
        }

        var draft = await _draftRepo.GetByIdAsync(doc.LastExtractionDraftId);
        if (draft == null)
        {
            return null;
        }

        return MapDraftDto(draft);
    }

    private List<AIUsageLog> BuildIngestionUsageLogs(
        Document doc,
        AIExtractionDraft draft,
        GatekeeperResult gatekeeper,
        ExtractedContentResultDto normalized,
        EmbeddingTaggingRunResult run,
        DateTime now)
    {
        var logs = new List<AIUsageLog>();

        if (!string.IsNullOrWhiteSpace(gatekeeper.Provider) || gatekeeper.TotalTokens.HasValue || gatekeeper.CostUsd.HasValue)
        {
            logs.Add(new AIUsageLog
            {
                TriggeredBy = doc.UserId,
                AgentId = AIAgentCatalog.GatekeeperAgentId,
                CreditsDeducted = 0d,
                TokensUsed = gatekeeper.TotalTokens ?? 0,
                CostUsd = gatekeeper.CostUsd ?? 0m,
                CreatedAt = now,
                PayloadData = new Dictionary<string, object?>
                {
                    ["feature"] = "DocumentIngestion",
                    ["step"] = "gatekeeper",
                    ["document_id"] = doc.Id,
                    ["draft_id"] = draft.Id,
                    ["course_id"] = doc.CourseId,
                    ["provider"] = gatekeeper.Provider,
                    ["model"] = gatekeeper.EffectiveModel ?? gatekeeper.Model,
                    ["configured_model"] = gatekeeper.ConfiguredModel,
                    ["effective_model"] = gatekeeper.EffectiveModel ?? gatekeeper.Model,
                    ["usage_source"] = gatekeeper.UsageSource,
                    ["cost_source"] = gatekeeper.CostSource,
                    ["fallback_used"] = gatekeeper.FallbackUsed,
                    ["fallback_from_provider"] = gatekeeper.FallbackFromProvider,
                    ["fallback_from_model"] = gatekeeper.FallbackFromModel,
                    ["fallback_reason_code"] = gatekeeper.FallbackReasonCode,
                    ["input_tokens"] = gatekeeper.InputTokens,
                    ["output_tokens"] = gatekeeper.OutputTokens,
                    ["total_tokens"] = gatekeeper.TotalTokens,
                    ["verdict"] = gatekeeper.Verdict,
                    ["primary_domain"] = gatekeeper.PrimaryDomain,
                    ["credit_charge_applied"] = 0d
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(normalized.Provider) || normalized.TotalTokens.HasValue || normalized.CostUsd.HasValue)
        {
            logs.Add(new AIUsageLog
            {
                TriggeredBy = doc.UserId,
                AgentId = AIAgentCatalog.ExtractionAgentId,
                CreditsDeducted = 0d,
                TokensUsed = normalized.TotalTokens ?? 0,
                CostUsd = normalized.CostUsd ?? 0m,
                CreatedAt = now,
                PayloadData = new Dictionary<string, object?>
                {
                    ["feature"] = "DocumentIngestion",
                    ["step"] = "extracted_content",
                    ["document_id"] = doc.Id,
                    ["draft_id"] = draft.Id,
                    ["course_id"] = doc.CourseId,
                    ["provider"] = normalized.Provider,
                    ["model"] = normalized.EffectiveModel ?? normalized.Model,
                    ["configured_model"] = normalized.ConfiguredModel,
                    ["effective_model"] = normalized.EffectiveModel ?? normalized.Model,
                    ["usage_source"] = normalized.UsageSource,
                    ["cost_source"] = normalized.CostSource,
                    ["fallback_used"] = normalized.FallbackUsed,
                    ["fallback_from_provider"] = normalized.FallbackFromProvider,
                    ["fallback_from_model"] = normalized.FallbackFromModel,
                    ["fallback_reason_code"] = normalized.FallbackReasonCode,
                    ["input_tokens"] = normalized.InputTokens,
                    ["output_tokens"] = normalized.OutputTokens,
                    ["total_tokens"] = normalized.TotalTokens,
                    ["used_ai_normalization"] = normalized.UsedAiNormalization,
                    ["used_ai_structure_detection"] = normalized.UsedAiStructureDetection,
                    ["extraction_mode"] = normalized.ExtractionMode,
                    ["vision_attempted_count"] = normalized.VisionEnrichmentAttemptedCount,
                    ["vision_succeeded_count"] = normalized.VisionEnrichmentSucceededCount,
                    ["vision_failed_count"] = normalized.VisionEnrichmentFailedCount,
                    ["credit_charge_applied"] = 0d
                }
            });
        }

        if (run.EmbeddingUsage is not null)
        {
            logs.Add(new AIUsageLog
            {
                TriggeredBy = doc.UserId,
                AgentId = AIAgentCatalog.EmbeddingAgentId,
                CreditsDeducted = 0d,
                TokensUsed = run.EmbeddingUsage.TokensUsed,
                CostUsd = run.EmbeddingUsage.CostUsd,
                CreatedAt = now,
                PayloadData = new Dictionary<string, object?>
                {
                    ["feature"] = "EmbeddingTagging",
                    ["step"] = "embedding",
                    ["document_id"] = doc.Id,
                    ["draft_id"] = draft.Id,
                    ["course_id"] = doc.CourseId,
                    ["provider"] = run.EmbeddingUsage.Provider,
                    ["model"] = run.EmbeddingUsage.EffectiveModel,
                    ["configured_model"] = run.EmbeddingUsage.ConfiguredModel,
                    ["effective_model"] = run.EmbeddingUsage.EffectiveModel,
                    ["model_family"] = run.EmbeddingUsage.ModelFamily,
                    ["normalized_model_key"] = run.EmbeddingUsage.NormalizedModelKey,
                    ["usage_source"] = run.EmbeddingUsage.UsageSource,
                    ["cost_source"] = run.EmbeddingUsage.CostSource,
                    ["fallback_used"] = run.EmbeddingUsage.FallbackUsed,
                    ["fallback_from_provider"] = run.EmbeddingUsage.FallbackFromProvider,
                    ["fallback_from_model"] = run.EmbeddingUsage.FallbackFromModel,
                    ["fallback_reason_code"] = run.EmbeddingUsage.FallbackReasonCode,
                    ["tokens_used"] = run.EmbeddingUsage.TokensUsed,
                    ["vector_count"] = run.EmbeddingUsage.VectorCount,
                    ["embedding_dimension"] = run.EmbeddingUsage.EmbeddingDimension,
                    ["ingestion_mode"] = "automatic_after_gatekeeper",
                    ["credit_charge_applied"] = 0d
                }
            });
        }

        if (run.TaggingUsage is not null)
        {
            logs.Add(new AIUsageLog
            {
                TriggeredBy = doc.UserId,
                AgentId = AIAgentCatalog.TaggingAgentId,
                CreditsDeducted = 0d,
                TokensUsed = run.TaggingUsage.TotalTokens,
                CostUsd = run.TaggingUsage.CostUsd,
                CreatedAt = now,
                PayloadData = new Dictionary<string, object?>
                {
                    ["feature"] = "EmbeddingTagging",
                    ["step"] = "auto_tagging",
                    ["document_id"] = doc.Id,
                    ["draft_id"] = draft.Id,
                    ["course_id"] = doc.CourseId,
                    ["provider"] = run.TaggingUsage.Provider,
                    ["model"] = run.TaggingUsage.EffectiveModel,
                    ["configured_model"] = run.TaggingUsage.ConfiguredModel,
                    ["effective_model"] = run.TaggingUsage.EffectiveModel,
                    ["model_family"] = run.TaggingUsage.ModelFamily,
                    ["normalized_model_key"] = run.TaggingUsage.NormalizedModelKey,
                    ["usage_source"] = run.TaggingUsage.UsageSource,
                    ["cost_source"] = run.TaggingUsage.CostSource,
                    ["fallback_used"] = run.TaggingUsage.FallbackUsed,
                    ["fallback_from_provider"] = run.TaggingUsage.FallbackFromProvider,
                    ["fallback_from_model"] = run.TaggingUsage.FallbackFromModel,
                    ["fallback_reason_code"] = run.TaggingUsage.FallbackReasonCode,
                    ["input_tokens"] = run.TaggingUsage.InputTokens,
                    ["output_tokens"] = run.TaggingUsage.OutputTokens,
                    ["total_tokens"] = run.TaggingUsage.TotalTokens,
                    ["call_count"] = run.TaggingUsage.CallCount,
                    ["successful_calls"] = run.TaggingUsage.SuccessfulCalls,
                    ["fallback_calls"] = run.TaggingUsage.FallbackCalls,
                    ["ingestion_mode"] = "automatic_after_gatekeeper",
                    ["credit_charge_applied"] = 0d
                }
            });
        }

        return logs;
    }

    private async Task LogEmbeddingUsageAsync(IReadOnlyCollection<AIUsageLog> logs)
    {
        foreach (var log in logs)
        {
            await _aiUsageLogRepository.CreateAsync(log);
        }
    }

    private static AIExtractionDraft BuildExtractionDraft(
        Document document,
        string relativePath,
        ExtractionResultDto extraction,
        ExtractedContentResultDto normalized,
        GatekeeperResult gatekeeper,
        bool isByos)
    {
        var inputTokens = EstimateTokenCount(extraction.RawText);
        var outputTokens = EstimateTokenCount(normalized.NormalizedMarkdown);
        return new AIExtractionDraft
        {
            DocumentId = document.Id,
            UserId = document.UserId,
            CourseId = document.CourseId,
            SourceType = normalized.SourceType,
            SourceName = document.FileName,
            SourceStoragePath = relativePath,
            SourceSizeBytes = Math.Max(1, extraction.RawText.Length),
            SourceChecksum = ComputeStableHash(extraction.RawText),
            Language = normalized.NormalizedMarkdown.Any(ch => ch > 127) ? "vi" : "en",
            SubjectCode = gatekeeper.PrimaryDomain,
            OwnershipType = isByos ? "byos" : "system_seeded",
            IngestParser = normalized.ParserName ?? extraction.ParserName ?? "text-parser",
            IngestParserVersion = "v1",
            ExtractionMode = normalized.ExtractionMode,
            VisionProvider = InferProvider(normalized.VisionModel),
            VisionModel = normalized.VisionModel,
            TotalPages = Math.Max(1, extraction.EstimatedPageCount),
            DetectedImagePlaceholderCount = normalized.DetectedImagePlaceholderCount,
            DetectedImageReferences = normalized.DetectedImageReferences,
            EmbeddedImageCount = normalized.EmbeddedImageCount,
            VisionEnrichmentAttemptedCount = normalized.VisionEnrichmentAttemptedCount,
            VisionEnrichmentSucceededCount = normalized.VisionEnrichmentSucceededCount,
            VisionEnrichmentFailedCount = normalized.VisionEnrichmentFailedCount,
            UnresolvedImagePlaceholderCount = normalized.UnresolvedImagePlaceholderCount,
            CandidateTitles = normalized.CandidateTitles,
            CandidateChapterMarkers = normalized.CandidateChapterMarkers,
            RejectedHeadingCandidates = normalized.RejectedHeadingCandidates,
            CleanDisplayTitleCandidates = normalized.CleanDisplayTitleCandidates,
            CleanupWarnings = normalized.Warnings,
            RawTextPreview = BuildPreview(extraction.RawText),
            CleanMarkdownPreview = BuildPreview(normalized.NormalizedMarkdown),
            ApprovedMarkdownPreview = BuildPreview(normalized.NormalizedMarkdown),
            ReviewStatus = "auto_ingested",
            ExtractionInputTokens = inputTokens,
            ExtractionOutputTokens = outputTokens,
            ExtractionCostUsd = 0,
            ExtractionLatencyMs = 0,
            CreditChargeStatus = "pending",
            CreditChargeAmount = 0,
            PricingBasisSnapshot = new ExtractionPricingBasisSnapshot
            {
                SourceSizeBytes = Math.Max(1, extraction.RawText.Length),
                TotalPages = Math.Max(1, extraction.EstimatedPageCount),
                TotalWordsEstimate = normalized.WordCount,
                DetectedImagePlaceholderCount = normalized.DetectedImagePlaceholderCount,
                EmbeddedImageCount = normalized.EmbeddedImageCount,
                VisionEnrichmentAttemptedCount = normalized.VisionEnrichmentAttemptedCount,
                VisionEnrichmentSucceededCount = normalized.VisionEnrichmentSucceededCount,
                VisionEnrichmentFailedCount = normalized.VisionEnrichmentFailedCount,
                UnresolvedImagePlaceholderCount = normalized.UnresolvedImagePlaceholderCount,
                ExtractionInputTokens = inputTokens,
                ExtractionOutputTokens = outputTokens,
                ExtractionCostUsd = 0
            }
        };
    }

    private static ExtractionDraftDto MapDraftDto(AIExtractionDraft draft)
    {
        return new ExtractionDraftDto
        {
            DraftId = draft.Id,
            DocumentId = draft.DocumentId,
            CourseId = draft.CourseId,
            SourceType = draft.SourceType,
            SourceName = draft.SourceName,
            Language = draft.Language,
            SubjectCode = draft.SubjectCode,
            IngestParser = draft.IngestParser,
            ExtractionMode = draft.ExtractionMode,
            VisionModel = draft.VisionModel,
            TotalPages = draft.TotalPages,
            TotalSegments = draft.TotalSegments,
            DetectedImagePlaceholderCount = draft.DetectedImagePlaceholderCount,
            EmbeddedImageCount = draft.EmbeddedImageCount,
            VisionEnrichmentAttemptedCount = draft.VisionEnrichmentAttemptedCount,
            VisionEnrichmentSucceededCount = draft.VisionEnrichmentSucceededCount,
            VisionEnrichmentFailedCount = draft.VisionEnrichmentFailedCount,
            UnresolvedImagePlaceholderCount = draft.UnresolvedImagePlaceholderCount,
            CandidateTitles = draft.CandidateTitles,
            CandidateChapterMarkers = draft.CandidateChapterMarkers,
            RejectedHeadingCandidates = draft.RejectedHeadingCandidates,
            CleanDisplayTitleCandidates = draft.CleanDisplayTitleCandidates,
            ReviewStatus = draft.ReviewStatus,
            IngestionStage = draft.ChunkingReady && draft.ChunkCount > 0 ? "ready_for_generation" : "normalized_only",
            CleanMarkdownPreview = draft.CleanMarkdownPreview,
            CleanupWarnings = draft.CleanupWarnings,
            ApprovedSegments = draft.ApprovedSegments,
            RejectedSegments = draft.RejectedSegments,
            ChunkingReady = draft.ChunkingReady,
            HasEmbeddedChunks = draft.ChunkingReady && draft.ChunkCount > 0,
            IsReadyForGeneration = draft.ChunkingReady && draft.ChunkCount > 0,
            ChunkCount = draft.ChunkCount,
            LastEmbeddingRunId = draft.LastEmbeddingRunId,
            CreatedAt = draft.CreatedAt,
            UpdatedAt = draft.UpdatedAt
        };
    }

    private static string ResolveIngestionStage(Document doc, bool hasDraft, bool hasEmbeddedChunks)
    {
        if (doc.Status == DocumentStatus.Deleted)
        {
            return "deleted";
        }

        if (doc.Status == DocumentStatus.Completed && hasEmbeddedChunks)
        {
            return "ready_for_generation";
        }

        if (doc.Status == DocumentStatus.Failed)
        {
            return "failed";
        }

        if (hasEmbeddedChunks)
        {
            return "embedded";
        }

        if (hasDraft)
        {
            return "normalized_only";
        }

        if (!string.IsNullOrWhiteSpace(doc.GatekeeperVerdict))
        {
            return "gatekeeper_passed";
        }

        return "processing";
    }

    private async Task<DocumentDto> BuildDocumentDtoAsync(
        Document doc,
        bool duplicateDetected,
        string? duplicateMatchType,
        string? duplicateOfDocumentId,
        decimal usdToVndRate = 0m,
        long actualCostVnd = 0L,
        long chargedVnd = 0L,
        long actualDeductedVnd = 0L,
        long absorbedVnd = 0L,
        long remainingBalanceVnd = 0L,
        int? knownChunkCount = null)
    {
        var draft = string.IsNullOrWhiteSpace(doc.LastExtractionDraftId)
            ? null
            : await _draftRepo.GetByIdAsync(doc.LastExtractionDraftId);
        var chunkCount = knownChunkCount ?? (await _chunkRepo.GetByDocumentIdAsync(doc.Id)).Count;
        var hasDraft = draft is not null || !string.IsNullOrWhiteSpace(doc.LastExtractionDraftId);
        var hasEmbeddedChunks = chunkCount > 0 || (draft?.ChunkingReady == true && draft.ChunkCount > 0);

        return new DocumentDto
        {
            Id = doc.Id,
            CourseId = doc.CourseId,
            FileName = doc.FileName,
            FileType = doc.FileType,
            FileSizeBytes = await ResolveDocumentFileSizeAsync(doc, draft),
            IsActive = doc.IsActive,
            FilePath = doc.FilePath,
            Source = doc.Source,
            SubjectCode = doc.SubjectCode,
            GatekeeperVerdict = doc.GatekeeperVerdict,
            LastExtractionDraftId = doc.LastExtractionDraftId,
            IngestionStatus = doc.Status.ToString().ToLowerInvariant(),
            IngestionStage = duplicateDetected && doc.Status == DocumentStatus.Completed && hasEmbeddedChunks
                ? "duplicate_reused"
                : ResolveIngestionStage(doc, hasDraft, hasEmbeddedChunks),
            HasExtractionDraft = hasDraft,
            HasEmbeddedChunks = hasEmbeddedChunks,
            IsReadyForGeneration = doc.Status == DocumentStatus.Completed && hasEmbeddedChunks,
            ChunkCount = chunkCount > 0 ? chunkCount : draft?.ChunkCount ?? 0,
            CreatedAt = doc.CreatedAt,
            LastExtractedAt = doc.LastExtractedAt,
            LastEmbeddedAt = doc.LastEmbeddedAt,
            DuplicateDetected = duplicateDetected,
            DuplicateOfDocumentId = duplicateOfDocumentId,
            DuplicateMatchType = duplicateMatchType,
            UsdToVndRate = usdToVndRate,
            ActualCostVnd = actualCostVnd,
            ChargedVnd = chargedVnd,
            ActualDeductedVnd = actualDeductedVnd,
            AbsorbedVnd = absorbedVnd,
            RemainingBalanceVnd = remainingBalanceVnd
        };
    }

    private async Task<long> ResolveDocumentFileSizeAsync(Document doc, AIExtractionDraft? draft, CancellationToken cancellationToken = default)
    {
        if (doc.FileSizeBytes > 0)
        {
            return doc.FileSizeBytes;
        }

        if (!string.IsNullOrWhiteSpace(doc.FilePath))
        {
            var storageFileSize = await _fileStorage.GetSizeAsync(doc.FilePath, cancellationToken);
            if (storageFileSize.HasValue && storageFileSize.Value > 0)
            {
                return storageFileSize.Value;
            }
        }

        if (draft?.SourceSizeBytes > 0)
        {
            return draft.SourceSizeBytes;
        }

        return 0L;
    }

    private static List<string> SegmentMarkdown(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new List<string>();
        }

        var blocks = Regex.Split(markdown.Trim(), @"\n\s*\n")
            .Select(block => block.Trim())
            .Where(block => !string.IsNullOrWhiteSpace(block))
            .ToList();

        if (blocks.Count == 0)
        {
            return new List<string> { markdown.Trim() };
        }

        var segments = new List<string>();
        var current = new StringBuilder();

        foreach (var block in blocks)
        {
            if (current.Length > 0 && current.Length + block.Length > 1600)
            {
                segments.Add(current.ToString().Trim());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.AppendLine();
                current.AppendLine();
            }

            current.Append(block);
        }

        if (current.Length > 0)
        {
            segments.Add(current.ToString().Trim());
        }

        return segments;
    }

    private static string? BuildPreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var normalized = content.Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private static int CountWords(string content)
        => string.IsNullOrWhiteSpace(content) ? 0 : content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static int CountLogicalSegments(string markdown)
        => SegmentMarkdown(markdown).Count;

    private static int EstimateTokenCount(string content)
        => string.IsNullOrWhiteSpace(content) ? 0 : Math.Max(1, (int)Math.Round(content.Length / 4.0));

    private static async Task<MemoryStream> BufferInputAsync(Stream input, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        if (input.CanSeek)
        {
            input.Position = 0;
        }

        await input.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    private static void ResetStreamPosition(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
    }

    private static string ComputeStableHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ComputeStableHash(byte[] content)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(content ?? Array.Empty<byte>());
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? InferProvider(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        if (model.Contains("gemini", StringComparison.OrdinalIgnoreCase))
        {
            return "Gemini";
        }

        if (model.Contains("gpt", StringComparison.OrdinalIgnoreCase))
        {
            return "OpenAI";
        }

        if (model.Contains("cohere", StringComparison.OrdinalIgnoreCase) ||
            model.Contains("embed", StringComparison.OrdinalIgnoreCase) ||
            model.Contains("command", StringComparison.OrdinalIgnoreCase))
        {
            return "Cohere";
        }

        return "Unknown";
    }
}
