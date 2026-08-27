using System.Text.Json;
using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileExtractionDraftStore : IExtractionDraftStore
{
    private readonly string _draftRoot;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public FileExtractionDraftStore(IHostEnvironment environment)
    {
        _draftRoot = Path.Combine(environment.ContentRootPath, "App_Data", "extraction-drafts");
        Directory.CreateDirectory(_draftRoot);
    }

    public async Task<ExtractionDraftEnvelope> SaveAsync(ExtractionDraft draft, IReadOnlyList<ExtractionDraftContent> contents, CancellationToken cancellationToken)
    {
        var envelope = new ExtractionDraftEnvelope(draft, contents);
        await WriteEnvelopeAsync(envelope, cancellationToken);
        return envelope;
    }

    public async Task<ExtractionDraftEnvelope?> GetAsync(string draftId, CancellationToken cancellationToken)
    {
        var path = BuildPath(draftId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ExtractionDraftEnvelope>(stream, _jsonOptions, cancellationToken);
    }

    public async Task<ExtractionDraftEnvelope> ApproveAsync(string draftId, ApproveExtractionDraftRequest request, CancellationToken cancellationToken)
    {
        var envelope = await GetAsync(draftId, cancellationToken)
            ?? throw new FileNotFoundException($"Extraction draft '{draftId}' was not found.");

        var patchMap = (request.Segments ?? [])
            .GroupBy(static item => item.ContentId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        var contents = envelope.Contents.Select(content =>
        {
            if (patchMap.TryGetValue(content.ContentId, out var patch))
            {
                var approvedMarkdown = string.IsNullOrWhiteSpace(patch.ApprovedMarkdown)
                    ? content.ApprovedMarkdown ?? content.CleanMarkdown
                    : patch.ApprovedMarkdown.Trim();
                var reviewStatus = NormalizeReviewStatus(patch.ReviewStatus, request.ApproveAll);
                return content with
                {
                    ApprovedMarkdown = approvedMarkdown,
                    ReviewStatus = reviewStatus,
                    ReviewedBy = patch.ReviewerId ?? request.ReviewerId ?? request.UserId,
                    ReviewedAt = now,
                    ApprovalVersion = content.ApprovalVersion + 1,
                    UpdatedAt = now
                };
            }

            if (request.ApproveAll)
            {
                return content with
                {
                    ApprovedMarkdown = content.ApprovedMarkdown ?? content.CleanMarkdown,
                    ReviewStatus = "approved",
                    ReviewedBy = request.ReviewerId ?? request.UserId,
                    ReviewedAt = now,
                    ApprovalVersion = content.ApprovalVersion + 1,
                    UpdatedAt = now
                };
            }

            return content;
        }).ToList();

        var approvedSegments = contents.Count(static item => string.Equals(item.ReviewStatus, "approved", StringComparison.OrdinalIgnoreCase));
        var rejectedSegments = contents.Count(static item => string.Equals(item.ReviewStatus, "rejected", StringComparison.OrdinalIgnoreCase));
        var reviewStatus = approvedSegments == contents.Count && contents.Count > 0
            ? "approved"
            : approvedSegments > 0
                ? "partially_approved"
                : "needs_review";

        var draft = envelope.Draft with
        {
            ApprovedSegments = approvedSegments,
            RejectedSegments = rejectedSegments,
            HumanNotes = request.HumanNotes ?? envelope.Draft.HumanNotes,
            ApprovedMarkdownPreview = BuildPreview(string.Join("\n\n", contents.Select(static item => item.ApprovedMarkdown ?? string.Empty).Where(static item => !string.IsNullOrWhiteSpace(item)))),
            ReviewStatus = reviewStatus,
            ReviewedBy = request.ReviewerId ?? request.UserId,
            ReviewedAt = now,
            ApprovalVersion = envelope.Draft.ApprovalVersion + 1,
            ChunkingReady = approvedSegments > 0,
            UpdatedAt = now
        };

        var updated = new ExtractionDraftEnvelope(draft, contents);
        await WriteEnvelopeAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<ExtractionDraftEnvelope> UpdateEmbeddingAsync(string draftId, int chunkCount, string pipelineRunId, CancellationToken cancellationToken)
    {
        var envelope = await GetAsync(draftId, cancellationToken)
            ?? throw new FileNotFoundException($"Extraction draft '{draftId}' was not found.");

        var updated = new ExtractionDraftEnvelope(
            envelope.Draft with
            {
                ChunkCount = chunkCount,
                LastEmbeddingRunId = pipelineRunId,
                ReviewStatus = "embedded",
                UpdatedAt = DateTimeOffset.UtcNow
            },
            envelope.Contents);

        await WriteEnvelopeAsync(updated, cancellationToken);
        return updated;
    }

    private async Task WriteEnvelopeAsync(ExtractionDraftEnvelope envelope, CancellationToken cancellationToken)
    {
        var path = BuildPath(envelope.Draft.DraftId);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, envelope, _jsonOptions, cancellationToken);
    }

    private string BuildPath(string draftId)
        => Path.Combine(_draftRoot, $"{draftId}.json");

    private static string NormalizeReviewStatus(string? requestedStatus, bool approveAll)
    {
        if (approveAll)
        {
            return "approved";
        }

        return requestedStatus?.Trim().ToLowerInvariant() switch
        {
            "approved" => "approved",
            "rejected" => "rejected",
            _ => "needs_review"
        };
    }

    private static string? BuildPreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var normalized = content.Trim();
        return normalized.Length <= 400 ? normalized : normalized[..400];
    }
}
