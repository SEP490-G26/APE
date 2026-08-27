using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUserController : ControllerBase
{
    private readonly IUserRepository _userRepo;

    public AdminUserController(IUserRepository userRepo) => _userRepo = userRepo;

    private string? AdminId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);
        var (users, total) = await _userRepo.ListAsync(search, role, status, page, limit);

        var items = users.Select(MapToListDto).ToList();

        return Ok(ApiResponse.Ok(new
        {
            items,
            total,
            page,
            limit,
            totalPages = (int)Math.Ceiling(total / (double)limit)
        }));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AdminUserDetailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetById(string id)
    {
        var user = await _userRepo.GetByIdAsync(id);
        if (user == null) return NotFound(ApiResponse.Fail("Không t́m th?y ng??i dùng."));
        return Ok(ApiResponse.Ok(MapToDetailDto(user)));
    }

    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateUserStatusDto dto)
    {
       
        if (string.Equals(AdminId, id, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Fail("Administrators cannot change the status of their own account."));

        var target = await _userRepo.GetByIdAsync(id);
        if (target == null)
        {
            return NotFound(ApiResponse.Fail("User not found."));
        }

        if (!string.Equals(target.Role, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse.Fail("Only Student accounts can be banned/unbanned."));
        }

        if (string.IsNullOrWhiteSpace(dto.Status))
            return BadRequest(ApiResponse.Fail("Status is required."));

        string newStatus = dto.Status.Trim();
        if (!string.Equals(newStatus, "Active", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(newStatus, "Banned", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Fail("Status must be 'Active' or 'Banned'."));

        bool shouldClearTokens = string.Equals(newStatus, "Banned", StringComparison.OrdinalIgnoreCase);

        
        bool isSuccess = await _userRepo.UpdateUserStatusAsync(id, newStatus, shouldClearTokens);

        if (!isSuccess)
            return NotFound(ApiResponse.Fail("User not found."));

        return Ok(ApiResponse.Ok(new { Id = id, Status = newStatus }));
    }

    private static AdminUserListDto MapToListDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        AvatarUrl = user.AvatarUrl,
        Role = user.Role,
        Status = user.Status
    };

    private static AdminUserDetailDto MapToDetailDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        AvatarUrl = user.AvatarUrl,
        Role = user.Role,
        Status = user.Status,
       
    };
}
