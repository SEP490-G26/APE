using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public class FESubmissionInputDto
{
    public string SessionId { get; set; } = string.Empty;

    public string QuestionId { get; set; } = string.Empty;

    public List<string> UserAnswer { get; set; } = new();
}

public sealed class PESubmissionInputDto
{
    [Required]
    public string SessionId { get; init; } = string.Empty;

    [Required]
    public string QuestionId { get; init; } = string.Empty;

    public int? RequestedLanguageId { get; init; }

    [Required]
    [MinLength(1)]
    public List<SubmittedCodeFileDto> Files { get; init; } = new();
}

public sealed class SubmittedCodeFileDto
{
    [Required]
    [MaxLength(255)]
    public string Filename { get; init; } = string.Empty;

    [Required]
    public string Content { get; init; } = string.Empty;
}

public class PECodeRunInputDto
{
    [Required]
    public string QuestionId { get; set; } = string.Empty;

    public string? Stdin { get; set; }

    [Required]
    [MinLength(1)]
    public List<CodeFileDto> Files { get; set; } = new();
}

public class PECodeRunResultDto
{
    public string Status { get; set; } = string.Empty;

    public string Stdout { get; set; } = string.Empty;

    public string Stderr { get; set; } = string.Empty;

    public string CompileOutput { get; set; } = string.Empty;

    public int RuntimeMs { get; set; }

    public int MemoryKb { get; set; }

    public int PassedSampleCases { get; set; }

    public int TotalSampleCases { get; set; }

    public ExecutionFeedbackStatus? ExecutionStatus { get; set; }

    public bool IsJudged { get; set; }

    public IReadOnlyCollection<ExecutionFeedbackDiagnosticDto> Diagnostics { get; set; }
        = Array.Empty<ExecutionFeedbackDiagnosticDto>();

    public List<PECodeRunCaseResultDto> SampleResults { get; set; } = new();

    public PECodeRunCaseResultDto? CustomRunResult { get; set; }
}

public class PECodeRunCaseResultDto
{
    public int Index { get; set; }

