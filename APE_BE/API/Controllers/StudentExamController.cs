using Application.Common;
using Application.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/student/exams")]
[Authorize]
public class StudentExamController : ControllerBase
{
    private readonly ExamService _examService;
    private readonly PracticeService _practiceService;

    public StudentExamController(ExamService examService, PracticeService practiceService)
    {
        _examService = examService;
        _practiceService = practiceService;
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    [HttpGet("{id}")]
    public async Task<IActionResult> GetExamById(string id)
    {
        try
        {
            var exam = await _examService.GetExamDetailByIdAsync(id);
            if (exam == null) return NotFound(ApiResponse.Fail("Exam not found"));
            return Ok(ApiResponse.Ok(exam));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMyExam(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(ApiResponse.Fail("Exam id is required."));
            }

            await _examService.SoftDeleteExamAsync(id, UserId, requireOwner: true);
            return Ok(ApiResponse.Ok(true));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListExams([FromQuery] string courseId, [FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(courseId))
                return BadRequest(ApiResponse.Fail("courseId is required"));

            if (page < 1 || limit < 1)
                return BadRequest(ApiResponse.Fail("page and limit must be >= 1"));

            var (items, total) = await _examService.ListPublicAsync(courseId, null, null, page, limit);
            var response = new
            {
                items,
                total,
                page,
                limit,
                totalPages = (total + limit - 1) / limit
            };
            return Ok(ApiResponse.Ok(response));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("library")]
    public async Task<IActionResult> Library([FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        try
        {
            if (page < 1 || limit < 1)
                return BadRequest(ApiResponse.Fail("page and limit must be >= 1"));

            var (items, total) = await _examService.ListOwnedAsync(UserId, null, null, null, page, limit);
            foreach (var item in items)
            {
                var activeSession = await _practiceService.GetActiveSessionByExamAsync(UserId, item.Id);
                var session = activeSession.Data;
                if (session == null)
                {
                    continue;
                }

                item.HasActiveSession = true;
                item.ActiveSessionId = session.SessionId;
                item.ActiveSessionPaused = session.IsPaused;
                item.ActiveSessionElapsedSeconds = session.ActiveDurationSeconds;
                item.ActiveDraftCodeCount = session.DraftCodeCount;
                item.ActiveSelectedQuestionId = session.SelectedQuestionId;
            }

            var response = new
            {
                items,
                total,
                page,
                limit,
                totalPages = (total + limit - 1) / limit
            };
            return Ok(ApiResponse.Ok(response));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("system-library")]
    public async Task<IActionResult> SystemLibrary([FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        try
        {
            if (page < 1 || limit < 1)
                return BadRequest(ApiResponse.Fail("page and limit must be >= 1"));

            var (items, total) = await _examService.ListPublicAsync(null, null, null, page, limit);
            foreach (var item in items)
            {
                var activeSession = await _practiceService.GetActiveSessionByExamAsync(UserId, item.Id);
                var session = activeSession.Data;
                if (session == null)
                {
                    continue;
                }

                item.HasActiveSession = true;
                item.ActiveSessionId = session.SessionId;
                item.ActiveSessionPaused = session.IsPaused;
                item.ActiveSessionElapsedSeconds = session.ActiveDurationSeconds;
                item.ActiveDraftCodeCount = session.DraftCodeCount;
                item.ActiveSelectedQuestionId = session.SelectedQuestionId;
            }

            var response = new
            {
                items,
                total,
                page,
                limit,
                totalPages = (total + limit - 1) / limit
            };
            return Ok(ApiResponse.Ok(response));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }
}
