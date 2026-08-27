using System.Text.RegularExpressions;
using Domain.Entities;

namespace Application.Common;

public static class DocumentChapterSummaryBuilder
{
    public static List<DocumentChapterSummary> Build(IReadOnlyList<KnowledgeChunk> chunks)
        => MergeDuplicateChapterSummaries(
            chunks
            .GroupBy(ResolveChapterKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var groupedChunks = group.ToList();
                var chapterTitle = ResolveGroupChapterTitle(groupedChunks, group.Key);
                var chapterOrder = groupedChunks
                    .Where(chunk => chunk.ChapterOrder.HasValue)
                    .Select(chunk => chunk.ChapterOrder!.Value)
                    .DefaultIfEmpty()
                    .Min();
                var normalizedChapterOrder = chapterOrder == 0 && !groupedChunks.Any(chunk => chunk.ChapterOrder.HasValue)
                    ? (int?)null
                    : chapterOrder;
                var coveredTopics = BuildCoveredTopics(groupedChunks, chapterTitle);

                var sampleSectionTitles = groupedChunks
                    .Select(chunk => chunk.SectionTitle)
                    .Select(CanonicalizeSectionSummaryTitle)
                    .Where(title => !string.IsNullOrWhiteSpace(title))
                    .Cast<string>()
                    .Where(title => !IsGenericSectionOnlyHeading(title) || !HasMoreSpecificSectionTitle(groupedChunks))
                    .GroupBy(title => title, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => ScoreSampleSectionTitle(group.Key, coveredTopics, group.Count()))
                    .ThenBy(group => group.Key.Length)
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.Key)
                    .Take(4)
                    .ToList();

                var estimatedTokens = groupedChunks.Sum(chunk => chunk.TokenCount > 0 ? chunk.TokenCount : EstimateTokenCount(chunk.RawText));
                if (ShouldSkipChapterSummary(chapterTitle, coveredTopics, sampleSectionTitles, groupedChunks.Count, estimatedTokens))
                {
                    return null;
                }

                return new DocumentChapterSummary
                {
                    ChapterKey = group.Key,
                    ChapterTitle = chapterTitle,
                    ChapterOrder = normalizedChapterOrder,
                    ChunkCount = groupedChunks.Count,
                    EstimatedTokens = estimatedTokens,
                    CoveredTopics = coveredTopics,
                    SampleSectionTitles = sampleSectionTitles,
                    OverviewShort = BuildOverviewShort(chapterTitle, coveredTopics, sampleSectionTitles)
                };
            })
            .Where(item => item is not null)
            .Cast<DocumentChapterSummary>()
            .OrderBy(item => item.ChapterOrder ?? int.MaxValue)
            .ThenBy(item => item.ChapterTitle, StringComparer.OrdinalIgnoreCase)
            .ToList());

    public static List<DocumentChapterSummary> Clone(IReadOnlyList<DocumentChapterSummary> chapters)
        => chapters.Select(item => new DocumentChapterSummary
        {
            ChapterKey = item.ChapterKey,
            ChapterTitle = item.ChapterTitle,
            ChapterOrder = item.ChapterOrder,
            ChunkCount = item.ChunkCount,
            EstimatedTokens = item.EstimatedTokens,
            CoveredTopics = item.CoveredTopics.ToList(),
            SampleSectionTitles = item.SampleSectionTitles.ToList(),
            OverviewShort = item.OverviewShort
        }).ToList();

    public static string BuildOverviewShort(
        string? chapterTitle,
        IReadOnlyList<string> coveredTopics,
        IReadOnlyList<string> sampleSectionTitles)
    {
        var normalizedTitle = (chapterTitle ?? string.Empty).Trim();
        var topics = coveredTopics
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var sections = sampleSectionTitles
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => NormalizeLabel(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        if (topics.Count > 0)
        {
            var verb = ResolveVerb(normalizedTitle);
            return $"{verb} {FormatList(topics)}.";
        }

        if (sections.Count > 0)
        {
            return $"Covers {FormatList(sections)}.";
        }

        return !string.IsNullOrWhiteSpace(normalizedTitle)
            ? $"Covers {NormalizeLabel(normalizedTitle)}."
            : "Covers core concepts from this chapter.";
    }

    public static string ResolveChapterKey(KnowledgeChunk chunk)
        => !string.IsNullOrWhiteSpace(chunk.ChapterKey) && !IsLowValueStructuralTitle(chunk.ChapterKey)
            ? chunk.ChapterKey
            : BuildFallbackChapterKey(chunk.ChapterTitle) ??
              BuildFallbackChapterKey(chunk.SectionTitle) ??
              $"chapter-{chunk.ChunkIndex + 1:00}";

    public static string ResolveChapterTitle(KnowledgeChunk chunk)
        => SanitizeStructuralTitle(chunk.ChapterTitle) ??
           SanitizeStructuralTitle(chunk.SectionTitle) ??
           $"Untitled Chapter {chunk.ChunkIndex + 1}";

    private static string ResolveGroupChapterTitle(IReadOnlyList<KnowledgeChunk> chunks, string chapterKey)
    {
        var titles = chunks
            .Select(chunk => SanitizeStructuralTitle(chunk.ChapterTitle) ?? SanitizeStructuralTitle(chunk.SectionTitle))
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Cast<string>()
            .GroupBy(title => title, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Title = group.Key,
                Count = group.Count(),
                Score = ScoreChapterTitle(group.Key)
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Count)
            .ThenBy(item => item.Title.Length)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (titles.Count > 0)
        {
            return titles[0].Title;
        }

        var fallbackFromKey = BuildDisplayTitleFromChapterKey(chapterKey);
        return !string.IsNullOrWhiteSpace(fallbackFromKey)
            ? fallbackFromKey
            : $"Untitled Chapter {chunks.Min(chunk => chunk.ChunkIndex) + 1}";
    }

    private static string ResolveVerb(string chapterTitle)
    {
        if (Regex.IsMatch(chapterTitle, @"(?i)\b(introduction|intro|overview|fundamentals|basic|basics|foundation|foundations)\b"))
        {
            return "Introduces";
        }

        if (Regex.IsMatch(chapterTitle, @"(?i)\b(example|examples|exercise|exercises|practice|lab|worksheet)\b"))
        {
            return "Provides examples on";
        }

        return "Covers";
    }

    private static string FormatList(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return "core concepts";
        }

        if (values.Count == 1)
        {
            return values[0];
        }

        if (values.Count == 2)
        {
            return $"{values[0]} and {values[1]}";
        }

        return $"{string.Join(", ", values.Take(values.Count - 1))}, and {values[^1]}";
    }

    private static string NormalizeLabel(string value)
    {
        var normalized = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "core concepts";
        }

        normalized = Regex.Replace(normalized, @"^(chapter|chap|chuong|section|sec|part|module|unit)\s*[\d\.\-:]*\s*", string.Empty, RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(normalized) ? "core concepts" : normalized;
    }

    private static string? BuildFallbackChapterKey(string? sectionTitle)
    {
        var sanitizedTitle = SanitizeStructuralTitle(sectionTitle);
        if (string.IsNullOrWhiteSpace(sanitizedTitle))
        {
            return null;
        }

        var normalized = Regex.Replace(sanitizedTitle.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var chapterMatch = Regex.Match(sanitizedTitle, @"(?i)\b(chapter|chap|chuong|section|sec|part|module|unit)\s*(\d+([\.]\d+)*)");
        if (chapterMatch.Success)
        {
            return $"{chapterMatch.Groups[1].Value.ToLowerInvariant()}-{chapterMatch.Groups[2].Value}";
        }

        var ordinalMatch = Regex.Match(sanitizedTitle, @"^\s*(\d+([\.]\d+)*)");
        if (ordinalMatch.Success)
        {
            return $"chapter-{ordinalMatch.Groups[1].Value}";
        }

        return normalized.Length <= 64 ? normalized : normalized[..64].Trim('-');
    }

    private static string? SanitizeStructuralTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
        normalized = normalized.TrimStart('#').Trim();
        normalized = Regex.Replace(normalized, @"!\[[^\]]*\]\([^)]+\)", string.Empty, RegexOptions.IgnoreCase).Trim();
        if (IsLowValueStructuralTitle(normalized))
        {
            return null;
        }

        return normalized;
    }

    private static string? CanonicalizeSectionSummaryTitle(string? value)
    {
        var sanitized = SanitizeStructuralTitle(value);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return null;
        }

        var normalized = Regex.Replace(sanitized, @"^\d+(?:\.\d+){0,3}\s+", string.Empty).Trim();
        normalized = Regex.Replace(normalized, @"\s*-\s*\d{1,4}$", string.Empty).Trim();

        var conceptPatterns = new (string Pattern, string Label)[]
        {
            (@"(?i)\bpriority queues?\b", "Priority Queues"),
            (@"(?i)\bdouble-ended queues?\b|\bdeque\b", "Double-Ended Queues"),
            (@"(?i)\bapplications? of queues?\b", "Applications of Queues"),
            (@"(?i)\bround robin schedulers?\b", "Round Robin Schedulers"),
            (@"(?i)\bcircular queue\b", "A Circular Queue")
        };

        foreach (var (pattern, label) in conceptPatterns)
        {
            if (Regex.IsMatch(normalized, pattern))
            {
                return label;
            }
        }

        return normalized;
    }

    private static bool IsLowValueStructuralTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
        if (normalized.StartsWith("Parsed Document:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.StartsWith("[Image Summary:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsInstitutionalBannerLine(normalized) || IsSlidePageCounterLine(normalized) || LooksLikeDanglingFragmentTitle(normalized))
        {
            return true;
        }

        if (normalized.Length <= 2)
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^[#\-\d\s\.]+$"))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^(?:[01]{2,}[ \-]*)+$"))
        {
            return true;
        }

        var alphaNumericTokens = Regex.Matches(normalized, @"[A-Za-z0-9]+")
            .Select(match => match.Value)
            .ToList();
        if (alphaNumericTokens.Count > 0 && alphaNumericTokens.All(token => Regex.IsMatch(token, @"^[01]{2,}$")))
        {
            return true;
        }

        return false;
    }

    private static List<DocumentChapterSummary> MergeDuplicateChapterSummaries(IReadOnlyList<DocumentChapterSummary> chapters)
    {
        var merged = chapters
            .Where(chapter => !IsUntitledChapterTitle(chapter.ChapterTitle))
            .GroupBy(chapter => NormalizeChapterMergeKey(chapter.ChapterTitle), StringComparer.OrdinalIgnoreCase)
            .Select(group => MergeChapterSummaryGroup(group.ToList()))
            .ToList();

        foreach (var untitled in chapters.Where(chapter => IsUntitledChapterTitle(chapter.ChapterTitle)))
        {
            var candidate = merged
                .Select(chapter => new
                {
                    Chapter = chapter,
                    Score = ScoreUntitledMergeCandidate(untitled, chapter)
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Chapter.ChapterOrder ?? int.MaxValue)
                .FirstOrDefault();

            if (candidate is not null)
            {
                MergeChapterInto(candidate.Chapter, untitled);
                continue;
            }

            if (!ShouldSuppressUntitledChapter(untitled))
            {
                merged.Add(untitled);
            }
        }

        return merged
            .OrderBy(item => item.ChapterOrder ?? int.MaxValue)
            .ThenBy(item => item.ChapterTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DocumentChapterSummary MergeChapterSummaryGroup(IReadOnlyList<DocumentChapterSummary> group)
    {
        var merged = Clone(group[0]);
        foreach (var chapter in group.Skip(1))
        {
            MergeChapterInto(merged, chapter);
        }

        merged.ChapterTitle = group
            .Select(item => item.ChapterTitle)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .OrderByDescending(ScoreChapterTitle)
            .ThenBy(title => title.Length)
            .ThenBy(title => title, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? merged.ChapterTitle;

        merged.OverviewShort = BuildOverviewShort(merged.ChapterTitle, merged.CoveredTopics, merged.SampleSectionTitles);
        return merged;
    }

    private static void MergeChapterInto(DocumentChapterSummary target, DocumentChapterSummary source)
    {
        target.ChunkCount += source.ChunkCount;
        target.EstimatedTokens += source.EstimatedTokens;
        target.ChapterOrder = ResolveMergedChapterOrder(target.ChapterOrder, source.ChapterOrder);
        target.CoveredTopics = target.CoveredTopics
            .Concat(source.CoveredTopics)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        target.SampleSectionTitles = target.SampleSectionTitles
            .Concat(source.SampleSectionTitles)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(title => ScoreSampleSectionTitle(title, target.CoveredTopics, 1))
            .ThenBy(title => title.Length)
            .ThenBy(title => title, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
        target.OverviewShort = BuildOverviewShort(target.ChapterTitle, target.CoveredTopics, target.SampleSectionTitles);
    }

    private static DocumentChapterSummary Clone(DocumentChapterSummary item)
        => new()
        {
            ChapterKey = item.ChapterKey,
            ChapterTitle = item.ChapterTitle,
            ChapterOrder = item.ChapterOrder,
            ChunkCount = item.ChunkCount,
            EstimatedTokens = item.EstimatedTokens,
            CoveredTopics = item.CoveredTopics.ToList(),
            SampleSectionTitles = item.SampleSectionTitles.ToList(),
            OverviewShort = item.OverviewShort
        };

    private static int? ResolveMergedChapterOrder(int? left, int? right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        if (!right.HasValue)
        {
            return left;
        }

        return Math.Min(left.Value, right.Value);
    }

    private static int ScoreUntitledMergeCandidate(DocumentChapterSummary untitled, DocumentChapterSummary titled)
    {
        var score = 0;
        if (Math.Abs((untitled.ChapterOrder ?? int.MaxValue) - (titled.ChapterOrder ?? int.MaxValue)) <= 1)
        {
            score += 2;
        }

        var topicOverlap = untitled.CoveredTopics.Count(topic => titled.CoveredTopics.Contains(topic, StringComparer.OrdinalIgnoreCase));
        score += topicOverlap * 3;

        var titleOverlap = untitled.SampleSectionTitles.Count(title => titled.SampleSectionTitles.Contains(title, StringComparer.OrdinalIgnoreCase));
        score += titleOverlap * 2;

        if (untitled.SampleSectionTitles.Any(title => string.Equals(title, titled.ChapterTitle, StringComparison.OrdinalIgnoreCase)))
        {
            score += 4;
        }

        return score;
    }

    private static bool ShouldSuppressUntitledChapter(DocumentChapterSummary item)
    {
        if (!IsUntitledChapterTitle(item.ChapterTitle))
        {
            return false;
        }

        if (item.ChunkCount <= 2 && item.EstimatedTokens <= 180)
        {
            return true;
        }

        return item.CoveredTopics.Count == 0 &&
               item.SampleSectionTitles.Count <= 1 &&
               item.EstimatedTokens <= 260;
    }

    private static bool IsUntitledChapterTitle(string? title)
        => !string.IsNullOrWhiteSpace(title) &&
           title.StartsWith("Untitled Chapter ", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeChapterMergeKey(string? title)
    {
        var normalized = NormalizeLabel(title ?? string.Empty);
        normalized = Regex.Replace(normalized, @"[^a-z0-9]+", " ", RegexOptions.IgnoreCase).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? "untitled" : normalized;
    }

    private static int ScoreChapterTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return 0;
        }

        if (IsUntitledChapterTitle(title))
        {
            return -10;
        }

        var score = 0;
        if (!LooksStructuredSection(title))
        {
            score += 4;
        }

        if (!IsGenericSectionOnlyHeading(title))
        {
            score += 3;
        }

        if (!Regex.IsMatch(title, @"^\d+(\.\d+){0,3}$"))
        {
            score += 2;
        }

        score += Math.Min(title.Length, 40) / 10;
        return score;
    }

    private static string? BuildDisplayTitleFromChapterKey(string? chapterKey)
    {
        if (string.IsNullOrWhiteSpace(chapterKey))
        {
            return null;
        }

        var cleaned = Regex.Replace(chapterKey, @"^(chapter|chap|chuong|section|sec|part|module|unit)-", string.Empty, RegexOptions.IgnoreCase);
        cleaned = cleaned.Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        return Regex.Replace(cleaned, @"\b\w", match => match.Value.ToUpperInvariant());
    }

    private static bool LooksStructuredSection(string? sectionTitle)
        => !string.IsNullOrWhiteSpace(sectionTitle) &&
           Regex.IsMatch(sectionTitle, @"(?i)^(chapter|chap|chuong|section|sec|part|module|unit|\d+(\.\d+){0,2})");

    private static List<string> BuildCoveredTopics(IReadOnlyList<KnowledgeChunk> chunks, string? chapterTitle)
    {
        var chapterText = (chapterTitle ?? string.Empty).Trim();
        var tagScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in chunks)
        {
            var sectionText = chunk.SectionTitle ?? string.Empty;
            foreach (var tag in chunk.TopicTags ?? new List<string>())
            {
                var normalized = NormalizeTopicTag(tag);
                if (string.IsNullOrWhiteSpace(normalized) || IsLowValueCoverageTag(normalized))
                {
                    continue;
                }

                tagCounts[normalized] = tagCounts.TryGetValue(normalized, out var currentCount) ? currentCount + 1 : 1;

                var score = 2;
                if (ContainsPhrase(chapterText, normalized))
                {
                    score += 3;
                }

                if (ContainsPhrase(sectionText, normalized))
                {
                    score += 2;
                }

                tagScores[normalized] = tagScores.TryGetValue(normalized, out var currentScore) ? currentScore + score : score;
            }
        }

        var hasSpecificTags = tagScores.Keys.Any(tag => !IsBroadCoverageTag(tag));
        return tagScores
            .Where(item => !hasSpecificTags || !IsBroadCoverageTag(item.Key))
            .OrderByDescending(item => item.Value)
            .ThenByDescending(item => tagCounts.TryGetValue(item.Key, out var count) ? count : 0)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Key)
            .Take(8)
            .ToList();
    }

    private static string NormalizeTopicTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        return Regex.Replace(tag.Trim(), @"\s+", " ").Trim().ToLowerInvariant();
    }

    private static bool IsLowValueCoverageTag(string tag)
        => string.IsNullOrWhiteSpace(tag)
           || tag is "general" or "topic" or "concept" or "study content" or "computer science" or "image summary";

    private static bool IsBroadCoverageTag(string tag)
        => tag is "c" or "java" or "java_oop" or "dsa_java" or "classes" or "objects" or "methods";

    private static int ScoreSampleSectionTitle(string title, IReadOnlyList<string> coveredTopics, int occurrenceCount)
    {
        var score = occurrenceCount * 2;
        var normalized = title.Trim();

        if (Regex.IsMatch(normalized, @"(?i)\b(priority queues?|double-ended queues?|deque|applications? of queues?|round robin schedulers?)\b"))
        {
            score += 8;
        }

        if (coveredTopics.Any(topic => ContainsPhrase(normalized, topic)))
        {
            score += 5;
        }

        if (Regex.IsMatch(normalized, @"(?i)\b(array|linked list|implementation)\b"))
        {
            score -= 1;
        }

        if (Regex.IsMatch(normalized, @"(?i)\babstract data type\b"))
        {
            score -= 2;
        }

        if (Regex.IsMatch(normalized, @"^\d+(?:\.\d+){1,3}\b"))
        {
            score -= 3;
        }

        return score;
    }

    private static bool HasMoreSpecificSectionTitle(IReadOnlyList<KnowledgeChunk> chunks)
        => chunks
            .Select(chunk => chunk.SectionTitle)
            .Select(SanitizeStructuralTitle)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Cast<string>()
            .Any(title => !IsGenericSectionOnlyHeading(title));

    private static bool ShouldSkipChapterSummary(
        string? chapterTitle,
        IReadOnlyList<string> coveredTopics,
        IReadOnlyList<string> sampleSectionTitles,
        int chunkCount,
        int estimatedTokens)
    {
        var normalizedTitle = (chapterTitle ?? string.Empty).Trim();
        var looksUntitled = normalizedTitle.StartsWith("Untitled Chapter ", StringComparison.OrdinalIgnoreCase);
        return looksUntitled &&
               coveredTopics.Count == 0 &&
               sampleSectionTitles.Count == 0 &&
               chunkCount <= 1 &&
               estimatedTokens <= 40;
    }

    private static bool ContainsPhrase(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        return text.Contains(phrase, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSlidePageCounterLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = Regex.Replace(line.Trim(), @"\s+", " ");
        return Regex.IsMatch(normalized, @"^(?:page|slide)?\s*\d+\s*/\s*\d+$", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(normalized, @"^/\d+$", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(normalized, @"^\d+/\d+$", RegexOptions.IgnoreCase);
    }

    private static bool LooksLikeDanglingFragmentTitle(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = Regex.Replace(line.Trim(), @"\s+", " ");
        if (normalized.Length < 8 || normalized.Length > 80)
        {
            return false;
        }

        if (Regex.IsMatch(normalized, @"[.;:,!?]$"))
        {
            return false;
        }

        var words = Regex.Matches(normalized, @"\p{L}[\p{L}\p{N}_\-]*")
            .Select(match => match.Value)
            .ToList();
        if (words.Count is < 3 or > 10)
        {
            return false;
        }

        var ending = words.LastOrDefault() ?? string.Empty;
        if (ending.Length <= 3)
        {
            return true;
        }

        return Regex.IsMatch(normalized, @"(?i)\b(of|to|for|with|whose|that|which|where|when|from|into|onto|than|then|using|used|use)\s*$");
    }

    private static bool IsInstitutionalBannerLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = Regex.Replace(line.Trim(), @"\s+", " ");
        return Regex.IsMatch(normalized,
            @"(?i)\b(fpt university|truong dai hoc fpt|trường đại học fpt|qs stars(?:\s+(?:rated|rating|ranking))?\s+for\s+excellence|stars\s+(?:rated|rating|ranking)\s+for\s+excellence|rating\s+for\s+excellence|ranking\s+for\s+excellence)\b");
    }

    private static bool IsGenericSectionOnlyHeading(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalized = Regex.Replace(title.Trim(), @"\s+", " ");
        return Regex.IsMatch(normalized,
            @"(?i)^(classes?|methods?|objects?|objectives?|question|questions|exercise\s*\d*|example\s*\d*|examples|summary|overview|practice|quiz|worksheet|remark|note|notes)$");
    }

    private static int EstimateTokenCount(string? content)
        => string.IsNullOrWhiteSpace(content) ? 0 : Math.Max(1, (int)Math.Round(content.Length / 4.0));
}
