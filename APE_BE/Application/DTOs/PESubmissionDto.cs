namespace Application.DTOs;

public class PESubmissionResultDto
{
    public string Id { get; set; } = null!;
    public string QuestionId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public int LanguageId { get; set; }
    public DateTime SubmitTime { get; set; }
    public string Status { get; set; } = null!;
    public double? EarnedScore { get; set; }
    public double MaxScore { get; set; }
    public int? TestCasesPassed { get; set; }
    public int? TotalTestCases { get; set; }
    public string? CompileError { get; set; }
    public int? RuntimeMs { get; set; }
    public int? MemoryKb { get; set; }
    public int AttemptCount { get; set; }
    public string Mode { get; set; } = string.Empty;
    public List<string> TopicTags { get; set; } = new();
    public List<CodeFileDto> SubmittedCode { get; set; } = new();
    public List<TestResultItemDto> TestCaseResults { get; set; } = new();
}

public class TestResultItemDto
{
    public string? Input { get; set; } // null nếu hidden
    public string? ExpectedOutput { get; set; }
    public string? ActualOutput { get; set; }
    public bool Passed { get; set; }
    public bool IsHidden { get; set; }
    public string? Error { get; set; }
    public int TimeMs { get; set; }
    public int MemoryKb { get; set; }
}
