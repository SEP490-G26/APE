using System.Collections.Concurrent;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Infrastructure.Persistence;

public sealed class InMemoryModuleRepository : IModuleRepository
{
    private readonly ConcurrentDictionary<string, StoredDocument> _documents = new();
    private readonly ConcurrentDictionary<string, (IReadOnlyList<GeneratedQuestion> Questions, ReviewDecision Review)> _generations = new();
    private readonly ConcurrentDictionary<string, (IReadOnlyList<FeQuestionRecord> Questions, ReviewDecision Review)> _feGenerations = new();
    private readonly ConcurrentDictionary<string, (IReadOnlyList<PeQuestionRecord> Questions, ReviewDecision Review)> _peGenerations = new();
    private readonly ConcurrentDictionary<string, MentorFeedback> _mentorFeedback = new();

    public Task SaveDocumentAsync(StoredDocument document, CancellationToken cancellationToken)
    {
        _documents[document.DocumentId] = document;
        return Task.CompletedTask;
    }

    public Task<StoredDocument?> GetDocumentAsync(string documentId, CancellationToken cancellationToken)
    {
        _documents.TryGetValue(documentId, out var document);
        return Task.FromResult(document);
    }

    public Task SaveGenerationAsync(string documentId, IReadOnlyList<GeneratedQuestion> questions, ReviewDecision review, CancellationToken cancellationToken)
    {
        _generations[documentId] = (questions, review);
        return Task.CompletedTask;
    }

    public Task SaveFeQuestionsAsync(string generationId, IReadOnlyList<FeQuestionRecord> questions, ReviewDecision review, CancellationToken cancellationToken)
    {
        _feGenerations[generationId] = (questions, review);
        return Task.CompletedTask;
    }

    public Task SavePeQuestionsAsync(string generationId, IReadOnlyList<PeQuestionRecord> questions, ReviewDecision review, CancellationToken cancellationToken)
    {
        _peGenerations[generationId] = (questions, review);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<QuestionDuplicateCandidate>> FindQuestionDuplicateCandidatesAsync(
        string? courseId,
        string questionType,
        string title,
        string description,
        IReadOnlyList<string> topicTags,
        CancellationToken cancellationToken)
    {
        var normalizedType = questionType.Trim().ToUpperInvariant();
        var candidates = normalizedType == "FE"
            ? FindFeDuplicates(courseId, title, description, topicTags)
            : FindPeDuplicates(courseId, title, description, topicTags);
        return Task.FromResult<IReadOnlyList<QuestionDuplicateCandidate>>(candidates);
    }

    public Task SaveMentorFeedbackAsync(string submissionId, MentorFeedback feedback, CancellationToken cancellationToken)
    {
        _mentorFeedback[submissionId] = feedback;
        return Task.CompletedTask;
    }

    private List<QuestionDuplicateCandidate> FindFeDuplicates(string? courseId, string title, string description, IReadOnlyList<string> topicTags)
    {
        var results = new List<QuestionDuplicateCandidate>();
        foreach (var (_, bucket) in _feGenerations)
        {
            foreach (var question in bucket.Questions)
            {
                if (!MatchesCourse(courseId, question.CourseId))
                {
                    continue;
                }

                var similarity = CalculateSimilarity(title, description, topicTags, question.Title, question.Description, question.TopicTags);
                if (similarity >= 0.85m)
                {
                    results.Add(new QuestionDuplicateCandidate(
                        question.Id,
                        "FE",
                        question.Title,
                        question.Description,
                        question.TopicTags,
                        similarity));
                }
            }
        }

        return results.OrderByDescending(static item => item.SimilarityScore).Take(5).ToList();
    }

    private List<QuestionDuplicateCandidate> FindPeDuplicates(string? courseId, string title, string description, IReadOnlyList<string> topicTags)
    {
        var results = new List<QuestionDuplicateCandidate>();
        foreach (var (_, bucket) in _peGenerations)
        {
            foreach (var question in bucket.Questions)
            {
                if (!MatchesCourse(courseId, question.CourseId))
                {
                    continue;
                }

                var similarity = CalculateSimilarity(title, description, topicTags, question.Title, question.Description, question.TopicTags);
                if (similarity >= 0.85m)
                {
                    results.Add(new QuestionDuplicateCandidate(
                        question.Id,
                        "PE",
                        question.Title,
                        question.Description,
                        question.TopicTags,
                        similarity));
                }
            }
        }

        return results.OrderByDescending(static item => item.SimilarityScore).Take(5).ToList();
    }

    private static bool MatchesCourse(string? requestedCourseId, string? existingCourseId)
    {
        if (string.IsNullOrWhiteSpace(requestedCourseId))
        {
            return true;
        }

        return string.Equals(requestedCourseId, existingCourseId, StringComparison.OrdinalIgnoreCase);
    }

    private static decimal CalculateSimilarity(
        string titleA,
        string descriptionA,
        IReadOnlyList<string> tagsA,
        string titleB,
        string descriptionB,
        IReadOnlyList<string> tagsB)
    {
        var titleScore = Jaccard(Tokenize(titleA), Tokenize(titleB));
        var descriptionScore = Jaccard(Tokenize(descriptionA), Tokenize(descriptionB));
        var tagScore = Jaccard(tagsA.Select(static tag => tag.Trim().ToLowerInvariant()).ToHashSet(), tagsB.Select(static tag => tag.Trim().ToLowerInvariant()).ToHashSet());
        return Math.Round((titleScore * 0.45m) + (descriptionScore * 0.40m) + (tagScore * 0.15m), 4);
    }

    private static HashSet<string> Tokenize(string value)
    {
        var normalized = new string(value
            .ToLowerInvariant()
            .Where(static ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            .ToArray());
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static decimal Jaccard(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 && right.Count == 0)
        {
            return 1m;
        }

        if (left.Count == 0 || right.Count == 0)
        {
            return 0m;
        }

        var intersection = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Count();
        var union = left.Union(right, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0m : decimal.Round((decimal)intersection / union, 4);
    }
}
