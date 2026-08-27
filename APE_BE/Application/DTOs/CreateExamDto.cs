namespace Application.DTOs;

public class CreateExamDto
{
    public string Title { get; set; } = null!;
    public string CourseId { get; set; } = null!;
    public string? ExamType { get; set; }
    public string PresetScope { get; set; } = "Course";
    public string Mode { get; set; } = "MockTest"; // MockTest, PracticePlaylist
    public int? TimeLimit { get; set; }
    public string CreatedBy { get; set; } = null!;
    public string Visibility { get; set; } = "Public";
    public List<string> FeExamQuestions { get; set; } = new();
    public List<PeExamQuestionDto> PeExamQuestions { get; set; } = new();
}

public class PeExamQuestionDto
{
    public string PeQuestionId { get; set; } = null!;
    public double AssignedPoints { get; set; }
}

public class ExamDto
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string CourseId { get; set; } = null!;
    public string ExamType { get; set; } = null!;
    public string PresetScope { get; set; } = "Course";
    public string Mode { get; set; } = null!;
    public int? TimeLimit { get; set; }
    public string CreatedBy { get; set; } = null!;
    public string Visibility { get; set; } = null!;
    public int FEQuestionCount { get; set; }
    public int PEQuestionCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool HasActiveSession { get; set; }
    public string? ActiveSessionId { get; set; }
    public bool ActiveSessionPaused { get; set; }
    public int ActiveSessionElapsedSeconds { get; set; }
    public int ActiveDraftCodeCount { get; set; }
    public string? ActiveSelectedQuestionId { get; set; }
}
