using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Application.Services.AI;

public sealed class DifficultyAlignmentService : IDifficultyAlignmentService
{
    public DifficultyAlignmentResult Evaluate(
        string subject,
        string questionType,
        string requestedDifficulty,
        IReadOnlyList<GeneratedQuestion> questions)
    {
        var normalizedRequested = NormalizeDifficulty(requestedDifficulty);
        var signals = new List<string>();
        var score = 0;

        foreach (var question in questions)
        {
            score += questionType.Equals("PE", StringComparison.OrdinalIgnoreCase)
                ? ScorePeQuestion(question, signals)
                : ScoreFeQuestion(question, signals);
        }

        var averageScore = questions.Count == 0 ? 0 : score / questions.Count;
        var estimatedDifficulty = averageScore switch
        {
            <= 2 => "Easy",
            <= 5 => "Medium",
            _ => "Hard"
        };

        var aligned = string.Equals(normalizedRequested, estimatedDifficulty, StringComparison.OrdinalIgnoreCase);
        var confidence = aligned ? 0.85m : 0.65m;

        return new DifficultyAlignmentResult(
            normalizedRequested,
            estimatedDifficulty,
            aligned,
            signals.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            confidence);
    }

    private static int ScoreFeQuestion(GeneratedQuestion question, List<string> signals)
    {
        var score = 0;
        var descriptionLength = question.Description?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        var optionCount = question.Options?.Count ?? 0;
        var explanationLength = question.Explanation?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;

        if (descriptionLength > 35)
        {
            score += 2;
            signals.Add("FE description is long enough to suggest multi-step reasoning.");
        }

        if (optionCount >= 4)
        {
            score += 1;
        }

        if (explanationLength > 20)
        {
            score += 1;
            signals.Add("FE explanation suggests deeper reasoning demand.");
        }

        if (ContainsComplexKeywords(question.Description, ["trace", "predict", "inheritance", "pointer", "polymorphism", "complexity"]))
        {
            score += 2;
            signals.Add("FE question contains complexity or tracing keywords.");
        }

        return score;
    }

    private static int ScorePeQuestion(GeneratedQuestion question, List<string> signals)
    {
        var score = 0;
        var descriptionLength = question.Description?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        var hiddenTests = question.TestCases?.Count(static testCase => testCase.IsHidden) ?? 0;
        var totalTests = question.TestCases?.Count ?? 0;
        var codeLines = question.SolutionCode?.Sum(static file => file.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length) ?? 0;

        if (descriptionLength > 40)
        {
            score += 2;
            signals.Add("PE description is long enough to suggest a richer implementation task.");
        }

        if (hiddenTests > 0)
        {
            score += 2;
            signals.Add("PE question includes hidden tests.");
        }

        if (totalTests >= 3)
        {
            score += 1;
        }

        if (codeLines > 18)
        {
            score += 2;
            signals.Add("PE solution length suggests medium-to-hard implementation effort.");
        }

        if (ContainsComplexKeywords(question.Description, ["edge case", "optimize", "design", "multiple", "state", "algorithm"]))
        {
            score += 2;
            signals.Add("PE statement contains algorithmic or multi-step implementation keywords.");
        }

        return score;
    }

    private static bool ContainsComplexKeywords(string? text, IEnumerable<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeDifficulty(string? difficulty)
        => difficulty?.Trim().ToLowerInvariant() switch
        {
            "easy" => "Easy",
            "hard" => "Hard",
            _ => "Medium"
        };
}
