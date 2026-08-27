using System.Security.Claims;
using Application.Common;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/student/analytics")]
[Authorize]
public class StudentAnalyticsController : ControllerBase
{
    private readonly GamificationService _gamificationService;

    public StudentAnalyticsController(GamificationService gamificationService)
    {
        _gamificationService = gamificationService;
    }

    private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized."));
        }

        var summary = await _gamificationService.GetSummaryAsync(UserId);
        if (summary == null) return NotFound(ApiResponse.Fail("User not found"));
        return Ok(ApiResponse.Ok(summary));
    }
}
