using Application.DTOs;
using Domain.Entities;

namespace Application.Mappers;

public static class CourseMapper
{
    public static CourseDto ToDto(this Course course)
    {
        return new CourseDto
        {
            Id = course.Id,
            Code = course.Code,
            Name = course.Name,
            Description = course.Description,
            Status = course.Status,

            CreatedBy = course.CreatedBy,
            CreatedAt = course.CreatedAt,
            LastModifiedBy = course.LastModifiedBy,
            LastModifiedAt = course.LastModifiedAt,

            ExamMatrix = course.ExamMatrix?
                .Select(x => new ExamMatrixItemDto
                {
                    Difficulty = x.Difficulty,
                    Count = x.Count,
                    PointsPerQuestion = x.PointsPerQuestion
                })
                .ToList() ?? new()
        };
    }

    public static CourseListItemDto ToListDto(this Course course)
    {
        return new CourseListItemDto
        {
            Id = course.Id,
            Code = course.Code,
            Name = course.Name,
            Status = course.Status
        };
    }

    public static Course ToEntity(this CreateCourseDto dto, string? createdBy)
    {
        return new Course
        {
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),

            ExamMatrix = dto.ExamMatrix?
                .Select(x => new ExamMatrixItem
                {
                    Difficulty = x.Difficulty,
                    Count = x.Count,
                    PointsPerQuestion = x.PointsPerQuestion
                })
                .ToList(),

            Status = "Inactive",

            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static void UpdateEntity(this UpdateCourseDto dto, Course course, string? modifiedBy)
    {
        if (dto.Name != null)
            course.Name = dto.Name.Trim();

        if (dto.Description != null)
            course.Description = dto.Description.Trim();

        if (dto.ExamMatrix != null)
        {
            course.ExamMatrix = dto.ExamMatrix
                .Select(x => new ExamMatrixItem
                {
                    Difficulty = x.Difficulty,
                    Count = x.Count,
                    PointsPerQuestion = x.PointsPerQuestion
                })
                .ToList();
        }

        course.LastModifiedBy = modifiedBy;
        course.LastModifiedAt = DateTime.UtcNow;
    }
}