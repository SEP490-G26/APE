using System;
using System.Text.RegularExpressions;

namespace Application.Common;

/// <summary>
/// AIContentSanitizer
/// Provides lightweight, high-performance sanitization for knowledge chunk texts before RAG retrieval and question generation.
/// Guarantees that legacy chunks stored in the database containing residual image placeholders, page numbers,
/// or OCR divider lines are thoroughly cleaned before being packed into AI context packs and prompt payloads.
/// </summary>
public static class AIContentSanitizer
{
    private static readonly Regex ImageTagRegex = new(
        @"\[\[IMAGE:.*?\]\]|\[Image Summary:.*?\]|!\[.*?\]\(.*?\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex PageSlideNumberRegex = new(
        @"(?im)^\s*\[?(?:Page|Slide|Trang)\s+\d+(?:\s*(?:of|/)\s*\d+)?\]?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex DividerRegex = new(
        @"(?im)^\s*[=\-_*]{3,}\s*$",
        RegexOptions.Compiled);

    private static readonly Regex MultipleNewlinesRegex = new(
        @"(\r?\n){3,}",
        RegexOptions.Compiled);

    /// <summary>
    /// Sanitizes chunk text by stripping out machine image placeholders, slide/page numbering markers,
    /// and visual divider lines, returning clean academic text.
    /// </summary>
    /// <param name="content">The raw or normalized chunk text from the database.</param>
    /// <returns>Cleaned text ready for RAG packing and prompt injection.</returns>
    public static string SanitizeChunkContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        // 1. Remove machine image tags and markdown image markers
        var cleaned = ImageTagRegex.Replace(content, string.Empty);

        // 2. Remove isolated slide/page numbers and OCR visual dividers
        cleaned = PageSlideNumberRegex.Replace(cleaned, string.Empty);
        cleaned = DividerRegex.Replace(cleaned, string.Empty);

        // 3. Normalize multiple blank lines to a single paragraph break and trim
        cleaned = MultipleNewlinesRegex.Replace(cleaned, $"{Environment.NewLine}{Environment.NewLine}");

        return cleaned.Trim();
    }
}
