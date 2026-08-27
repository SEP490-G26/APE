using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/admin/courses")]
[Authorize(Roles = "Admin")]
public class CourseController : ControllerBase
{
    private readonly ICourseService _service;

    public CourseController(ICourseService service)
    {
        _service = service;
    }

    private string? UserId =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>
    /// Create new course.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CourseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateCourseDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto, UserId);

            return Ok(ApiResponse.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get course by id.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CourseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await _service.GetByIdAsync(id);

        if (result == null)
            return NotFound(ApiResponse.Fail("Course not found."));

        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Get paginated courses (Now with Search!).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CourseListResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? keyword = null, // Mở khóa tính năng tìm kiếm
        [FromQuery] string? status = null)
    {
        try
        {
            // Truyền keyword xuống Service
            var result = await _service.GetPagedAsync(page, limit, keyword, status);
            return Ok(ApiResponse.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Update course information.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CourseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string id,
        UpdateCourseDto dto)
    {
        try
        {
            var result = await _service.UpdateAsync(id, dto, UserId);

            return Ok(ApiResponse.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Soft delete course.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await _service.DeleteAsync(id, UserId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Update course status.
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<CourseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        string id,
        UpdateCourseStatusDto dto)
    {
        try
        {
            var result = await _service.UpdateStatusAsync(id, dto, UserId);

            return Ok(ApiResponse.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }
}