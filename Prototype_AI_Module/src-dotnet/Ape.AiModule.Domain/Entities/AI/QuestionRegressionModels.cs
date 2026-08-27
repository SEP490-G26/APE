namespace Ape.AiModule.Domain.Entities.AI;

public sealed record QuestionRegressionCaseResult(
    string CaseId,
    string Subject,
    string QuestionType,
    string Difficulty,
    string Outcome,
    bool SchemaValid,
    bool DifficultyAligned,
    bool DuplicateDetected,
    IReadOnlyList<string> Issues,
    decimal ReviewScore,
    TokenCostBreakdown Totals);

public sealed record QuestionRegressionRunResult(
    string DatasetId,
    string Subject,
    string QuestionType,
    string GeneratorModel,
    string ReviewerModel,
    int TotalCases,
    int AcceptedCases,
    int RejectedCases,
    IReadOnlyList<QuestionRegressionCaseResult> Cases,
    TokenCostBreakdown Totals,
    DateTimeOffset CreatedAt);
