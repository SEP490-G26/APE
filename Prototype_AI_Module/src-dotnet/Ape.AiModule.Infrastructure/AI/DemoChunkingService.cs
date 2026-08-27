using System.Text.RegularExpressions;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class DemoChunkingService : IChunkingService
{
    public IReadOnlyList<string> BuildChunks(string normalizedMarkdown, PipelineMode mode)
    {
        var cleaned = Regex.Replace(normalizedMarkdown, @"\r\n|\r", "\n");
        var rawSections = cleaned
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var sections = BuildLogicalSections(rawSections);

        if (sections.Count == 0)
        {
            return new[] { cleaned };
        }

        var chunks = new List<string>();
        var buffer = new List<string>();
        var wordCount = 0;
        var limit = mode == PipelineMode.FullMultimodalPage ? 220 : 320;

        foreach (var section in sections)
        {
            var sectionWords = section.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (buffer.Count > 0 && wordCount + sectionWords > limit)
            {
                chunks.Add(string.Join("\n\n", buffer));
                buffer.Clear();
                wordCount = 0;
            }

            buffer.Add(section);
            wordCount += sectionWords;
        }

        if (buffer.Count > 0)
        {
            chunks.Add(string.Join("\n\n", buffer));
        }

        return chunks;
    }

    private static List<string> BuildLogicalSections(IReadOnlyList<string> rawSections)
    {
        var sections = new List<string>();

        for (var index = 0; index < rawSections.Count; index++)
        {
            var current = rawSections[index];
            if (IsImageMarkerBlock(current) && index + 1 < rawSections.Count && IsVisionDescriptionBlock(rawSections[index + 1]))
            {
                current = $"{current}\n\n{rawSections[index + 1]}";
                index++;
            }

            if (ShouldKeepSection(current))
            {
                sections.Add(current);
            }
        }

        return sections;
    }

    private static bool ShouldKeepSection(string section)
    {
        if (ContainsVisionContent(section))
        {
            return true;
        }

        return CountWords(section) >= 20;
    }

    private static bool ContainsVisionContent(string section)
        => IsImageMarkerBlock(section)
           || IsVisionDescriptionBlock(section)
           || section.Contains("[image:", StringComparison.OrdinalIgnoreCase)
           || section.Contains("[img:", StringComparison.OrdinalIgnoreCase)
           || section.Contains("[figure:", StringComparison.OrdinalIgnoreCase)
           || section.Contains("Vision Extracted From Image", StringComparison.OrdinalIgnoreCase);

    private static bool IsImageMarkerBlock(string section)
        => Regex.IsMatch(section, @"^\[(image|img|figure):[^\]]+\]$", RegexOptions.IgnoreCase);

    private static bool IsVisionDescriptionBlock(string section)
        => section.StartsWith("### Vision Extracted From Image", StringComparison.OrdinalIgnoreCase);

    private static int CountWords(string section)
        => section.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
