namespace Application.DTOs;

public sealed class ExamConfigDocumentDto
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
}

public sealed class ExamConfigQuestionItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public List<string> TopicTags { get; set; } = new();
}

public sealed class ExamConfigQuestionPoolDto
{
    public string CourseId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ExamType { get; set; } = string.Empty;
    public List<string> DocumentIds { get; set; } = new();
    public int AvailableCount { get; set; }
    public List<ExamConfigQuestionItemDto> Items { get; set; } = new();
}

public sealed class DifficultyCountDto
{
    public string Difficulty { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class StartConfiguredExamRequestDto
{
    public string CourseId { get; set; } = string.Empty;
    public string Source { get; set; } = "System";
    public List<string> DocumentIds { get; set; } = new();
    public string ExamType { get; set; } = string.Empty;
    public string Mode { get; set; } = "Practice";
    public List<string> TopicTags { get; set; } = new();
    public List<string> Difficulties { get; set; } = new();
    public List<DifficultyCountDto> FeDifficultyCounts { get; set; } = new();
    public int? FeQuestionCount { get; set; }
    public List<string> PeQuestionIds { get; set; } = new();
}

public sealed class StartConfiguredExamResultDto
{
    public string ExamId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public string ExamType { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public ExamDetailDto? Exam { get; set; }
}
