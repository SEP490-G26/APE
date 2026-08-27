using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;

namespace BE_IntegrationTests.External;

public sealed class ControlledAIEmbeddingTaggingService : IAIEmbeddingTaggingService
{
    public Task<EmbeddingTaggingRunResult> CreateChunksAsync(
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
        var chunkText = string.IsNullOrWhiteSpace(content)
            ? "Controlled chunk content."
            : content.Trim();

        var chunk = new KnowledgeChunk
        {
            DocumentId = documentId,
            CourseId = courseId,
            UserId = userId,
            ChunkIndex = 0,
            SourceType = sourceType,
            SourceName = "controlled-source",
            SubjectCode = subjectCode ?? "PRN212",
            ChapterKey = "chapter-1",
            ChapterTitle = "Controlled Chapter 1",
            SectionTitle = "Controlled Section",
            TopicTags = ["queues", "stacks"],
            RawText = chunkText,
            ContentText = chunkText,
            MarkdownText = chunkText,
            NormalizedText = chunkText,
            TokenCount = Math.Max(32, chunkText.Length / 4),
            WordCount = Math.Max(1, chunkText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length),
            CharCount = chunkText.Length,
            Status = "active",
            RetrievalEnabled = true,
            SourcePageFrom = 1,
            SourcePageTo = 1,
            ChapterOrder = 1
        };

        return Task.FromResult(new EmbeddingTaggingRunResult
        {
            Chunks = [chunk],
            EmbeddingUsage = new EmbeddingUsageSummaryDto
            {
                Provider = "ControlledAI",
                EffectiveModel = "embedding-v1",
                ConfiguredModel = "embedding-v1",
                ModelFamily = "controlled",
                NormalizedModelKey = "controlled.embedding-v1",
                UsageSource = "controlled",
                CostSource = "controlled",
                TokensUsed = chunk.TokenCount,
                VectorCount = 1,
                EmbeddingDimension = 8,
                CostUsd = 0.001m
            },
            TaggingUsage = new TaggingUsageSummaryDto
            {
                Provider = "ControlledAI",
                EffectiveModel = "tagging-v1",
                ConfiguredModel = "tagging-v1",
                ModelFamily = "controlled",
                NormalizedModelKey = "controlled.tagging-v1",
                UsageSource = "controlled",
                CostSource = "controlled",
                InputTokens = 32,
                OutputTokens = 12,
                TotalTokens = 44,
                CallCount = 1,
                SuccessfulCalls = 1,
                CostUsd = 0.001m
            }
        });
    }
}