    public int Number { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool Passed { get; set; }

    public ExecutionFeedbackStatus ExecutionStatus { get; set; }

    public bool IsJudged { get; set; }

    public bool IsHidden { get; set; }

    public string Input { get; set; } = string.Empty;

    public string ExpectedOutput { get; set; } = string.Empty;

    public string ActualOutput { get; set; } = string.Empty;

    public string Stderr { get; set; } = string.Empty;

    public string CompileOutput { get; set; } = string.Empty;

    public int RuntimeMs { get; set; }

    public int MemoryKb { get; set; }

    public IReadOnlyCollection<ExecutionFeedbackDiagnosticDto> Diagnostics { get; set; }
        = Array.Empty<ExecutionFeedbackDiagnosticDto>();
}

public class CodeFileDto
{
    public string Filename { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public class FESubmissionResultDto
{
    public string Id { get; set; } = string.Empty;

    public string QuestionId { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;

    public string CourseId { get; set; } = string.Empty;

    public string QuestionType { get; set; } = "FE";

    public string Status { get; set; } = "Graded";

    public List<string> UserAnswer { get; set; } = new();

    public List<string> CorrectAnswer { get; set; } = new();

    public bool IsCorrect { get; set; }

    public double EarnedPoints { get; set; }
}

public sealed class SubmissionAcceptedDto
{
    public string SubmissionId { get; init; } = string.Empty;

    public SubmissionProcessingStatus Status { get; init; }

    public int AttemptCount { get; init; }

    public DateTime SubmittedAt { get; init; }
}

public class SubmissionSessionResultDto
{
    public string SessionId { get; set; } = string.Empty;

    public string ExamId { get; set; } = string.Empty;

    public string ExamTitle { get; set; } = string.Empty;

    public string CourseId { get; set; } = string.Empty;

    public string ExamMode { get; set; } = string.Empty;

    public int? TimeLimit { get; set; }

    public string SessionStatus { get; set; } = string.Empty;

    public int FEQuestionCount { get; set; }

    public int PEQuestionCount { get; set; }

    public int TotalQuestionCount { get; set; }

    public int FESubmissionCount { get; set; }

    public int PESubmissionCount { get; set; }

    public int TotalSubmissionCount { get; set; }

    public List<FESubmissionResultDto> FE { get; set; } = new();

    public List<PESubmissionResultDto> PE { get; set; } = new();
}

public class TestCaseDto
{
    [Required]
    public string Input { get; set; } = string.Empty;

    [Required]
    public string ExpectedOutput { get; set; } = string.Empty;

    public bool IsHidden { get; set; }
}

public sealed class PESubmissionSummaryDto
{
    public string Id { get; init; } = string.Empty;

    public string QuestionId { get; init; } = string.Empty;

    public SubmissionProcessingStatus Status { get; init; }

    public SubmissionVerdict? FinalVerdict { get; init; }

    public double QuestionScore { get; init; }

    public double MaxScore { get; init; }

    public int AttemptCount { get; init; }

    public DateTime SubmittedAt { get; init; }

    public DateTime? CompletedAt { get; init; }
}

public sealed class PESubmissionDetailDto
{
    public string Id { get; init; } = string.Empty;

    public string QuestionId { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public string? CourseId { get; init; }

    public SubmissionMode Mode { get; init; }

    public int LanguageId { get; init; }

    public double MaxScore { get; init; }

    public double QuestionScore { get; init; }

    public SubmissionProcessingStatus Status { get; init; }

    public SubmissionVerdict? FinalVerdict { get; init; }

    public int TestCasesPassed { get; init; }

    public int TotalTestCases { get; init; }

    public int RuntimeMs { get; init; }

    public int MemoryKb { get; init; }

    public int AttemptCount { get; init; }

    public DateTime SubmittedAt { get; init; }

    public DateTime? ProcessingStartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public string? ProcessingError { get; init; }

    public IReadOnlyCollection<ExecutionFeedbackDiagnosticDto> Diagnostics { get; init; }
        = Array.Empty<ExecutionFeedbackDiagnosticDto>();

    public IReadOnlyCollection<CodeFileDto> SubmittedCode { get; init; }
        = Array.Empty<CodeFileDto>();

    public IReadOnlyCollection<TestResultDto> TestResults { get; init; }
        = Array.Empty<TestResultDto>();
}

public sealed class TestResultDto
{
    public int TestCaseIndex { get; init; }

    public int Number { get; init; }

    public string Label { get; init; } = string.Empty;

    public SubmissionVerdict Verdict { get; init; }

    public ExecutionFeedbackStatus ExecutionStatus { get; init; }

    public bool IsJudged { get; init; }

    public string? Input { get; init; }

    public string? ActualOutput { get; init; }

    public string? ExpectedOutput { get; init; }

    public string? StandardError { get; init; }

    public string? CompileOutput { get; init; }

    public int RuntimeMs { get; init; }

    public int MemoryKb { get; init; }

    public bool IsHidden { get; init; }

    public IReadOnlyCollection<ExecutionFeedbackDiagnosticDto> Diagnostics { get; init; }
        = Array.Empty<ExecutionFeedbackDiagnosticDto>();
}

public enum ExecutionFeedbackStatus
{
    Passed = 0,
    Failed = 1,
    NotExecuted = 2,
    Completed = 3
}

public enum ExecutionFeedbackCategory
{
    Compilation = 0,
    Runtime = 1,
    ResourceLimit = 2,
    System = 3
}

public enum ExecutionFeedbackSeverity
{
    Error = 0
}

public sealed class ExecutionFeedbackDiagnosticDto
{
    public ExecutionFeedbackCategory Category { get; init; }

    public string Code { get; init; } = string.Empty;

    public ExecutionFeedbackSeverity Severity { get; init; } =
        ExecutionFeedbackSeverity.Error;

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? Filename { get; init; }

    public int? Line { get; init; }

    public int? Column { get; init; }

    public IReadOnlyCollection<string> Suggestions { get; init; } =
        Array.Empty<string>();

    public string? RawDetails { get; init; }
}
