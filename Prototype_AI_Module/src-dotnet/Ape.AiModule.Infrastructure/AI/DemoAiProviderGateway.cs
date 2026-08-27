using System.Security.Cryptography;
using System.Text;
using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class DemoAiProviderGateway : IAiProviderGateway
{
    public TokenCostBreakdown BuildCostFromUsage(string stageName, string modelName, long inputTokens, long outputTokens, decimal latencyMs)
    {
        const decimal inputRatePerToken = 0.00000015m;
        const decimal outputRatePerToken = 0.0000006m;
        var inputCost = decimal.Round(inputTokens * inputRatePerToken, 6);
        var outputCost = decimal.Round(outputTokens * outputRatePerToken, 6);
        var totalCost = inputCost + outputCost;
        return new TokenCostBreakdown(
            inputTokens,
            outputTokens,
            inputCost,
            outputCost,
            totalCost,
            latencyMs,
            new UsageCapture("estimated", null, null, null, null),
            null);
    }

    public TokenCostBreakdown? ConsumeUsageSnapshot(string usageKey)
        => null;

    public NormalizedError? ConsumeErrorSnapshot(string usageKey)
        => null;

    public Task<GatekeeperVerdict> EvaluateGatekeeperAsync(GatekeeperRequest request, CancellationToken cancellationToken)
    {
        var cKeywords = new[] { "pointer", "scanf", "printf", "c language", "array", "loop", "function" };
        var javaOopKeywords = new[] { "java", "class", "object", "constructor", "encapsulation", "inheritance", "polymorphism" };
        var dsaJavaKeywords = new[] { "stack", "queue", "linked list", "tree", "graph", "sort", "search", "algorithm" };

        var matches = new List<string>();
        var detectedTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var content = $"{request.Subject}\n{request.RawContent}";

        AddMatches("C", cKeywords);
        AddMatches("JAVA_OOP", javaOopKeywords);
        AddMatches("DSA_JAVA", dsaJavaKeywords);

        var verdict = matches.Count switch
        {
            0 => "unsupported",
            1 => "supported",
            _ => "ambiguous"
        };

        var primaryDomain = matches.Count == 1 ? matches[0] : matches.FirstOrDefault() ?? "UNKNOWN";
        var isSupported = verdict == "supported";
        var confidence = verdict switch
        {
            "supported" => 0.86m,
            "ambiguous" => 0.52m,
            _ => 0.18m
        };

        var reason = verdict switch
        {
            "supported" => $"The document matches the supported subject whitelist for {primaryDomain}.",
            "ambiguous" => $"The document shows signals from multiple supported subject areas: {string.Join(", ", matches)}.",
            _ => "The document does not provide strong evidence that it belongs to the supported whitelist subjects."
        };

        var rejectionReasonCode = verdict switch
        {
            "unsupported" => "out_of_whitelist_scope",
            "ambiguous" => "mixed_supported_signals",
            _ => null
        };

        return Task.FromResult(new GatekeeperVerdict(
            isSupported,
            verdict,
            primaryDomain,
            matches,
            confidence,
            reason,
            rejectionReasonCode,
            detectedTopics.ToList(),
            request.Model));

        void AddMatches(string subjectCode, IEnumerable<string> keywords)
        {
            var found = keywords.Where(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (found.Count == 0)
            {
                return;
            }

            matches.Add(subjectCode);
            foreach (var item in found)
            {
                detectedTopics.Add(item);
            }
        }
    }

    public Task<IReadOnlyList<float>> CreateEmbeddingAsync(string content, string model, CancellationToken cancellationToken)
    {
        var vector = BuildVector(content, 16);
        return Task.FromResult<IReadOnlyList<float>>(vector);
    }

    public Task<IReadOnlyList<string>> CreateTagsAsync(string content, string subject, string language, IReadOnlyList<string> allowedTags, string model, CancellationToken cancellationToken)
    {
        var picked = allowedTags
            .Where(tag => content.Contains(tag, StringComparison.OrdinalIgnoreCase) || subject.Contains(tag, StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToList();

        if (picked.Count == 0)
        {
            picked = allowedTags.Take(3).ToList();
        }

        if (!picked.Contains(language, StringComparer.OrdinalIgnoreCase))
        {
            picked.Add(language.ToLowerInvariant());
        }

        return Task.FromResult<IReadOnlyList<string>>(picked);
    }

    public Task<string> DescribeImageAsync(string imageReference, string model, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Image '{imageReference}' appears to contain educational slide content with text and diagrams.");
    }

    public Task<IReadOnlyList<GeneratedQuestion>> GenerateQuestionsAsync(QuestionGenerationRequest request, CancellationToken cancellationToken)
    {
        if (request.Chunks is null || request.Chunks.Count == 0)
        {
            throw new InvalidOperationException("Question generation requires at least one chunk.");
        }

        var questions = new List<GeneratedQuestion>();
        for (var i = 0; i < request.Count; i++)
        {
            var baseChunk = request.Chunks[i % request.Chunks.Count];
            var tags = baseChunk.TopicTags.Take(3).ToList();
            if (request.QuestionType.Equals("FE", StringComparison.OrdinalIgnoreCase))
            {
                questions.Add(new GeneratedQuestion(
                    "FE",
                    tags,
                    request.Difficulty,
                    $"FE Question {i + 1}: {tags.FirstOrDefault() ?? request.Subject}",
                    $"Based on the chunk content, select the best explanation for {tags.FirstOrDefault() ?? request.Subject}.",
                    new[] { baseChunk.ChunkId },
                    null,
                    null,
                    null,
                    new[]
                    {
                        $"A. Correct statement about {tags.FirstOrDefault() ?? request.Subject}",
                        "B. Distractor option 1",
                        "C. Distractor option 2",
                        "D. Distractor option 3"
                    },
                    new[] { "A. Correct statement about " + (tags.FirstOrDefault() ?? request.Subject) },
                    "A is marked as the correct answer in this demo output."));
            }
            else
            {
                questions.Add(new GeneratedQuestion(
                    "PE",
                    tags,
                    request.Difficulty,
                    $"PE Question {i + 1}: {tags.FirstOrDefault() ?? request.Subject}",
                    $"Write a program related to {tags.FirstOrDefault() ?? request.Subject} using the supplied specification.",
                    new[] { baseChunk.ChunkId },
                    new[]
                    {
                        new CodeFile(request.Subject.Contains("java", StringComparison.OrdinalIgnoreCase) ? "Main.java" : "main.c", BuildSkeleton(request.Subject), false)
                    },
                    new[]
                    {
                        new CodeFile(request.Subject.Contains("java", StringComparison.OrdinalIgnoreCase) ? "Main.java" : "main.c", BuildSolution(request.Subject))
                    },
                    new[]
                    {
                        new QuestionTestCase("sample input 1", "sample output 1", false),
                        new QuestionTestCase("hidden input 2", "hidden output 2", true)
                    },
                    null,
                    null,
                    null));
            }
        }

        return Task.FromResult<IReadOnlyList<GeneratedQuestion>>(questions);
    }

    public Task<ReviewDecision> ReviewQuestionsAsync(QuestionReviewRequest request, CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        var suggestions = new List<string>();

        if (request.Questions.Any(question => question.TopicTags is null || question.TopicTags.Count == 0))
        {
            issues.Add("Some questions are missing topic tags.");
        }

        if (request.Questions.Any(question => string.IsNullOrWhiteSpace(question.Description)))
        {
            issues.Add("Some questions are missing problem descriptions.");
        }

        if (issues.Count == 0)
        {
            suggestions.Add("Question set is structurally valid for benchmark execution.");
            suggestions.Add("Proceed to manual academic validation before production publishing.");
        }
        else
        {
            suggestions.Add("Regenerate the questions after fixing the structural issues.");
        }

        return Task.FromResult(new ReviewDecision(
            issues.Count == 0 ? "accepted" : "needs_revision",
            issues,
            suggestions,
            issues.Count == 0 ? 0.9m : 0.55m,
            true,
            issues.Count == 0,
            issues.Count > 0,
            request.ReviewerModel));
    }

    public Task<MentorFeedback> MentorCodeAsync(CodeMentorRequest request, CancellationToken cancellationToken)
    {
        var combinedCode = CombineSourceFiles(request.SourceFiles, request.Code);
        var issues = new List<string>();
        var suggestions = new List<string>();

        if (!combinedCode.Contains("return", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("The submission may be missing an explicit return path.");
        }

        if (combinedCode.Contains("TODO", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("The submission still contains TODO markers.");
        }

        if (combinedCode.Contains("for", StringComparison.OrdinalIgnoreCase) || combinedCode.Contains("while", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Looping constructs were detected; verify boundary conditions and off-by-one handling.");
        }

        suggestions.Add("Compare the current logic against the problem statement and validate the sample cases.");

        var categories = ClassifyIssueCategories(issues, combinedCode);
        var scenarios = BuildFailingScenarios(issues, request.Problem, request.Language);
        var confidence = issues.Count == 0 ? 0.83m : 0.71m;
        var complexity = combinedCode.Contains("for", StringComparison.OrdinalIgnoreCase) ? "Likely O(n)" : "Not enough information";
        var score = Math.Round(confidence * 10m, 2);

        return Task.FromResult(new MentorFeedback(
            "PE",
            issues.Count == 0 ? "acceptable_with_minor_notes" : "needs_fix",
            new MentorQualityScore(score, score, Math.Max(4m, score - 0.8m), Math.Max(4m, score - 0.5m), Math.Max(4m, score - 1.0m), confidence),
            new MentorPerformanceSummary(
                issues.Count == 0
                    ? "The submission appears broadly aligned with the problem based on static review."
                    : "The submission shows issues that likely affect correctness or robustness based on static review.",
                complexity,
                null,
                ["This demo review is based on static code reading only."]),
            issues.Select((issue, index) => new MentorErrorAnalysisItem(
                categories.ElementAtOrDefault(index) ?? categories.FirstOrDefault() ?? "logic",
                "medium",
                issue.Length > 96 ? issue[..96].TrimEnd() : issue,
                issue,
                scenarios)).ToList(),
            suggestions.Select(text => new MentorImprovementSuggestion(
                "medium",
                text.Length > 96 ? text[..96].TrimEnd() : text,
                text,
                null)).ToList(),
            categories,
            string.Join(" ", new[]
            {
                issues.Count == 0 ? "Static review found no obvious major defect." : "Static review found issues that should be fixed.",
                suggestions.FirstOrDefault()
            }.Where(static item => !string.IsNullOrWhiteSpace(item))),
            complexity,
            request.MentorModel));
    }

    public TokenCostBreakdown EstimateCost(string stageName, string modelName, int inputSize, int outputSize)
    {
        var inputTokens = EstimateTokens(inputSize);
        var outputTokens = EstimateTokens(outputSize);
        var latency = Math.Max(120, (inputTokens + outputTokens) * 1.8m);
        return BuildCostFromUsage(stageName, modelName, inputTokens, outputTokens, latency);
    }

    private static List<float> BuildVector(string content, int dimensions)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        var vector = new List<float>(dimensions);

        for (var i = 0; i < dimensions; i++)
        {
            var byteValue = hash[i % hash.Length];
            vector.Add((byteValue - 128) / 128f);
        }

        return vector;
    }

    private static long EstimateTokens(int size)
        => Math.Max(1, (long)Math.Ceiling(size / 4.0));

    private static string BuildSkeleton(string subject)
        => subject.Contains("java", StringComparison.OrdinalIgnoreCase)
            ? "public class Main {\n    public static void main(String[] args) {\n        // TODO: implement\n    }\n}"
            : "#include <stdio.h>\n\nint main(void) {\n    // TODO: implement\n    return 0;\n}";

    private static string BuildSolution(string subject)
        => subject.Contains("java", StringComparison.OrdinalIgnoreCase)
            ? "public class Main {\n    public static void main(String[] args) {\n        System.out.println(\"demo solution\");\n    }\n}"
            : "#include <stdio.h>\n\nint main(void) {\n    printf(\"demo solution\\n\");\n    return 0;\n}";

    private static IReadOnlyList<string> ClassifyIssueCategories(IReadOnlyList<string> issues, string code)
    {
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in issues)
        {
            var lowered = issue.ToLowerInvariant();
            if (lowered.Contains("return") || lowered.Contains("todo"))
            {
                categories.Add("logic");
            }
        }

        if (code.Contains("for", StringComparison.OrdinalIgnoreCase) || code.Contains("while", StringComparison.OrdinalIgnoreCase))
        {
            categories.Add("complexity");
        }

        if (categories.Count == 0)
        {
            categories.Add("style");
        }

        return categories.ToList();
    }

    private static IReadOnlyList<string> BuildFailingScenarios(IReadOnlyList<string> issues, string problem, string language)
    {
        if (issues.Count == 0)
        {
            return [];
        }

        return
        [
            $"Review edge inputs for problem '{problem}' in {language}.",
            "Validate one minimal case and one boundary case."
        ];
    }

    private static string CombineSourceFiles(IReadOnlyList<CodeFile>? sourceFiles, string? fallbackCode)
    {
        if (sourceFiles is null || sourceFiles.Count == 0)
        {
            return fallbackCode ?? string.Empty;
        }

        return string.Join(
            "\n\n",
            sourceFiles.Select(static file =>
                $"// FILE: {file.FileName}\n{file.Content}".Trim()));
    }
}
