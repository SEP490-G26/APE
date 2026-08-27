using Application.DTOs;
using Application.Interfaces;

namespace BE_IntegrationTests.External;

public sealed class ControlledAIExtractedContentService : IAIExtractedContentService
{
    public Task<ExtractedContentResultDto> NormalizeAsync(ExtractionResultDto extracted, AIRuntimeOverride? runtimeOverride = null, CancellationToken cancellationToken = default)
    {
        var content = string.IsNullOrWhiteSpace(extracted.RawText)
            ? "Introduction to controlled integration content."
            : extracted.RawText.Trim();

        var normalized = content.StartsWith("## ", StringComparison.Ordinal)
            ? content
            : $"## Controlled Document\n{content}";

        return Task.FromResult(new ExtractedContentResultDto
        {
            RawText = content,
            Content = content,
            NormalizedMarkdown = normalized,
            SourceType = extracted.SourceType,
            ParserName = "controlled-normalizer",
            ExtractionMode = "text_only",
            WordCount = Math.Max(1, content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length),
            CandidateTitles = ["Controlled Document"],
            CandidateChapterMarkers = ["chapter-1"],
            RejectedHeadingCandidates = [],
            CleanDisplayTitleCandidates = ["Controlled Document"],
            StructuralUnits =
            [
                new ExtractedStructuralUnitDto
                {
                    UnitKey = "chapter-1",
                    Kind = "chapter",
                    DisplayTitle = "Controlled Chapter 1",
                    StartLine = 1,
                    EndLine = 4,
                    Confidence = 0.99m,
                    LearnerFacing = true,
                    Aliases = ["queues", "stacks"]
                }
            ]
        });
    }
}
