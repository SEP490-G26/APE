using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Domain.Entities;

namespace Application.Services;

internal static class ExecutionFeedbackSanitizer
{
    public const int MaximumRawDetailsLength = 800;
    public const int MaximumMessageLength = 240;
    public const int MaximumTitleLength = 120;

    private static readonly Regex SourceLocationPattern =
        new(
            @"(?<path>(?:[A-Za-z]:[\\/]|/)[^:\r\n]*?(?<file>[^\\/:\r\n]+\.(?:java|c|cc|cpp|cxx|h|hpp))):(?<line>\d+)(?::(?<column>\d+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BareLocationPattern =
        new(
            @"(?<file>[^\\/\s:]+?\.(?:java|c|cc|cpp|cxx|h|hpp)):(?<line>\d+)(?::(?<column>\d+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? SanitizeTechnicalText(
        string? value,
        IReadOnlyCollection<CodeFile> submittedFiles,
        int maximumLength = MaximumRawDetailsLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = value.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);

        var submittedNames = submittedFiles
            .Select(file => Path.GetFileName(file.Filename))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        sanitized = SourceLocationPattern.Replace(
            sanitized,
            match =>
            {
                var file = match.Groups["file"].Value;
                if (submittedNames.Contains(file))
                {
                    return match.Value.Replace(
                        match.Groups["path"].Value,
                        file,
                        StringComparison.Ordinal);
                }

                return $"{file}:{match.Groups["line"].Value}";
            });

        sanitized = Regex.Replace(
            sanitized,
            @"(?:[A-Za-z]:[\\/]|/)[^\s:]+",
            "[path]",
            RegexOptions.IgnoreCase);

        return Truncate(
            sanitized.Trim(),
            maximumLength);
    }

    public static bool TryExtractSafeSourceLocation(
        string? text,
        IReadOnlyCollection<CodeFile> submittedFiles,
        out string? filename,
        out int? line,
        out int? column)
    {
        filename = null;
        line = null;
        column = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var submittedNames = submittedFiles
            .Select(file => Path.GetFileName(file.Filename))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in SourceLocationPattern.Matches(text))
        {
            var candidate = match.Groups["file"].Value;
            if (!submittedNames.Contains(candidate))
            {
                continue;
            }

            filename = candidate;
            line = ParseNullableInt(match.Groups["line"].Value);
            column = ParseNullableInt(match.Groups["column"].Value);
            return true;
        }

        foreach (Match match in BareLocationPattern.Matches(text))
        {
            var candidate = match.Groups["file"].Value;
            if (!submittedNames.Contains(candidate))
            {
                continue;
            }

            filename = candidate;
            line = ParseNullableInt(match.Groups["line"].Value);
            column = ParseNullableInt(match.Groups["column"].Value);
            return true;
        }

        return false;
    }

    public static string Truncate(
        string value,
        int maximumLength)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length <= maximumLength)
        {
            return value;
        }

        return value[..maximumLength];
    }

    private static int? ParseNullableInt(
        string value)
    {
        return int.TryParse(value, out var parsed)
            ? parsed
            : null;
    }
}
