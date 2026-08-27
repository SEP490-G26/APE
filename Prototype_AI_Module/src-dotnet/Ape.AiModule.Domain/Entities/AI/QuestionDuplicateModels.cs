namespace Ape.AiModule.Domain.Entities.AI;

public sealed record QuestionDuplicateCandidate(
    string QuestionId,
    string QuestionType,
    string Title,
    string Description,
    IReadOnlyList<string> TopicTags,
    decimal SimilarityScore);
