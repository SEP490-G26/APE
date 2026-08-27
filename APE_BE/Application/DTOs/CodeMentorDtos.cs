using Domain.Entities;

namespace Application.DTOs;

public class CodeMentorRequestDto
{
    public AIRuntimeOverride? RuntimeOverride { get; set; }
}

public class CodeMentorResultDto
{
    public string SubmissionId { get; set; } = string.Empty;
    public MentorFeedbackDto Feedback { get; set; } = new();
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ConfiguredModel { get; set; } = string.Empty;
    public string EffectiveModel { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal CostUsd { get; set; }
    public decimal ReportedCostUsd { get; set; }
    public decimal UsdToVndRate { get; set; }
    public long ChargedVnd { get; set; }
    public long ActualDeductedVnd { get; set; }
    public long AbsorbedVnd { get; set; }
    public long RemainingBalanceVnd { get; set; }
    public string UsageSource { get; set; } = string.Empty;
    public string CostSource { get; set; } = string.Empty;
    public bool FallbackUsed { get; set; }
    public string? FallbackFromProvider { get; set; }
    public string? FallbackFromModel { get; set; }
    public string? FallbackReasonCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MentorFeedbackDto
{
    public string SubmissionId { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "PE";
    public string Verdict { get; set; } = "needs_fix";
    public MentorQualityScore? QualityScore { get; set; }
    public MentorPerformanceSummary? PerformanceSummary { get; set; }
    public List<MentorErrorAnalysisItem> ErrorAnalysis { get; set; } = new();
    public List<MentorImprovementSuggestion> ImprovementSuggestions { get; set; } = new();
    public List<string> IssueCategories { get; set; } = new();
    public string? FeedbackText { get; set; }
    public string? SuggestedComplexity { get; set; }
    public string? ModelName { get; set; }
    public long ChargedVnd { get; set; }
    public long ActualDeductedVnd { get; set; }
    public long AbsorbedVnd { get; set; }
    public long RemainingBalanceVnd { get; set; }
    public decimal ReportedCostUsd { get; set; }
    public decimal UsdToVndRate { get; set; }
    public string? BillingStatus { get; set; }
    public bool FallbackUsed { get; set; }
    public DateTime Date { get; set; }
    public DateTime CreatedAt { get; set; }
}
