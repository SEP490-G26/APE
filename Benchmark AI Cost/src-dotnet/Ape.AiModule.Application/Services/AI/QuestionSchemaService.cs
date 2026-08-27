using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Application.Services.AI;

public sealed class QuestionSchemaService : IQuestionSchemaService
{
    public QuestionSchemaMappingResult ValidateAndMap(
        string subject,
        string questionType,
        IReadOnlyList<GeneratedQuestion> questions,
        IReadOnlyList<ChunkContextDto> chunks)
    {
        var normalizedType = NormalizeQuestionType(questionType);
        var issues = new List<QuestionValidationIssue>();
        var feQuestions = new List<FeQuestionDocument>();
        var peQuestions = new List<PeQuestionDocument>();
        var validChunkIds = chunks.Select(static chunk => chunk.ChunkId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var question in questions)
        {
            var title = string.IsNullOrWhiteSpace(question.Title) ? null : question.Title.Trim();
            var effectiveTitle = title ?? "(untitled)";
            var questionTypeValue = NormalizeQuestionType(question.Type);

            if (!questionTypeValue.Equals(normalizedType, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new("error", "type_mismatch", $"Question type '{question.Type}' does not match requested type '{normalizedType}'.", effectiveTitle));
                continue;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                issues.Add(new("error", "missing_title", "Question title is required.", effectiveTitle));
                continue;
            }

            if (string.IsNullOrWhiteSpace(question.Description))
            {
                issues.Add(new("error", "missing_description", "Question description is required.", effectiveTitle));
                continue;
            }

            var topicTags = NormalizeTags(question.TopicTags);
            if (topicTags.Count == 0)
            {
                issues.Add(new("error", "missing_topic_tags", "Question must contain at least one topic tag.", effectiveTitle));
                continue;
            }

            var sourceChunkIds = NormalizeChunkIds(question.SourceChunkIds, validChunkIds);
            if (sourceChunkIds.Count == 0)
            {
                issues.Add(new("error", "missing_source_chunk_ids", "Question must reference at least one valid source chunk id.", effectiveTitle));
                continue;
            }

            if (normalizedType.Equals("FE", StringComparison.OrdinalIgnoreCase))
            {
                var options = NormalizeStrings(question.Options);
                var correctAnswers = NormalizeStrings(question.CorrectAnswer);

                if (question.SkeletonCode is not null || question.SolutionCode is not null || question.TestCases is not null)
                {
                    issues.Add(new("error", "fe_contains_pe_fields", "FE question must not contain skeleton_code, solution_code, or test_cases.", effectiveTitle));
                    continue;
                }

                if (options.Count < 2)
                {
                    issues.Add(new("error", "invalid_options", "FE question must contain at least 2 options.", effectiveTitle));
                    continue;
                }

                if (correctAnswers.Count == 0)
                {
                    issues.Add(new("error", "missing_correct_answer", "FE question must contain at least one correct_answer.", effectiveTitle));
                    continue;
                }

                var optionLabels = options
                    .Select(static option => option.Split('.', 2)[0].Trim())
                    .Where(static label => !string.IsNullOrWhiteSpace(label))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (correctAnswers.Any(answer => !optionLabels.Contains(answer)))
                {
                    issues.Add(new("error", "correct_answer_not_in_options", "FE correct_answer must match option labels.", effectiveTitle));
                    continue;
                }

                feQuestions.Add(new FeQuestionDocument(
                    "FE",
                    subject,
                    NormalizeDifficulty(question.Difficulty),
                    topicTags,
                    title,
                    question.Description.Trim(),
                    sourceChunkIds,
                    options,
                    correctAnswers,
                    string.IsNullOrWhiteSpace(question.Explanation) ? null : question.Explanation.Trim()));
                continue;
            }

            if (question.Options is not null || question.CorrectAnswer is not null || question.Explanation is not null)
            {
                issues.Add(new("error", "pe_contains_fe_fields", "PE question must not contain options, correct_answer, or explanation.", effectiveTitle));
                continue;
            }

            var skeletonCode = NormalizeCodeFiles(question.SkeletonCode, requireReadonly: true);
            var solutionCode = NormalizeCodeFiles(question.SolutionCode, requireReadonly: false);
            var testCases = NormalizeTestCases(question.TestCases);

            if (skeletonCode.Count == 0)
            {
                issues.Add(new("error", "missing_skeleton_code", "PE question must contain skeleton_code.", effectiveTitle));
                continue;
            }

            if (solutionCode.Count == 0)
            {
                issues.Add(new("error", "missing_solution_code", "PE question must contain solution_code.", effectiveTitle));
                continue;
            }

            if (testCases.Count == 0)
            {
                issues.Add(new("error", "missing_test_cases", "PE question must contain test_cases.", effectiveTitle));
                continue;
            }

            if (!testCases.Any(static testCase => !testCase.IsHidden))
            {
                issues.Add(new("warning", "no_visible_test_case", "PE question should contain at least one visible test case.", effectiveTitle));
            }

            peQuestions.Add(new PeQuestionDocument(
                "PE",
                subject,
                NormalizeDifficulty(question.Difficulty),
                topicTags,
                title,
                question.Description.Trim(),
                sourceChunkIds,
                skeletonCode,
                solutionCode,
                testCases));
        }

        var validQuestionCount = normalizedType.Equals("FE", StringComparison.OrdinalIgnoreCase) ? feQuestions.Count : peQuestions.Count;
        DetectDuplicates(normalizedType, feQuestions, peQuestions, issues);
        var hasErrors = issues.Any(issue => issue.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
        return new QuestionSchemaMappingResult(
            subject,
            normalizedType,
            !hasErrors && validQuestionCount == questions.Count,
            questions.Count,
            validQuestionCount,
            issues,
            feQuestions,
            peQuestions);
    }

    private static void DetectDuplicates(
        string normalizedType,
        IReadOnlyList<FeQuestionDocument> feQuestions,
        IReadOnlyList<PeQuestionDocument> peQuestions,
        List<QuestionValidationIssue> issues)
    {
        if (normalizedType.Equals("FE", StringComparison.OrdinalIgnoreCase))
        {
            DetectDuplicateByKey(
                feQuestions.Select(question => (Key: BuildDuplicateKey(question.Title, question.Description), Title: question.Title)),
                issues);
            return;
        }

        DetectDuplicateByKey(
            peQuestions.Select(question => (Key: BuildDuplicateKey(question.Title, question.Description), Title: question.Title)),
            issues);
    }

    private static void DetectDuplicateByKey(
        IEnumerable<(string Key, string Title)> candidates,
        List<QuestionValidationIssue> issues)
    {
        foreach (var duplicateGroup in candidates
                     .Where(static candidate => !string.IsNullOrWhiteSpace(candidate.Key))
                     .GroupBy(static candidate => candidate.Key, StringComparer.OrdinalIgnoreCase)
                     .Where(static group => group.Count() > 1))
        {
            foreach (var duplicate in duplicateGroup)
            {
                issues.Add(new(
                    "error",
                    "duplicate_question",
                    "Detected duplicate or near-duplicate question title/description within the same generation run.",
                    duplicate.Title));
            }
        }
    }

    private static string BuildDuplicateKey(string title, string description)
    {
        static string Normalize(string value)
        {
            var filtered = new string(value
                .ToLowerInvariant()
                .Where(static ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                .ToArray());
            return string.Join(' ', filtered.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        var normalizedTitle = Normalize(title);
        var normalizedDescription = Normalize(description);
        var truncatedDescription = normalizedDescription.Length > 180 ? normalizedDescription[..180] : normalizedDescription;
        return $"{normalizedTitle}|{truncatedDescription}";
    }

    private static string NormalizeQuestionType(string? questionType)
        => string.Equals(questionType?.Trim(), "PE", StringComparison.OrdinalIgnoreCase) ? "PE" : "FE";

    private static string NormalizeDifficulty(string? difficulty)
    {
        var value = difficulty?.Trim();
        return value?.ToLowerInvariant() switch
        {
            "easy" => "Easy",
            "hard" => "Hard",
            _ => "Medium"
        };
    }

    private static List<string> NormalizeTags(IReadOnlyList<string>? tags)
        => tags?
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Select(static tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    private static List<string> NormalizeStrings(IReadOnlyList<string>? items)
        => items?
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    private static List<string> NormalizeChunkIds(IReadOnlyList<string>? chunkIds, HashSet<string> validChunkIds)
        => chunkIds?
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .Where(validChunkIds.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    private static List<CodeFile> NormalizeCodeFiles(IReadOnlyList<CodeFile>? codeFiles, bool requireReadonly)
        => codeFiles?
            .Where(static codeFile => !string.IsNullOrWhiteSpace(codeFile.FileName) && !string.IsNullOrWhiteSpace(codeFile.Content))
            .Select(codeFile => new CodeFile(codeFile.FileName.Trim(), codeFile.Content.Trim(), requireReadonly && codeFile.IsReadonly))
            .ToList() ?? [];

    private static List<QuestionTestCase> NormalizeTestCases(IReadOnlyList<QuestionTestCase>? testCases)
        => testCases?
            .Where(static testCase => !string.IsNullOrWhiteSpace(testCase.Input) || !string.IsNullOrWhiteSpace(testCase.ExpectedOutput))
            .Select(testCase => new QuestionTestCase(
                testCase.Input.Trim(),
                testCase.ExpectedOutput.Trim(),
                testCase.IsHidden))
            .ToList() ?? [];
}
