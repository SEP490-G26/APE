using System.Text.RegularExpressions;
using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Infrastructure.Files;

public sealed class DemoDocumentParser : IDocumentParser
{
    private readonly IAiProviderGateway _aiProviderGateway;
    private readonly IExtractedContentPolicyService _extractedContentPolicyService;

    public DemoDocumentParser(
        IAiProviderGateway aiProviderGateway,
        IExtractedContentPolicyService extractedContentPolicyService)
    {
        _aiProviderGateway = aiProviderGateway;
        _extractedContentPolicyService = extractedContentPolicyService;
    }

    public async Task<ExtractedContent> ParseAsync(ExtractedContentRequest request, CancellationToken cancellationToken)
    {
        var policy = await _extractedContentPolicyService.GetPolicyAsync(cancellationToken);
        var content = request.RawContent;
        var imageRefs = Regex.Matches(content, @"\[(image|img|figure):([^\]]+)\]", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[2].Value.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(request.RawContent))
        {
            warnings.Add("The input content is empty.");
        }

        if (imageRefs.Count > 0 && string.IsNullOrWhiteSpace(request.VisionModel))
        {
            warnings.Add("Images were detected but no vision model was provided, so image placeholders were not expanded.");
        }

        foreach (var imageRef in imageRefs)
        {
            if (!string.IsNullOrWhiteSpace(request.VisionModel))
            {
                var description = await _aiProviderGateway.DescribeImageAsync(imageRef, request.VisionModel, cancellationToken);
                content = content.Replace($"[image:{imageRef}]", $"\n\n### Vision Extracted From Image `{imageRef}`\n{description}\n", StringComparison.OrdinalIgnoreCase);
                content = content.Replace($"[img:{imageRef}]", $"\n\n### Vision Extracted From Image `{imageRef}`\n{description}\n", StringComparison.OrdinalIgnoreCase);
                content = content.Replace($"[figure:{imageRef}]", $"\n\n### Vision Extracted From Image `{imageRef}`\n{description}\n", StringComparison.OrdinalIgnoreCase);
            }
        }

        var normalized = NormalizeToMarkdown(content, request.FileName, request.Mode, policy);
        var imageMatches = Regex.Matches(request.RawContent, @"\[(image|img|figure):[^\]]+\]", RegexOptions.IgnoreCase).Count;
        var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount == 0)
        {
            warnings.Add("No textual content remained after normalization.");
        }

        return new ExtractedContent(
            RawText: content,
            NormalizedMarkdown: normalized,
            WordCount: wordCount,
            EmbeddedImageCount: imageMatches,
            ParserName: request.Mode == PipelineMode.FullMultimodalPage ? "multimodal-page-parser" : "text-parser",
            VisionModelName: request.Mode == PipelineMode.FullMultimodalPage ? request.VisionModel : null,
            ExtractionMode: request.Mode == PipelineMode.FullMultimodalPage ? "full_multimodal_page" : "text_only",
            DetectedImageReferences: imageRefs,
            Warnings: warnings);
    }

    private static string NormalizeToMarkdown(string content, string fileName, PipelineMode mode, IReadOnlyDictionary<string, object> policy)
    {
        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        if (TryReadBool(policy, "normalization_rules", "collapse_spaces") != false)
        {
            normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        }

        if (TryReadBool(policy, "normalization_rules", "collapse_blank_lines") != false)
        {
            normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        }

        var header = $"# Parsed Document: {fileName}\n\n";
        var modeLine = $"Mode: {(mode == PipelineMode.FullMultimodalPage ? "full-multimodal-page" : "text-only")}\n\n";
        return header + modeLine + normalized;
    }

    private static bool? TryReadBool(IReadOnlyDictionary<string, object> root, string objectKey, string valueKey)
    {
        if (!root.TryGetValue(objectKey, out var nested) || nested is not IReadOnlyDictionary<string, object> nestedDictionary)
        {
            return null;
        }

        if (!nestedDictionary.TryGetValue(valueKey, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }
}
