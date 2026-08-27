using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/student/practice")]
[Authorize]
public class PracticeController : ControllerBase
{
    private readonly PracticeService _practiceService;

    public PracticeController(PracticeService practiceService)
    {
        _practiceService = practiceService;
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartSessionDto dto)
    {
        var result = await _practiceService.StartSessionAsync(UserId, dto.ExamId, dto.ForceNew);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId)
    {
        var result = await _practiceService.GetSessionAsync(UserId, sessionId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpGet("exams/{examId}/active-session")]
    public async Task<IActionResult> GetActiveSession(string examId)
    {
        var result = await _practiceService.GetActiveSessionByExamAsync(UserId, examId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("sessions/{sessionId}/end")]
    public async Task<IActionResult> EndSession(string sessionId)
    {
        var result = await _practiceService.EndSessionAsync(UserId, sessionId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("sessions/{sessionId}/draft-code")]
    public async Task<IActionResult> SaveDraftCode(string sessionId, [FromBody] SaveDraftCodeDto dto)
    {
        var result = await _practiceService.SaveDraftCodeAsync(UserId, sessionId, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("sessions/{sessionId}/pause")]
    public async Task<IActionResult> PauseSession(string sessionId)
    {
        var result = await _practiceService.PauseSessionAsync(UserId, sessionId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("sessions/{sessionId}/resume")]
    public async Task<IActionResult> ResumeSession(string sessionId)
    {
        var result = await _practiceService.ResumeSessionAsync(UserId, sessionId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("history")]
    // UC-19: View Practice History
    public async Task<IActionResult> History(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? courseId = null,
        [FromQuery] string? type = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var result = await _practiceService.GetPracticeHistoryAsync(UserId, page, limit, courseId, type, fromDate, toDate);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
