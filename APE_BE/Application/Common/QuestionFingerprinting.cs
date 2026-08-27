using System.Text.RegularExpressions;
using Application.DTOs;

namespace Application.Common;

public static class QuestionFingerprinting
{
    public static string Build(GeneratedQuestionCandidateDto question)
    {
        var normalizedTitle = NormalizeLooseText(question.Title);
        var normalizedDescription = NormalizeLooseText(question.Description);
        var topicPart = string.Join("|", question.TopicTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().ToLowerInvariant())
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase));

        if (string.Equals(question.Type, "PE", StringComparison.OrdinalIgnoreCase))
        {
            var combinedCode = string.Join("\n", question.SolutionCode?.Select(file => file.Content) ?? Array.Empty<string>());
            var methodNames = Regex.Matches(combinedCode, @"@Override\s+public\s+\w+(?:<[^>]+>)?\s+(\w+)\s*\(", RegexOptions.IgnoreCase)
                .Select(match => match.Groups[1].Value.Trim().ToLowerInvariant())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
            var testShapeItems = question.TestCases is null
                ? Array.Empty<string>()
                : question.TestCases
                    .Select(test => $"{NormalizeLooseText(test.Input)}=>{NormalizeLooseText(test.ExpectedOutput)}")
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            var testShape = string.Join("|", testShapeItems);
            var classCount = Regex.Matches(combinedCode, @"\bclass\s+\w+\b").Count;
            return $"PE::{topicPart}::{normalizedTitle}::{TrimForFingerprint(normalizedDescription)}::classes={classCount}::methods={string.Join(",", methodNames)}::tests={TrimForFingerprint(testShape)}";
        }

        var optionItems = question.Options is null
            ? Array.Empty<string>()
            : question.Options
                .Select(NormalizeLooseText)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        var options = string.Join("|", optionItems);
        return $"FE::{topicPart}::{normalizedTitle}::{TrimForFingerprint(normalizedDescription)}::{TrimForFingerprint(options)}";
    }

    public static string NormalizeLooseText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(text.ToLowerInvariant(), @"\s+", " ").Trim();
        normalized = Regex.Replace(normalized, @"\d+", "#");
        return normalized;
    }

    public static string TrimForFingerprint(string text)
    {
        const int maxLength = 160;
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
