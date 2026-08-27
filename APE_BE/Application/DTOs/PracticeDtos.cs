using System.Collections.Generic;
using Domain.Enums;

namespace Application.DTOs;

public class StartSessionDto
{
    public string ExamId { get; set; } = null!;
    public string? Mode { get; set; }
    public bool ForceNew { get; set; }
}

public class StartSessionResultDto
{
    public string SessionId { get; set; } = null!;
    public DateTime StartTime { get; set; }
}

public class PracticeSessionDto
{
    public string Id { get; set; } = null!;
    public string StudentId { get; set; } = null!;
    public string ExamId { get; set; } = null!;
    public string ExamTitle { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public string ExamMode { get; set; } = string.Empty;
    public int FEQuestionCount { get; set; }
    public int PEQuestionCount { get; set; }
    public int TotalQuestionCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int ActiveDurationSeconds { get; set; }
    public DateTime? LastResumedAt { get; set; }
    public DateTime? LastPausedAt { get; set; }
    public bool IsPaused { get; set; }
    public SessionStatus Status { get; set; }
    public double? TotalScore { get; set; }
    public string? SelectedQuestionId { get; set; }
    public List<DraftCodeDto> DraftCodes { get; set; } = new();
}

public class DraftCodeDto
{
    public string QuestionId { get; set; } = null!;
    public List<SubmittedCodeFileDto> Files { get; set; } = new();
}

public class PracticeHistoryItemDto
{
    public string SessionId { get; set; } = null!;
    public string ExamId { get; set; } = null!;
    public DateTime Date { get; set; }
    public string CourseId { get; set; } = null!;
    public string ExamType { get; set; } = null!;
    public double Score { get; set; }
    public double MaxScore { get; set; }
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public int DurationSeconds { get; set; }
    public string Status { get; set; } = null!;
    public string? LatestPeSubmissionId { get; set; }
    public string? LatestPeVerdict { get; set; }
    public bool ExamDeleted { get; set; }

    // Backward compatibility fields used by existing responses
    public string ExamTitle { get; set; } = "Unknown";
    public string CourseName { get; set; } = "";
    public string Mode { get; set; } = null!;
    public double TotalScore { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public class SaveDraftCodeDto
{
    public string QuestionId { get; set; } = null!;
    public string? SelectedQuestionId { get; set; }
    public List<SubmittedCodeFileDto> Files { get; set; } = new();
}

public class ActivePracticeSessionSummaryDto
{
    public string SessionId { get; set; } = null!;
    public string ExamId { get; set; } = null!;
    public bool IsPaused { get; set; }
    public int ActiveDurationSeconds { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? LastPausedAt { get; set; }
    public string? SelectedQuestionId { get; set; }
    public int DraftCodeCount { get; set; }
}

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public long Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public long TotalPages { get; set; }
}
