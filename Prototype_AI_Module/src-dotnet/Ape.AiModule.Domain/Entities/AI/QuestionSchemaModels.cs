namespace Ape.AiModule.Domain.Entities.AI;

public sealed record QuestionValidationIssue(
    string Severity,
    string Code,
    string Message,
    string? QuestionTitle);

public sealed record FeQuestionDocument(
    string Type,
    string Subject,
    string Difficulty,
    IReadOnlyList<string> TopicTags,
    string Title,
    string Description,
    IReadOnlyList<string> SourceChunkIds,
    IReadOnlyList<string> Options,
    IReadOnlyList<string> CorrectAnswer,
    string? Explanation);

public sealed record PeQuestionDocument(
    string Type,
    string Subject,
    string Difficulty,
    IReadOnlyList<string> TopicTags,
    string Title,
    string Description,
    IReadOnlyList<string> SourceChunkIds,
    IReadOnlyList<CodeFile> SkeletonCode,
    IReadOnlyList<CodeFile> SolutionCode,
    IReadOnlyList<QuestionTestCase> TestCases);

public sealed record QuestionSchemaMappingResult(
    string Subject,
    string QuestionType,
    bool IsValid,
    int InputQuestionCount,
    int ValidQuestionCount,
    IReadOnlyList<QuestionValidationIssue> Issues,
    IReadOnlyList<FeQuestionDocument> FeQuestions,
    IReadOnlyList<PeQuestionDocument> PeQuestions);
