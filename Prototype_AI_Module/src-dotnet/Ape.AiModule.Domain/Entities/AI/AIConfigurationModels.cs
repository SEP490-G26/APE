namespace Ape.AiModule.Domain.Entities.AI;

public sealed record AiPromptVersionRecord(
    string Id,
    string PromptKey,
    string FunctionName,
    string? SubjectCode,
    string? QuestionType,
    string? Language,
    string AgentId,
    string Version,
    bool IsActive,
    string Description,
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyList<string> Variables,
    string? ChangeSummary,
    string ContentHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AiRubricRecord(
    string Id,
    string RubricId,
    string FunctionName,
    string SubjectCode,
    string QuestionType,
    string Version,
    string Language,
    bool IsActive,
    IReadOnlyDictionary<string, object> ContentJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AiGroundTruthSetRecord(
    string Id,
    string DatasetName,
    string FunctionName,
    string SubjectCode,
    string? QuestionType,
    string Version,
    string Language,
    bool IsActive,
    IReadOnlyDictionary<string, object> ContentJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AiPolicyRecord(
    string Id,
    string PolicyId,
    string PolicyName,
    string FunctionName,
    string? SubjectCode,
    string? QuestionType,
    string? Language,
    string Version,
    bool IsActive,
    string Description,
    IReadOnlyDictionary<string, object> ContentJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
