namespace Ape.AiModule.Domain.Entities.AI;

public sealed record FeQuestionRecord(
    string Id,
    string? CourseId,
    IReadOnlyList<string> SourceChunkIds,
    IReadOnlyList<string> TopicTags,
    string Difficulty,
    string Title,
    string Description,
    IReadOnlyList<string> Options,
    IReadOnlyList<string> CorrectAnswer,
    string? Explanation,
    string CreatedBy,
    bool IsPublic,
    string ReviewStatus,
    string PipelineRunId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PeQuestionRecord(
    string Id,
    string? CourseId,
    IReadOnlyList<string> SourceChunkIds,
    IReadOnlyList<string> TopicTags,
    string Difficulty,
    string Title,
    string Description,
    IReadOnlyList<CodeFile> SkeletonCode,
    IReadOnlyList<CodeFile> SolutionCode,
    IReadOnlyList<QuestionTestCase> TestCases,
    string CreatedBy,
    bool IsPublic,
    string ReviewStatus,
    string PipelineRunId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
