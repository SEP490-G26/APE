using System.Text.RegularExpressions;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class DemoChunkingService : IChunkingService
{
    public IReadOnlyList<string> BuildChunks(string normalizedMarkdown, PipelineMode mode)
    {
        var cleaned = Regex.Replace(normalizedMarkdown, @"\r\n|\r", "\n");
        var sections = cleaned
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static part => part.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 20)
            .ToList();

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
}
