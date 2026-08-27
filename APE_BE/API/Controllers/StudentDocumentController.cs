using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/student/documents")]
[Authorize]
public class StudentDocumentController : ControllerBase
{
    private readonly DocumentService _service;
    private readonly IRetrievalPlannerService _retrievalPlannerService;

    public StudentDocumentController(DocumentService service, IRetrievalPlannerService retrievalPlannerService)
    {
        _service = service;
        _retrievalPlannerService = retrievalPlannerService;
    }

    private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost("byos")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> BYOS([FromForm] DocumentUploadDto dto)
    {
        try
        {
            var file = Request.Form.Files.FirstOrDefault();
            if (file == null) return BadRequest(ApiResponse.Fail("File is required"));

            using var stream = file.OpenReadStream();
            var result = await _service.UploadAsync(stream, file.FileName, UserId ?? "", dto.CourseId, true);
            return Ok(ApiResponse.Ok(result));
        }
        catch (AIProviderException ex)
        {
            return StatusCode(MapAiProviderStatusCode(ex), ApiResponse.Fail(MapAiProviderMessage(ex)));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/preview")]
    public async Task<IActionResult> Preview(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized(ApiResponse.Fail("Unauthorized"));

            var p = await _service.GetPreviewAsync(id, UserId);
            if (p == null) return NotFound(ApiResponse.Fail("Document not found or no preview available"));
            return Ok(ApiResponse.Ok(p));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<DocumentListItemDto>>), 200)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized(ApiResponse.Fail("Unauthorized"));

            var result = await _service.ListByUserAsync(UserId, page, limit);
            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/download")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Download(string id, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized(ApiResponse.Fail("Unauthorized"));

            var doc = await _service.GetByIdAsync(id);
            if (doc == null || doc.Status == Domain.Enums.DocumentStatus.Deleted)
            {
                return NotFound(ApiResponse.Fail("Document not found"));
            }

            if (!string.Equals(doc.UserId, UserId, StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(ApiResponse.Fail("You do not have permission to download this document"));
            }

            if (!await _service.FileExistsAsync(doc.FilePath, cancellationToken))
            {
                return NotFound(ApiResponse.Fail("File not found in storage"));
            }

            var stream = await _service.OpenFileReadAsync(doc.FilePath, cancellationToken);
            if (stream == null)
            {
                return NotFound(ApiResponse.Fail("File not found in storage"));
            }

            var contentType = GetContentType(doc.FileType);
            return File(stream, contentType, doc.FileName);
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized(ApiResponse.Fail("Unauthorized"));

            await _service.DeleteByStudentAsync(id, UserId, cancellationToken);
            return Ok(ApiResponse.Ok(true));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/extraction-draft")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetExtractionDraft(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized(ApiResponse.Fail("Unauthorized"));

            var draft = await _service.GetExtractionDraftAsync(id, UserId, isAdmin: false);
            if (draft == null) return NotFound(ApiResponse.Fail("Extraction draft not found"));
            return Ok(ApiResponse.Ok(draft));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/topics")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(ApiResponse<TopicSummaryResultDto>), 200)]
    public async Task<IActionResult> GetByosTopics(string id, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized(ApiResponse.Fail("Unauthorized"));

            var result = await _retrievalPlannerService.GetByosDocumentTopicSummaryAsync(UserId, id, cancellationToken);
            return Ok(ApiResponse.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/chapters")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(ApiResponse<ChapterSummaryResultDto>), 200)]
    public async Task<IActionResult> GetByosChapters(string id, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized(ApiResponse.Fail("Unauthorized"));

            var result = await _retrievalPlannerService.GetByosDocumentChapterSummaryAsync(UserId, id, cancellationToken);
            return Ok(ApiResponse.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/chapters/{chapterKey}/topics")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(ApiResponse<TopicSummaryResultDto>), 200)]
    public async Task<IActionResult> GetByosChapterTopics(string id, string chapterKey, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized(ApiResponse.Fail("Unauthorized"));

            var result = await _retrievalPlannerService.GetByosDocumentChapterTopicSummaryAsync(UserId, id, chapterKey, cancellationToken);
            return Ok(ApiResponse.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/chapter-topics")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(ApiResponse<TopicSummaryResultDto>), 200)]
    public async Task<IActionResult> GetByosMultiChapterTopics(string id, [FromQuery] string[] chapterKeys, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId)) return Unauthorized(ApiResponse.Fail("Unauthorized"));

            var normalizedKeys = chapterKeys
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (normalizedKeys.Count == 0) return BadRequest(ApiResponse.Fail("At least one chapterKey is required."));

            var result = await _retrievalPlannerService.GetByosDocumentMultiChapterTopicSummaryAsync(UserId, id, normalizedKeys, cancellationToken);
            return Ok(ApiResponse.Ok(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    private static int MapAiProviderStatusCode(AIProviderException exception)
    {
        return exception.StatusCode switch
        {
            408 => StatusCodes.Status504GatewayTimeout,
            429 => StatusCodes.Status429TooManyRequests,
            401 or 403 or 404 => StatusCodes.Status502BadGateway,
            >= 500 => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status502BadGateway
        };
    }

    private static string MapAiProviderMessage(AIProviderException exception)
    {
        return exception.StatusCode switch
        {
            401 or 403 =>
                "The AI provider authentication failed. Please contact the administrator to check the API key and provider settings.",
            404 =>
                "The AI model or endpoint is configured incorrectly. Please contact the administrator to review the settings.",
            408 =>
                "The AI service is responding too slowly. Please try again later.",
            429 =>
                "The AI service is temporarily rate-limited. Please try again later.",
            >= 500 =>
                "The AI provider is currently unavailable. Please try again later.",
            _ =>
                "Unable to process the document because the AI service has a configuration or connection problem. Please try again later."
        };
    }

    private static string GetContentType(string fileType) => fileType.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };
}
