namespace Application.DTOs;

public sealed class SaveAdminPracticeSetupRequestDto
{
    public string? Title { get; set; }
    public string PresetScope { get; set; } = "Course";
    public string CourseId { get; set; } = string.Empty;
    public string ExamType { get; set; } = "FE";
    public string Mode { get; set; } = "Practice";
    public List<string> TopicTags { get; set; } = new();
    public List<string> Difficulties { get; set; } = new();
    public List<DifficultyCountDto> FeDifficultyCounts { get; set; } = new();
    public List<string> PeQuestionIds { get; set; } = new();
}
