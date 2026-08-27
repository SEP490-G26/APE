namespace Application.DTOs;

public sealed class StudentQuestionBankListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool CanPublish { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public List<string> TopicTags { get; set; } = new();
    public string CourseId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public DateTime? LastModifiedAt { get; set; }
}

public sealed class StudentQuestionBankDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool CanPublish { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public List<string> TopicTags { get; set; } = new();
    public string CourseId { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public List<string>? Options { get; set; }
    public List<string>? CorrectAnswer { get; set; }
    public string? Explanation { get; set; }
    public List<CodeFileDto> SkeletonCode { get; set; } = new();
    public List<TestCaseDto> SampleTestCases { get; set; } = new();
}
