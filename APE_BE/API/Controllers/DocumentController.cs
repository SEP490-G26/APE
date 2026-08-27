using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/admin/documents")]
[Authorize(Roles = "Admin")]
public class DocumentController : ControllerBase
{
    private readonly DocumentService _service;

    public DocumentController(DocumentService service) => _service = service;

    private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost("upload")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), 200)]
    public async Task<IActionResult> Upload([FromForm] DocumentUploadDto dto)
    {
        try
        {
            var file = Request.Form.Files.FirstOrDefault();
            if (file == null) return BadRequest(ApiResponse.Fail("File is required"));

            using var stream = file.OpenReadStream();
            var result = await _service.UploadAsync(stream, file.FileName, UserId ?? "", dto.CourseId);
            return Ok(ApiResponse.Ok(result));
        }
        catch (AIProviderException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<DocumentListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 20, [FromQuery] string? courseId = null)
    {
        try
        {
            var result = await _service.ListSystemAsync(page, limit, courseId);
            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/preview")]
    [ProducesResponseType(typeof(ApiResponse<DocumentPreviewResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(string id)
    {
        try
        {
            var preview = await _service.GetPreviewAsync(id, UserId ?? string.Empty, isAdmin: true);
            if (preview == null)
            {
                return NotFound(ApiResponse.Fail("Document not found or no preview available"));
            }

            return Ok(ApiResponse.Ok(preview));
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

    [HttpGet("{id}/download")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Download(string id)
    {
        var doc = await _service.GetByIdAsync(id);
        if (doc == null) return NotFound(ApiResponse.Fail("Document not found"));

        if (!await _service.FileExistsAsync(doc.FilePath))
            return NotFound(ApiResponse.Fail("File not found on server"));

        var stream = await _service.OpenFileReadAsync(doc.FilePath);
        if (stream == null)
        {
            return NotFound(ApiResponse.Fail("File not found on server"));
        }

        var contentType = GetContentType(doc.FileType);
        return File(stream, contentType, doc.FileName);
    }

    [HttpPut("{id}/edit")]
    public async Task<IActionResult> Edit(string id, [FromBody] EditContentDto dto)
    {
        await _service.EditContentAsync(id, dto.Content, UserId ?? "");
        return Ok(ApiResponse.Ok(true));
    }

    [HttpPut("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(string id)
    {
        await _service.ToggleActiveAsync(id);
        return Ok(ApiResponse.Ok(true));
    }

    [HttpGet("{id}/extraction-draft")]
    [ProducesResponseType(typeof(ApiResponse<ExtractionDraftDto>), 200)]
    public async Task<IActionResult> GetExtractionDraft(string id)
    {
        var draft = await _service.GetExtractionDraftAsync(id, UserId ?? string.Empty, isAdmin: true);
        if (draft == null)
        {
            return NotFound(ApiResponse.Fail("Extraction draft not found"));
        }

        return Ok(ApiResponse.Ok(draft));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.DeleteByAdminAsync(id, UserId ?? string.Empty, cancellationToken);
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

    private static string GetContentType(string fileType) => fileType.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };
}
