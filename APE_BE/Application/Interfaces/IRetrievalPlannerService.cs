/**
 * IRetrievalPlannerService.cs
 * Interface for the RAG Retrieval and Context Planning subsystem in Step 4 of the APE AI Pipeline.
 * 
 * Responsibilities:
 * 1. Hybrid Search (metadata filters + vector cosine similarity).
 * 2. Multi-chapter context packing and token-budget pruning for BYOS and System courses.
 * 3. Topic and Chapter summary aggregation.
 */

using Application.DTOs;

namespace Application.Interfaces;

/// <summary>
/// Service contract for planning and retrieving relevant context chunks (RAG) for question generation.
/// </summary>
public interface IRetrievalPlannerService
{
    /// <summary>
    /// Executes a retrieval plan: performs hybrid search across document chunks to build a concise Context Pack.
    /// </summary>
    Task<RetrievalPlanResultDto> PlanAsync(RetrievalPlanRequestDto request, CancellationToken cancellationToken = default);

    Task<List<GenerationSourceDocumentDto>> GetSystemSourceDocumentsAsync(string userId, string courseId, CancellationToken cancellationToken = default);
    Task<TopicSummaryResultDto> GetSystemTopicSummaryAsync(string userId, string courseId, CancellationToken cancellationToken = default);
    Task<TopicSummaryResultDto> GetSystemDocumentChapterTopicSummaryAsync(string userId, string courseId, string documentId, string chapterKey, CancellationToken cancellationToken = default);
    Task<TopicSummaryResultDto> GetSystemDocumentMultiChapterTopicSummaryAsync(string userId, string courseId, string documentId, IReadOnlyList<string> chapterKeys, CancellationToken cancellationToken = default);
    Task<TopicSummaryResultDto> GetByosDocumentTopicSummaryAsync(string userId, string documentId, CancellationToken cancellationToken = default);
    Task<TopicSummaryResultDto> GetByosDocumentChapterTopicSummaryAsync(string userId, string documentId, string chapterKey, CancellationToken cancellationToken = default);
    Task<TopicSummaryResultDto> GetByosDocumentMultiChapterTopicSummaryAsync(string userId, string documentId, IReadOnlyList<string> chapterKeys, CancellationToken cancellationToken = default);
    Task<ChapterSummaryResultDto> GetSystemDocumentChapterSummaryAsync(string userId, string courseId, string documentId, CancellationToken cancellationToken = default);
    Task<ChapterSummaryResultDto> GetByosDocumentChapterSummaryAsync(string userId, string documentId, CancellationToken cancellationToken = default);
    Task<ContextPackDto?> GetContextPackAsync(string userId, string packId, CancellationToken cancellationToken = default);
    Task<List<ContextPackSummaryDto>> ListContextPacksAsync(
        string userId,
        string? subject,
        string? questionType,
        string? difficulty,
        string? topic,
        string? status,
        int take,
        CancellationToken cancellationToken = default);
    Task<ContextPackDto> MarkStaleAsync(string userId, string packId, CancellationToken cancellationToken = default);
}
