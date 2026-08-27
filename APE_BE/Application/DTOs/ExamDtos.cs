using Domain.Entities;

namespace Application.DTOs;

public class ExamDetailDto
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string CourseId { get; set; } = null!;
    public string ExamType { get; set; } = null!;
    public string PresetScope { get; set; } = "Course";
    public string Mode { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public int? TimeLimit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int FEQuestionCount { get; set; }
    public int PEQuestionCount { get; set; }
    public int TotalQuestionCount { get; set; }
    public List<FEQuestionDto> FEQuestions { get; set; } = new();
    public List<PEQuestionDto> PEQuestions { get; set; } = new();
}

public class FEQuestionDto
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public List<string> Options { get; set; } = new();
    public List<string> CorrectAnswer { get; set; } = new();
    public string? Explanation { get; set; }
    public string Difficulty { get; set; } = "Medium";
    public List<string> TopicTags { get; set; } = new();
}

public class PEQuestionDto
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public List<SubmittedCodeFileDto>? SkeletonCode { get; set; }
    public List<TestCaseDto> TestCases { get; set; } = new();
}
