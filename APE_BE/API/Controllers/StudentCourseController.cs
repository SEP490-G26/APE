using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/student/courses")]
[Authorize]
public class StudentCourseController : ControllerBase
{
    private readonly ICourseRepository _repo;

    public StudentCourseController(ICourseRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        try
        {
            limit = limit <= 0 ? 20 : limit;
            page = page <= 0 ? 1 : page;
            var (items, total) = await _repo.ListAsync(page, limit, status: "Active");
            var dtos = items.Select(c => new CourseDto { Id = c.Id, Name = c.Name, Code = c.Code, Description = c.Description, Status = c.Status }).ToList();
            var resp = new { items = dtos, total, page, limit, totalPages = (total + limit - 1) / limit };
            return Ok(ApiResponse.Ok(resp));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        try
        {
            var c = await _repo.GetByIdAsync(id);
            if (c == null || c.Status != "Active") return NotFound(ApiResponse.Fail("Course not found"));
            return Ok(ApiResponse.Ok(c));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }
}
