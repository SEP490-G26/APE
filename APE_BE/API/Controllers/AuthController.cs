using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly UserService _userService;

    public AuthController(
        IAuthService authService,
        UserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    private string ClientIp
    {
        get
        {
            if (Request.Headers.TryGetValue(
                    "X-Forwarded-For",
                    out var forwarded))
            {
                return forwarded
                    .FirstOrDefault()?
                    .Split(',')
                    .FirstOrDefault()?
                    .Trim()
                    ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "Unknown";
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString()
                   ?? "Unknown";
        }
    }

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException(
            "Authenticated user id is missing.");

    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(
        typeof(ApiResponse<AuthResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> GoogleLogin(
        [FromBody] GoogleLoginRequest request)
    {
        var response = await _authService.GoogleLoginAsync(
            request.IdToken,
            ClientIp);

        return Ok(ApiResponse.Ok(response));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(
        typeof(ApiResponse<AuthResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken(
        [FromBody] RefreshTokenRequest request)
    {
        var response = await _authService.RefreshTokenAsync(
            request.RefreshToken,
            ClientIp);

        return Ok(ApiResponse.Ok(response));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(
        typeof(ApiResponse<UserProfileDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Me()
    {
        await _authService.TouchDailyActivityAsync(UserId);
        var profile = await _userService.GetProfileAsync(UserId);

        if (profile is null)
        {
            return NotFound(ApiResponse.Fail("User profile was not found."));
        }

        return Ok(ApiResponse.Ok(profile));
    }

}
