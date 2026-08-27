using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AIRetrievalController : ControllerBase
{
    private readonly IRetrievalPlannerService _retrievalPlannerService;

    public AIRetrievalController(IRetrievalPlannerService retrievalPlannerService)
    {
        _retrievalPlannerService = retrievalPlannerService;
    }

    private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost("retrieval/plan")]
    [ProducesResponseType(typeof(ApiResponse<RetrievalPlanResultDto>), 200)]
    public async Task<IActionResult> Plan([FromBody] RetrievalPlanRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        dto.UserId = UserId;
        var result = await _retrievalPlannerService.PlanAsync(dto, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("courses/{courseId}/topics")]
    [ProducesResponseType(typeof(ApiResponse<TopicSummaryResultDto>), 200)]
    public async Task<IActionResult> GetSystemCourseTopics(string courseId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var result = await _retrievalPlannerService.GetSystemTopicSummaryAsync(UserId, courseId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("courses/{courseId}/documents")]
    [ProducesResponseType(typeof(ApiResponse<List<GenerationSourceDocumentDto>>), 200)]
    public async Task<IActionResult> GetSystemCourseDocuments(string courseId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var result = await _retrievalPlannerService.GetSystemSourceDocumentsAsync(UserId, courseId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("courses/{courseId}/documents/{documentId}/chapters")]
    [ProducesResponseType(typeof(ApiResponse<ChapterSummaryResultDto>), 200)]
    public async Task<IActionResult> GetSystemDocumentChapters(string courseId, string documentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var result = await _retrievalPlannerService.GetSystemDocumentChapterSummaryAsync(UserId, courseId, documentId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("courses/{courseId}/documents/{documentId}/chapters/{chapterKey}/topics")]
    [ProducesResponseType(typeof(ApiResponse<TopicSummaryResultDto>), 200)]
    public async Task<IActionResult> GetSystemDocumentChapterTopics(string courseId, string documentId, string chapterKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var result = await _retrievalPlannerService.GetSystemDocumentChapterTopicSummaryAsync(UserId, courseId, documentId, chapterKey, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("courses/{courseId}/documents/{documentId}/chapter-topics")]
    [ProducesResponseType(typeof(ApiResponse<TopicSummaryResultDto>), 200)]
    public async Task<IActionResult> GetSystemDocumentMultiChapterTopics(string courseId, string documentId, [FromQuery] string[] chapterKeys, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var normalizedKeys = chapterKeys
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedKeys.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("At least one chapterKey is required."));
        }

        var result = await _retrievalPlannerService.GetSystemDocumentMultiChapterTopicSummaryAsync(UserId, courseId, documentId, normalizedKeys, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("context-packs/{packId}")]
    [ProducesResponseType(typeof(ApiResponse<ContextPackDto>), 200)]
    public async Task<IActionResult> GetContextPack(string packId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var pack = await _retrievalPlannerService.GetContextPackAsync(UserId, packId, cancellationToken);
        if (pack == null)
        {
            return NotFound(ApiResponse.Fail("Context pack not found"));
        }

        return Ok(ApiResponse.Ok(pack));
    }

    [HttpGet("context-packs")]
    [ProducesResponseType(typeof(ApiResponse<List<ContextPackSummaryDto>>), 200)]
    public async Task<IActionResult> ListContextPacks(
        [FromQuery] string? subject,
        [FromQuery] string? questionType,
        [FromQuery] string? difficulty,
        [FromQuery] string? topic,
        [FromQuery] string? status,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var result = await _retrievalPlannerService.ListContextPacksAsync(
            UserId,
            subject,
            questionType,
            difficulty,
            topic,
            status,
            take,
            cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("context-packs/{packId}/stale")]
    [ProducesResponseType(typeof(ApiResponse<ContextPackDto>), 200)]
    public async Task<IActionResult> MarkStale(string packId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var pack = await _retrievalPlannerService.MarkStaleAsync(UserId, packId, cancellationToken);
        return Ok(ApiResponse.Ok(pack));
    }
}
