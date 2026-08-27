using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class CourseDto
{
    public string Id { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public List<ExamMatrixItemDto> ExamMatrix { get; set; } = new();

    public string Status { get; set; } = null!;

    public string? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }

    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

public class CreateCourseDto
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public List<ExamMatrixItemDto>? ExamMatrix { get; set; }
}

public class UpdateCourseDto
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public List<ExamMatrixItemDto>? ExamMatrix { get; set; }
}

public class UpdateCourseStatusDto
{
    [Required]
    public string Status { get; set; } = null!;
}

public class CourseListItemDto
{
    public string Id { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
}

public class ExamMatrixItemDto
{
    [Required]
    public string Difficulty { get; set; } = null!;

    [Range(1, 1000)]
    public int Count { get; set; }

    [Range(0.1, 100)]
    public double PointsPerQuestion { get; set; }
}

public class CourseListResponseDto
{
    public List<CourseListItemDto> Items { get; set; } = new();

    public long Total { get; set; }

    public int Page { get; set; }

    public int Limit { get; set; }

    public long TotalPages { get; set; }
}