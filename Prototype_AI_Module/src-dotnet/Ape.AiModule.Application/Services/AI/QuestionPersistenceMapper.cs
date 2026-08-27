using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Application.Services.AI;

public sealed class QuestionPersistenceMapper : IQuestionPersistenceMapper
{
    public IReadOnlyList<FeQuestionRecord> MapFeQuestions(
        QuestionSchemaMappingResult schemaMapping,
        string userId,
        string pipelineRunId,
        ReviewDecision review,
        string? courseId = null,
        bool isPublic = false)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return schemaMapping.FeQuestions.Select(question => new FeQuestionRecord(
            Id: BuildQuestionId("feq"),
            CourseId: courseId,
            SourceChunkIds: question.SourceChunkIds,
            TopicTags: question.TopicTags,
            Difficulty: question.Difficulty,
            Title: question.Title,
            Description: question.Description,
            Options: question.Options,
            CorrectAnswer: question.CorrectAnswer,
            Explanation: question.Explanation,
            CreatedBy: userId,
            IsPublic: isPublic,
            ReviewStatus: review.ReviewStatus,
            PipelineRunId: pipelineRunId,
            CreatedAt: timestamp,
            UpdatedAt: timestamp)).ToList();
    }

    public IReadOnlyList<PeQuestionRecord> MapPeQuestions(
        QuestionSchemaMappingResult schemaMapping,
        string userId,
        string pipelineRunId,
        ReviewDecision review,
        string? courseId = null,
        bool isPublic = false)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return schemaMapping.PeQuestions.Select(question => new PeQuestionRecord(
            Id: BuildQuestionId("peq"),
            CourseId: courseId,
            SourceChunkIds: question.SourceChunkIds,
            TopicTags: question.TopicTags,
            Difficulty: question.Difficulty,
            Title: question.Title,
            Description: question.Description,
            SkeletonCode: question.SkeletonCode,
            SolutionCode: question.SolutionCode,
            TestCases: question.TestCases,
            CreatedBy: userId,
            IsPublic: isPublic,
            ReviewStatus: review.ReviewStatus,
            PipelineRunId: pipelineRunId,
            CreatedAt: timestamp,
            UpdatedAt: timestamp)).ToList();
    }

    private static string BuildQuestionId(string prefix)
        => $"{prefix}-{Guid.NewGuid():N}"[..27];
}
