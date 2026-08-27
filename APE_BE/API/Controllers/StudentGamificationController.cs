using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/student")]
[Authorize]
public class StudentGamificationController : ControllerBase
{
    private readonly GamificationService _gamificationService;

    public StudentGamificationController(GamificationService gamificationService)
    {
        _gamificationService = gamificationService;
    }

    private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("streak")]
    public async Task<IActionResult> GetStreak()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized."));
        }

        var streak = await _gamificationService.GetStreakAsync(UserId);
        if (streak == null) return NotFound(ApiResponse.Fail("User not found"));
        return Ok(ApiResponse.Ok(streak));
    }

    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard([FromQuery] string? courseId = null, [FromQuery] string scope = "Global")
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized."));
        }

        var data = await _gamificationService.GetLeaderboardAsync(courseId, scope, UserId);
        return Ok(ApiResponse.Ok(data));
    }
}
