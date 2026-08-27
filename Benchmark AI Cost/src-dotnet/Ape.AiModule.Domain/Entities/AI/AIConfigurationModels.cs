namespace Ape.AiModule.Domain.Entities.AI;

public sealed record AiPromptVersionRecord(
    string Id,
    string AgentId,
    string Version,
    bool IsActive,
    string SystemPrompt,
    IReadOnlyList<string> Variables,
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
