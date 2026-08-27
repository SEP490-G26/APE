using Domain.Entities;
using MongoDB.Bson;

namespace Application.DTOs;

public class AIUsageLogDto
{
    public string Id { get; set; } = string.Empty;
    public string TriggeredBy { get; set; } = string.Empty;
    public string? TriggeredByName { get; set; }
    public string? TriggeredByEmail { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string? AgentRole { get; set; }
    public double CreditsDeducted { get; set; }
    public int TokensUsed { get; set; }
    public decimal CostUsd { get; set; }
    public DateTime CreatedAt { get; set; }
    public AIUsageLogParsedPayloadDto ParsedPayload { get; set; } = new();
    public object? PayloadData { get; set; }
}

public class AIUsageLogFilterOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class AIUsageLogFilterOptionsDto
{
    public List<AIUsageLogFilterOptionDto> TriggeredBy { get; set; } = new();
    public List<string> Features { get; set; } = new();
    public List<string> Providers { get; set; } = new();
    public List<string> Models { get; set; } = new();
}

public class AIVndBillingConfigDto
{
    public decimal UsdToVndRate { get; set; }
    public long MinBalanceVnd { get; set; }
    public decimal ChargeMultiplier { get; set; }
    public string SettingName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class AIVndBillingConfigUpsertRequestDto
{
    public decimal UsdToVndRate { get; set; }
    public long MinBalanceVnd { get; set; }
    public decimal ChargeMultiplier { get; set; }
}

public class AIVndBillingTransactionDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string FeatureKey { get; set; } = string.Empty;
    public string SourceEntityType { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ReportedCostUsd { get; set; }
    public decimal UsdToVndRate { get; set; }
    public decimal ChargeMultiplier { get; set; }
    public long MinimumBalanceVnd { get; set; }
    public long ActualCostVnd { get; set; }
    public long ChargedVnd { get; set; }
    public long ActualDeductedVnd { get; set; }
    public long AbsorbedVnd { get; set; }
    public long RefundedVnd { get; set; }
    public long BalanceBeforeVnd { get; set; }
    public long BalanceAfterVnd { get; set; }
    public string? RefundReason { get; set; }
    public List<string> UsageLogIds { get; set; } = new();
    public string PolicySettingName { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public object? PolicySnapshot { get; set; }
    public object? UsageSnapshot { get; set; }
    public object? ChargeBreakdown { get; set; }
}

public class AIVndBillingTransactionSummaryItemDto
{
    public string FeatureKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public int DistinctUserCount { get; set; }
    public decimal TotalReportedCostUsd { get; set; }
    public long TotalActualCostVnd { get; set; }
    public long TotalChargedVnd { get; set; }
    public long TotalActualDeductedVnd { get; set; }
    public long TotalAbsorbedVnd { get; set; }
    public DateTime FirstCreatedAt { get; set; }
    public DateTime LastCreatedAt { get; set; }
}

public class AIVndBillingDailySummaryItemDto
{
    public DateTime Date { get; set; }
    public int TransactionCount { get; set; }
    public int DistinctUserCount { get; set; }
    public long TotalActualCostVnd { get; set; }
    public long TotalChargedVnd { get; set; }
    public long TotalActualDeductedVnd { get; set; }
    public long TotalAbsorbedVnd { get; set; }
}

public class RefundVndTransactionRequestDto
{
    public string RefundReason { get; set; } = string.Empty;
}

public class AIUsageLogParsedPayloadDto
{
    public string? Feature { get; set; }
    public string? Step { get; set; }
    public string? Provider { get; set; }
    public string? ConfiguredModel { get; set; }
    public string? EffectiveModel { get; set; }
    public string? NormalizedModelKey { get; set; }
    public string? UsageSource { get; set; }
    public string? CostSource { get; set; }
    public bool? FallbackUsed { get; set; }
    public string? FallbackFromProvider { get; set; }
    public string? FallbackFromModel { get; set; }
    public string? FallbackReasonCode { get; set; }
    public string? PromptKey { get; set; }
    public string? PromptVersion { get; set; }
    public string? PolicyKey { get; set; }
    public string? PolicyVersion { get; set; }
    public string? RubricKey { get; set; }
    public string? RubricVersion { get; set; }
    public string? CourseId { get; set; }
    public string? DocumentId { get; set; }
    public string? SubmissionId { get; set; }
    public string? ContextPackId { get; set; }
    public string? QuestionType { get; set; }
    public string? Language { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public int? ChunkCount { get; set; }
    public int? FallbackCalls { get; set; }
}

public class AIUsageSummaryItemDto
{
    public string AgentId { get; set; } = string.Empty;
    public string? AgentRole { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string NormalizedModelKey { get; set; } = string.Empty;
    public string? Feature { get; set; }
    public string? Step { get; set; }
    public int CallCount { get; set; }
    public int DistinctTriggeredUsers { get; set; }
    public int TotalTokens { get; set; }
    public decimal TotalCostUsd { get; set; }
    public double TotalCreditsDeducted { get; set; }
    public int FallbackCallCount { get; set; }
    public decimal AvgTokensPerCall { get; set; }
    public decimal AvgCostUsdPerCall { get; set; }
    public decimal FallbackRate { get; set; }
    public DateTime FirstUsedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
}

public class AIArtifactUsageSummaryItemDto
{
    public string AgentId { get; set; } = string.Empty;
    public string? AgentRole { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Feature { get; set; }
    public string? Step { get; set; }
    public string? PromptKey { get; set; }
    public string? PromptVersion { get; set; }
    public string? PolicyKey { get; set; }
    public string? PolicyVersion { get; set; }
    public string? RubricKey { get; set; }
    public string? RubricVersion { get; set; }
    public int CallCount { get; set; }
    public int TotalTokens { get; set; }
    public decimal TotalCostUsd { get; set; }
    public int FallbackCallCount { get; set; }
    public decimal AvgTokensPerCall { get; set; }
    public decimal AvgCostUsdPerCall { get; set; }
    public decimal FallbackRate { get; set; }
    public DateTime FirstUsedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
}

public class AIUserUsageSummaryItemDto
{
    public string TriggeredBy { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string NormalizedModelKey { get; set; } = string.Empty;
    public string? Feature { get; set; }
    public string? Step { get; set; }
    public int CallCount { get; set; }
    public int TotalTokens { get; set; }
    public decimal TotalCostUsd { get; set; }
    public double TotalCreditsDeducted { get; set; }
    public int FallbackCallCount { get; set; }
    public decimal AvgTokensPerCall { get; set; }
    public decimal AvgCostUsdPerCall { get; set; }
    public decimal FallbackRate { get; set; }
    public DateTime FirstUsedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
}

public class AIRuntimeHealthItemDto
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentRole { get; set; } = string.Empty;
    public bool AgentEnabled { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool PrimaryProviderEnabled { get; set; }
    public bool PrimaryProviderHasApiKey { get; set; }
    public bool PrimaryRuntimeReady { get; set; }
    public string? FallbackProvider { get; set; }
    public string? FallbackModelName { get; set; }
    public bool FallbackConfigured { get; set; }
    public bool FallbackProviderEnabled { get; set; }
    public bool FallbackProviderHasApiKey { get; set; }
    public bool FallbackRuntimeReady { get; set; }
    public string RuntimeStatus { get; set; } = string.Empty;
    public bool HasWarnings => Warnings.Count > 0;
    public List<string> Warnings { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}

public class AIMentorFeedbackAdminDto
{
    public string Id { get; set; } = string.Empty;
    public string? AgentId { get; set; }
    public string SubmissionId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string QuestionType { get; set; } = "PE";
    public string? Verdict { get; set; }
    public MentorQualityScore? QualityScore { get; set; }
    public MentorPerformanceSummary? PerformanceSummary { get; set; }
    public List<MentorErrorAnalysisItem> ErrorAnalysis { get; set; } = new();
    public List<MentorImprovementSuggestion> ImprovementSuggestions { get; set; } = new();
    public List<string> IssueCategories { get; set; } = new();
    public string? FeedbackText { get; set; }
    public string? SuggestedComplexity { get; set; }
    public string? ModelName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PublishQuestionDto
{
    public string? Reason { get; set; }
}

public class GatekeeperSmokeRequestDto
{
    public string Content { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? SubjectHint { get; set; }
    public string? Language { get; set; }
    public AIRuntimeOverride? RuntimeOverride { get; set; }
}

public class ExtractedContentSmokeRequestDto
{
    public string FileName { get; set; } = "smoke.txt";
    public string RawText { get; set; } = string.Empty;
    public string SourceType { get; set; } = "text";
    public bool IsVisionRecommended { get; set; }
    public string? ParserName { get; set; }
    public int EstimatedPageCount { get; set; } = 1;
    public List<string> DetectedImageReferences { get; set; } = new();
    public List<AIImageInput> ExtractedImages { get; set; } = new();
    public int EmbeddedImageCount { get; set; }
    public List<string> Warnings { get; set; } = new();
    public AIRuntimeOverride? RuntimeOverride { get; set; }
}

public class EmbeddingSmokeRequestDto
{
    public string? DocumentId { get; set; }
    public string? CourseId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string SourceType { get; set; } = "text";
    public AIRuntimeOverride? EmbeddingOverride { get; set; }
    public AIRuntimeOverride? TaggingOverride { get; set; }
}

public class CodeMentorSmokeRequestDto
{
    public string SubmissionId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Language { get; set; } = "java";
    public string QuestionTitle { get; set; } = string.Empty;
    public string QuestionDescription { get; set; } = string.Empty;
    public List<string> TopicTags { get; set; } = new();
    public List<CodeFile> SubmittedCode { get; set; } = new();
    public AIRuntimeOverride? RuntimeOverride { get; set; }
}

public class RetrievalSmokeRequestDto : RetrievalPlanRequestDto
{
}

public class AIProviderCredentialInputDto
{
    public bool Enabled { get; set; } = true;
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiVersion { get; set; }
    public int? TimeoutSeconds { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class AIProviderCredentialViewDto
{
    public bool Enabled { get; set; } = true;
    public bool HasApiKey { get; set; }
    public string ApiKeyMasked { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class AIProviderCredentialsUpsertRequestDto
{
    public AIProviderCredentialInputDto? OpenAI { get; set; }
    public AIProviderCredentialInputDto? DeepSeek { get; set; }
    public AIProviderCredentialInputDto? Gemini { get; set; }
    public AIProviderCredentialInputDto? Cohere { get; set; }
}

public class AIProviderCredentialsConfigDto
{
    public AIProviderCredentialViewDto? OpenAI { get; set; }
    public AIProviderCredentialViewDto? DeepSeek { get; set; }
    public AIProviderCredentialViewDto? Gemini { get; set; }
    public AIProviderCredentialViewDto? Cohere { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class AIProviderCredentialRecordDto
{
    public string Provider { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool HasApiKey { get; set; }
    public string ApiKeyMasked { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? UpdatedBy { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class AIProviderModelOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class CreateAIProviderCredentialRequestDto : AIProviderCredentialInputDto
{
    public string Provider { get; set; } = string.Empty;
}

public class UpdateAIProviderCredentialRequestDto : AIProviderCredentialInputDto
{
    public string? Provider { get; set; }
}

public class AIAgentAdminDto
{
    public string Id { get; set; } = string.Empty;
    public string AgentRole { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public double CreditCost { get; set; }
    public bool IsEnabled { get; set; }
    public string? FallbackProvider { get; set; }
    public string? FallbackModelName { get; set; }
    public bool HasFallbackConfigured { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Notes { get; set; }
}

public class UpsertAIAgentRequestDto
{
    public string Provider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
    public double CreditCost { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? FallbackProvider { get; set; }
    public string? FallbackModelName { get; set; }
    public string? Notes { get; set; }
}
