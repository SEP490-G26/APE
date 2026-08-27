using Domain.Entities;

namespace Application.DTOs;

public class QuestionSummaryDto
{
    public string Id { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string Difficulty { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string CourseId { get; set; } = null!;
    public List<string> TopicTags { get; set; } = new();
    public string Source { get; set; } = "Admin";
    public string SourceScope { get; set; } = "SYSTEM";
    public string? OwnerUserId { get; set; }
    public string? QuestionFingerprint { get; set; }
    public int SourceDocumentCount { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

public class QuestionAdminDetailDto
{
    public string Id { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string CourseId { get; set; } = null!;
    public List<string> TopicTags { get; set; } = new();
    public string Difficulty { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsPublic { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string? DisabledReason { get; set; }
    public string? DisabledBy { get; set; }
    public DateTime? DisabledAt { get; set; }
    public string Source { get; set; } = "Admin";
    public string SourceScope { get; set; } = "SYSTEM";
    public string? OwnerUserId { get; set; }
    public string? QuestionFingerprint { get; set; }
    public List<string> SourceDocumentIds { get; set; } = new();
    public int SourceDocumentCount { get; set; }
    public List<string>? Options { get; set; }
    public List<string>? CorrectAnswer { get; set; }
    public string? Explanation { get; set; }
    public List<CodeFile>? SkeletonCode { get; set; }
    public List<CodeFile>? SolutionCode { get; set; }
    public List<TestCase>? TestCases { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsDraft { get; set; }
    public bool CanPublish { get; set; }
    public bool CanReject { get; set; }
    public bool CanDisable { get; set; }
    public bool CanEdit { get; set; }
    public string StatusLabel { get; set; } = null!;
}

public class QuestionAdminMutationResultDto
{
    public string Action { get; set; } = null!;
    public string Message { get; set; } = null!;
    public QuestionAdminDetailDto Question { get; set; } = new();
}

public class EditQuestionDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string>? TopicTags { get; set; }
    public string? Difficulty { get; set; }
    public List<string>? Options { get; set; }
    public List<string>? CorrectAnswer { get; set; }


    // PE specific
    public List<SubmittedCodeFileDto>? SkeletonCode { get; set; }
    public List<TestCaseDto>? TestCases { get; set; }

    

}

public class DisableQuestionDto
{
    public string Reason { get; set; } = null!;
}

public class ImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<RowError> RowErrors { get; set; } = new();
}

public class RowError
{
    public int Row { get; set; }
    public string Error { get; set; } = null!;
}

public class UpdateQuestionStatusDto
{
    public string Status { get; set; } = null!;
}

public class ToggleQuestionDisabledDto
{
    public string? Reason { get; set; }
}
