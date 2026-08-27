using Application.DTOs;

namespace Application.Interfaces;

public interface ICourseService
{
    Task<CourseDto> CreateAsync(CreateCourseDto dto, string? userId);

    Task<CourseDto?> GetByIdAsync(string id);


    Task<CourseListResponseDto> GetPagedAsync(
        int page,
        int limit,
        string? keyword,
        string? status);

    Task<CourseDto> UpdateAsync(
        string id,
        UpdateCourseDto dto,
        string? userId);

    Task DeleteAsync(
        string id,
        string? userId);

    Task<CourseDto> UpdateStatusAsync(
        string id,
        UpdateCourseStatusDto dto,
        string? userId);
}