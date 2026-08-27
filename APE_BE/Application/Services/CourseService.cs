using Application.Constants;
using Application.DTOs;
using Application.Interfaces;
using Application.Mappers;
using Domain.Entities;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

namespace Application.Services;

public class CourseService : ICourseService
{
    private readonly ICourseRepository _repository;

    public CourseService(ICourseRepository repository)
    {
        _repository = repository;
    }

    public async Task<CourseDto> CreateAsync(CreateCourseDto dto, string? userId)
    {
        dto.Code = dto.Code.Trim().ToUpperInvariant();

        var existed = await _repository.GetByCodeAsync(dto.Code);

        if (existed != null)
            throw new InvalidOperationException("Course code already exists.");

        ValidateExamMatrix(dto.ExamMatrix);

        var entity = dto.ToEntity(userId);

        await _repository.CreateAsync(entity);

        return entity.ToDto();
    }

    public async Task<CourseDto?> GetByIdAsync(string id)
    {
        var entity = await _repository.GetByIdAsync(id);

        return entity?.ToDto();
    }

    public async Task<CourseListResponseDto> GetPagedAsync(
    int page,
    int limit,
    string? keyword,
    string? status)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!CourseStatus.All.Contains(status))
                throw new ArgumentException("Invalid course status.");
        }

  
        var (items, total) = await _repository.ListAsync(page, limit, keyword, status);

        return new CourseListResponseDto
        {
            Items = items
                .Select(x => x.ToListDto())
                .ToList(),

            Total = total,
            Page = page,
            Limit = limit,
            TotalPages = (long)Math.Ceiling(total / (double)limit)
        };
    }

    public async Task<CourseDto> UpdateAsync(
        string id,
        UpdateCourseDto dto,
        string? userId)
    {
        var entity = await GetCourseOrThrow(id);

        ValidateExamMatrix(dto.ExamMatrix);

        dto.UpdateEntity(entity, userId);

        await _repository.UpdateAsync(entity);

        return entity.ToDto();
    }

    public async Task DeleteAsync(string id, string? userId)
    {
        // Chỉ 1 lần gọi DB. Không cần Query lên trước!
        bool isSuccess = await _repository.SoftDeleteAsync(id, userId);

        if (!isSuccess)
            throw new KeyNotFoundException("Course not found or already deleted.");
    }

    public async Task<CourseDto> UpdateStatusAsync(string id, UpdateCourseStatusDto dto, string? userId)
    {
        if (!CourseStatus.Editable.Contains(dto.Status))
            throw new ArgumentException("Invalid status.");

        
        bool isSuccess = await _repository.UpdateStatusAsync(id, dto.Status, userId);

        if (!isSuccess)
            throw new KeyNotFoundException("Course not found or already deleted.");

        
        var updatedEntity = await _repository.GetByIdAsync(id);
        return updatedEntity!.ToDto();
    }

    private async Task<Course> GetCourseOrThrow(string id)
    {
        var entity = await _repository.GetByIdAsync(id);

        if (entity == null)
            throw new KeyNotFoundException("Course not found.");

        return entity;
    }

    private static void ValidateExamMatrix(List<ExamMatrixItemDto>? matrix)
    {
        if (matrix == null || matrix.Count == 0)
            return;

        var difficulties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in matrix)
        {
            if (!new[] { "Easy", "Medium", "Hard" }
                .Contains(item.Difficulty))
            {
                throw new ArgumentException(
                    $"Difficulty '{item.Difficulty}' is invalid.");
            }

            if (!difficulties.Add(item.Difficulty))
            {
                throw new ArgumentException(
                    $"Duplicate difficulty '{item.Difficulty}'.");
            }

            if (item.Count <= 0)
            {
                throw new ArgumentException(
                    $"Question count of '{item.Difficulty}' must be greater than zero.");
            }

            if (item.PointsPerQuestion <= 0)
            {
                throw new ArgumentException(
                    $"PointsPerQuestion of '{item.Difficulty}' must be greater than zero.");
            }
        }
    }
}