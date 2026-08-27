using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Services;
using Application.DTOs;
using System.Security.Claims;

namespace API.Controllers;

[Authorize]
[ApiController]
[Route("api/student")]
public class UserController : ControllerBase
{
    private readonly UserService _userService;

    public UserController(UserService userService)
    {
        _userService = userService;
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var profile = await _userService.GetProfileAsync(UserId);
        if (profile == null) return NotFound();
        return Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var profile = await _userService.UpdateProfileAsync(UserId, dto);
        if (profile == null) return NotFound();
        return Ok(profile);
    }

}
