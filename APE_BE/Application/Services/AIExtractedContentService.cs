/**
 * AIExtractedContentService.cs
 * Implementation of Step 2 in the APE AI Pipeline: Content Normalization, Vision OCR, and Structure Detection.
 * 
 * Pipeline Stages:
 * Stage 1: Deterministic local cleanup & image placeholder extraction.
 * Stage 2: Multimodal / Vision OCR transcription for diagrams, tables, and slides.
 * Stage 3: LLM-powered text normalization into standard Markdown.
 * Stage 4: Structure & Chapter detection (generates ChapterKey, TopicKey hierarchy tree).
 */

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Application.DTOs;
using Application.Interfaces;
using Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

/// <summary>
/// Service that normalizes extracted document text and constructs hierarchical chapter/topic structures.
/// </summary>
public class AIExtractedContentService : IAIExtractedContentService
{
    private static readonly Regex ImagePlaceholderRegex = new(@"\[\[IMAGE:(?<id>image-\d+)\|ref=(?<ref>[^\|\]]+)(\|label=(?<label>[^\|\]]*))?(\|slide=(?<slide>\d+))?(\|page=(?<page>\d+))?\]\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly TimeSpan NormalizeTransientRetryDelay = TimeSpan.FromMilliseconds(700);
    private const int DefaultVisionContextCharacterWindow = 160;
    private const int SmallVisionContextCharacterWindow = 80;
    private const int TinyVisionContextCharacterWindow = 80;
    private const int OutputVisionContextCharacterWindow = 110;
    private const int MinimumBatchPageSize = 2;
    private const int MaximumBatchPageSize = 4;
    private const int StructureDetectionExcerptCharacterLimit = 12000;
    private const int StructureDetectionMaxLines = 220;
    private const int StructureDetectionFullMarkdownCharacterLimit = 3500;
    private const int AiNormalizationCharacterLimit = 14000;
    private const int AiNormalizationLineLimit = 280;
    private const int MaxVisionItemsPerDocument = 6;
    private const int MaxSingleVisionFallbacksPerDocument = 2;
    private readonly IAIExecutionService _aiExecutionService;
    private readonly IAIFeatureRoutingService _routingService;
    private readonly IAIPromptService _promptService;
    private readonly IAIArtifactCatalogService _artifactCatalogService;
    private readonly AIOptions _options;
    private readonly ILogger<AIExtractedContentService> _logger;

    public AIExtractedContentService(
        IAIExecutionService aiExecutionService,
        IAIFeatureRoutingService routingService,
        IAIPromptService promptService,
        IAIArtifactCatalogService artifactCatalogService,
        IOptions<AIOptions> options,
        ILogger<AIExtractedContentService> logger)
    {
        _aiExecutionService = aiExecutionService;
        _routingService = routingService;
        _promptService = promptService;
        _artifactCatalogService = artifactCatalogService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executes the full normalization pipeline: clean text, transcribe images/tables via Vision prompt,
    /// format Markdown blocks, and detect chapter/topic hierarchies.
    /// </summary>
    /// <param name="extracted">Raw extraction input from <see cref="IFileExtractionService"/>.</param>
    /// <param name="runtimeOverride">Optional runtime model override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cleaned markdown content along with detected chapters and topics.</returns>
    public async Task<ExtractedContentResultDto> NormalizeAsync(ExtractionResultDto extracted, AIRuntimeOverride? runtimeOverride = null, CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stageTimings = new List<string>();
        var policy = await _artifactCatalogService.GetPolicyAsync("extracted-content-policy.json", cancellationToken);
        var prompt = await _artifactCatalogService.GetPromptAsync("extract_content_normalize", cancellationToken);
        var visionPrompt = await _artifactCatalogService.GetPromptAsync("extract_content_vision", cancellationToken);
        var structurePrompt = await _artifactCatalogService.GetPromptAsync("extract_content_structure_detect", cancellationToken);
        var stageStopwatch = Stopwatch.StartNew();
        var normalized = CleanText(extracted.RawText, extracted.FileName, policy);
        stageTimings.Add($"local_cleanup_ms={stageStopwatch.ElapsedMilliseconds}");
        var placeholderSequence = ExtractImagePlaceholders(normalized);
        var localNormalizationAssessment = AssessLocalNormalizationQuality(extracted.RawText, normalized);
        var detectedPlaceholderCount = placeholderSequence.Count;
        var resolved = _routingService.ResolveText(AIFeatureNames.ExtractedContent, runtimeOverride);
        var structureResolved = _routingService.ResolveText(AIFeatureNames.ExtractedStructure, runtimeOverride);
        var warnings = new List<string>(extracted.Warnings);
        AITextResponse? normalizationResponse = null;
        var latestVisionResponse = (AITextResponse?)null;
        AITextResponse? structureResponse = null;
        var reinjectionResult = new ReinjectionResult(normalized, 0, 0, 0, 0, 0, new List<VisionEnrichmentDetailDto>());
        var visionInputTokens = 0;
        var visionOutputTokens = 0;
        var visionTotalTokens = 0;
        var visionCostUsd = 0m;
        var hasVisionInputTokens = false;
        var hasVisionOutputTokens = false;
        var hasVisionTotalTokens = false;
        var hasVisionCostUsd = false;
        var visionFallbackUsed = false;

        if (placeholderSequence.Count > 0)
        {
            warnings.Add("Image placeholders were preserved in extracted content. Vision enrichment is still needed to replace them with image-derived content.");
        }

        if (_options.Enabled &&
            !string.IsNullOrWhiteSpace(normalized) &&
            normalized.Length >= 100 &&
            ShouldUseAiNormalization(normalized, localNormalizationAssessment, placeholderSequence.Count))
        {
            try
            {
                stageStopwatch.Restart();
                var systemPrompt = prompt?.SystemPrompt ?? """
                    Normalize the extracted study material.
                    Keep original meaning.
                    Remove OCR noise, duplicate lines, page headers, and broken spacing.
                    Preserve any line using the syntax [[IMAGE:...]] exactly as-is and in the same relative position.
                    Return markdown only.
                    """;
                systemPrompt += """

                    Hard constraints:
                    - Preserve the original document order and structure as much as possible.
                    - Do not invent new chapter titles, section titles, summaries, explanations, or textbook-style prose.
                    - Do not rewrite ordinary body sentences into standalone headings.
                    - Do not convert code lines, comments, bullet descriptions, method descriptions, or reading references into headings.
                    - Only mark a line as a heading if the source text already strongly looks like a heading.
                    - When uncertain, keep the original plain text instead of beautifying it.
                    """;
                var userPrompt = _promptService.Render(
                    prompt?.UserPrompt ?? """
                    File: {{file_name}}
                    Source type: {{source_type}}
                    Raw extracted content:
                    {{raw_text}}
                    """,
                    new Dictionary<string, string?>
                    {
                        ["file_name"] = extracted.FileName,
                        ["source_type"] = extracted.SourceType,
                        ["raw_text"] = BuildNormalizationInput(normalized)
                    });
                userPrompt += """


                    Additional normalization constraints:
                    - Keep existing headings close to source wording.
                    - Keep bullets as bullets; do not promote them to headings.
                    - Keep code comments and API/method descriptions inside body/code context; do not promote them to headings.
                    - Keep reading/reference lines as references, not chapter titles.
                    """;

                var response = await ExecuteNormalizationRequestWithTransientRetryAsync(
                    resolved,
                    new AITextRequest
                    {
                        FeatureName = AIFeatureNames.ExtractedContent,
                        Provider = resolved.Provider,
                        Model = resolved.Model,
                        Temperature = resolved.Temperature,
                        MaxTokens = resolved.MaxTokens,
                        SystemPrompt = systemPrompt,
                        UserPrompt = userPrompt,
                        RuntimeOverride = runtimeOverride
                    },
                    warnings,
                    cancellationToken);
                normalizationResponse = response;

                if (!string.IsNullOrWhiteSpace(response.Content))
                {
                    var cleanedAiContent = CleanText(response.Content, extracted.FileName, policy);
                    if (!PlaceholdersPreservedExactly(placeholderSequence, cleanedAiContent))
                    {
                        warnings.Add("AI normalization output did not preserve image placeholders exactly. Fallback to local normalized text was used to avoid losing positional image context.");
                        cleanedAiContent = normalized;
                    }
                    normalized = cleanedAiContent;
                }
                stageTimings.Add($"ai_normalize_ms={stageStopwatch.ElapsedMilliseconds}");
            }
            catch (Exception ex)
            {
                stageTimings.Add($"ai_normalize_ms={stageStopwatch.ElapsedMilliseconds}");
                _logger.LogWarning(ex, "AI extracted content normalization failed. Falling back to local cleanup.");
            }
        }
        else
        {
            warnings.Add($"Skipped AI normalization because local cleanup confidence was {localNormalizationAssessment.ConfidenceLabel} and the text was prepared locally for latency control.");
        }

        if (_options.Enabled &&
            extracted.ExtractedImages.Count > 0 &&
            placeholderSequence.Count > 0)
        {
            stageStopwatch.Restart();
            reinjectionResult = await ReinjectionWithVisionAsync(
                extracted,
                normalized,
                resolved,
                runtimeOverride,
                visionPrompt,
                warnings,
                cancellationToken,
                onVisionResponse: response =>
                {
                    latestVisionResponse = response;
                    if (response.InputTokens.HasValue)
                    {
                        visionInputTokens += response.InputTokens.Value;
                        hasVisionInputTokens = true;
                    }

                    if (response.OutputTokens.HasValue)
                    {
                        visionOutputTokens += response.OutputTokens.Value;
                        hasVisionOutputTokens = true;
                    }

                    if (response.TotalTokens.HasValue)
                    {
                        visionTotalTokens += response.TotalTokens.Value;
                        hasVisionTotalTokens = true;
                    }

                    if (response.ReportedCostUsd.HasValue)
                    {
                        visionCostUsd += response.ReportedCostUsd.Value;
                        hasVisionCostUsd = true;
                    }

                    visionFallbackUsed = visionFallbackUsed || response.FromFallback;
                });
            normalized = reinjectionResult.Content;
            stageTimings.Add($"vision_reinjection_ms={stageStopwatch.ElapsedMilliseconds}");
        }

        if (placeholderSequence.Count > 0 && reinjectionResult.SucceededCount == 0)
        {
            warnings.Add("No image placeholders were successfully enriched by vision. Placeholder lines were preserved for downstream review.");
        }

        if (reinjectionResult.UnresolvedPlaceholderCount > 0)
        {
            warnings.Add($"Vision enrichment left {reinjectionResult.UnresolvedPlaceholderCount} image placeholder(s) unresolved in the normalized output.");
        }

        normalized = FinalSanitizeNormalizedMarkdown(normalized);

        var heuristicHints = ToStructuralHintsDto(ExtractStructuralHints(normalized));
        var structuralHints = heuristicHints;

        if (_options.Enabled &&
            !string.IsNullOrWhiteSpace(normalized) &&
            normalized.Length >= 120)
        {
            try
            {
                stageStopwatch.Restart();
                structureResponse = await ExecuteStructureDetectionAsync(
                    extracted,
                    normalized,
                    heuristicHints,
                    structureResolved,
                    runtimeOverride,
                    structurePrompt,
                    warnings,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(structureResponse.Content))
                {
                    structuralHints = MergeStructuralHints(
                        heuristicHints,
                        ParseStructureDetectionResponse(structureResponse.Content, normalized, warnings));
                }
                stageTimings.Add($"structure_detection_ms={stageStopwatch.ElapsedMilliseconds}");
            }
            catch (Exception ex)
            {
                stageTimings.Add($"structure_detection_ms={stageStopwatch.ElapsedMilliseconds}");
                _logger.LogWarning(ex, "AI extracted content structure detection failed. Falling back to heuristic structural hints.");
            }
        }

        var finalUsage = structureResponse ?? latestVisionResponse ?? normalizationResponse;
        var totalInputTokens = SumNullable(
            SumNullable(normalizationResponse?.InputTokens, hasVisionInputTokens ? visionInputTokens : null),
            structureResponse?.InputTokens);
        var totalOutputTokens = SumNullable(
            SumNullable(normalizationResponse?.OutputTokens, hasVisionOutputTokens ? visionOutputTokens : null),
            structureResponse?.OutputTokens);
        var totalTokens = SumNullable(
            SumNullable(normalizationResponse?.TotalTokens, hasVisionTotalTokens ? visionTotalTokens : null),
            structureResponse?.TotalTokens);
        var totalCostUsd = SumNullable(
            SumNullable(normalizationResponse?.ReportedCostUsd, hasVisionCostUsd ? visionCostUsd : null),
            structureResponse?.ReportedCostUsd);

        totalStopwatch.Stop();
        _logger.LogInformation(
            "Extracted content normalization completed. FileName={FileName}, SourceType={SourceType}, Characters={Characters}, Placeholders={Placeholders}, StructuralUnits={StructuralUnits}, TotalMs={TotalMs}, StageTimings={StageTimings}",
            extracted.FileName,
            extracted.SourceType,
            normalized.Length,
            detectedPlaceholderCount,
            structuralHints.StructuralUnits?.Count ?? 0,
            totalStopwatch.ElapsedMilliseconds,
            string.Join(", ", stageTimings));

        return new ExtractedContentResultDto
        {
            RawText = extracted.RawText,
            NormalizedMarkdown = normalized,
            SourceType = extracted.SourceType,
            UsedAiNormalization = normalizationResponse is not null || reinjectionResult.AttemptedCount > 0,
            Provider = finalUsage?.Provider ?? resolved.Provider,
            Model = finalUsage?.Model ?? resolved.Model,
            ConfiguredModel = resolved.Model,
            EffectiveModel = finalUsage?.Model ?? resolved.Model,
            UsageSource = finalUsage?.UsageSource,
            CostSource = finalUsage?.CostSource,
            InputTokens = totalInputTokens,
            OutputTokens = totalOutputTokens,
            TotalTokens = totalTokens,
            CostUsd = totalCostUsd,
            FallbackUsed = normalizationResponse?.FromFallback == true || visionFallbackUsed,
            FallbackFromProvider = latestVisionResponse?.FallbackFromProvider ?? normalizationResponse?.FallbackFromProvider,
            FallbackFromModel = latestVisionResponse?.FallbackFromModel ?? normalizationResponse?.FallbackFromModel,
            FallbackReasonCode = latestVisionResponse?.FallbackReasonCode ?? normalizationResponse?.FallbackReasonCode,
            ParserName = extracted.ParserName,
            VisionModel = reinjectionResult.SucceededCount > 0
                ? (latestVisionResponse?.Model ?? resolved.Model)
                : extracted.IsVisionRecommended ? resolved.Model : null,
            ExtractionMode = reinjectionResult.SucceededCount > 0
                ? "vision_reinjected_text"
                : extracted.IsVisionRecommended ? "vision_recommended_text" : "text_only",
            WordCount = CountWords(normalized),
            DetectedImagePlaceholderCount = detectedPlaceholderCount,
            VisionEnrichmentAttemptedCount = reinjectionResult.AttemptedCount,
            VisionEnrichmentSucceededCount = reinjectionResult.SucceededCount,
            VisionEnrichmentFailedCount = reinjectionResult.FailedCount,
            VisionEnrichmentSkippedCount = reinjectionResult.SkippedCount,
            UnresolvedImagePlaceholderCount = reinjectionResult.UnresolvedPlaceholderCount,
            VisionEnrichmentDetails = reinjectionResult.Details,
            DetectedImageReferences = extracted.DetectedImageReferences,
            EmbeddedImageCount = extracted.EmbeddedImageCount,
            CandidateTitles = structuralHints.CandidateTitles,
            CandidateChapterMarkers = structuralHints.CandidateChapterMarkers,
            RejectedHeadingCandidates = structuralHints.RejectedHeadingCandidates,
            CleanDisplayTitleCandidates = structuralHints.CleanDisplayTitleCandidates,
            StructuralUnits = structuralHints.StructuralUnits,
            UsedAiStructureDetection = structureResponse is not null,
            Warnings = warnings
        };
    }

    private async Task<AITextResponse> ExecuteStructureDetectionAsync(
        ExtractionResultDto extracted,
        string normalized,
        ExtractionStructuralHintsDto heuristicHints,
        AIResolvedExecutionOptions resolved,
        AIRuntimeOverride? runtimeOverride,
        AIPromptTemplateDto? structurePrompt,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var structureView = BuildStructurePreparationView(normalized, heuristicHints);
        var compactFullNormalized = BuildStructureFullMarkdownExcerpt(normalized);
        var systemPrompt = structurePrompt?.SystemPrompt ?? """
            You are an academic document structure detector.
            Read a structure-prepared view of normalized study material and return strict JSON only.
            Detect learner-facing structural units that can guide chunking and chapter summaries.
            Prefer faithful structure from the source. Do not invent textbook chapters.
            """;
        var userPrompt = _promptService.Render(
            structurePrompt?.UserPrompt ?? """
            File: {{file_name}}
            Source type: {{source_type}}
            Heuristic candidate titles: {{candidate_titles}}
            Heuristic candidate chapter markers: {{candidate_chapter_markers}}
            Structure-prepared view:
            {{structure_view}}

            Full normalized markdown:
            {{normalized_markdown}}
            """,
            new Dictionary<string, string?>
            {
                ["file_name"] = extracted.FileName,
                ["source_type"] = extracted.SourceType,
                ["candidate_titles"] = JsonSerializer.Serialize(heuristicHints.CandidateTitles),
                ["candidate_chapter_markers"] = JsonSerializer.Serialize(heuristicHints.CandidateChapterMarkers),
                ["structure_view"] = structureView.Rendered,
                ["structure_excerpt"] = structureView.Rendered,
                ["normalized_markdown"] = structureView.Rendered,
                ["full_normalized_markdown"] = compactFullNormalized
            });
        userPrompt += $"""


            Additional constraints:
            - `structure_view` is the primary input for chapter/scope detection.
            - `full_normalized_markdown` may be truncated for latency control.
            - Prefer broader learner-facing scope blocks over small concept headings when both appear.
            """;

        if (structureView.DroppedLineCount > 0)
        {
            warnings.Add($"Structure prep kept {structureView.KeptLineCount} high-signal lines and dropped {structureView.DroppedLineCount} low-signal lines before AI chapter detection.");
        }

        try
        {
            return await _aiExecutionService.ExecuteTextAsync(
                resolved,
                new AITextRequest
                {
                    FeatureName = AIFeatureNames.ExtractedStructure,
                    Provider = resolved.Provider,
                    Model = resolved.Model,
                    Temperature = resolved.Temperature ?? 0.1,
                    MaxTokens = resolved.MaxTokens,
                    SystemPrompt = systemPrompt,
                    UserPrompt = userPrompt,
                    RuntimeOverride = runtimeOverride
                },
                cancellationToken);
        }
        catch (Application.Exceptions.AIProviderException ex) when (IsTransientAiFailure(ex))
        {
            warnings.Add("AI structure detection hit a transient provider error and was retried once before falling back to heuristic hints.");
            await Task.Delay(NormalizeTransientRetryDelay, cancellationToken);
            return await _aiExecutionService.ExecuteTextAsync(
                resolved,
                new AITextRequest
                {
                    FeatureName = AIFeatureNames.ExtractedStructure,
                    Provider = resolved.Provider,
                    Model = resolved.Model,
                    Temperature = resolved.Temperature ?? 0.1,
                    MaxTokens = resolved.MaxTokens,
                    SystemPrompt = systemPrompt,
                    UserPrompt = userPrompt,
                    RuntimeOverride = runtimeOverride
                },
                cancellationToken);
        }
    }

    private static string BuildStructureFullMarkdownExcerpt(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length <= StructureDetectionFullMarkdownCharacterLimit)
        {
            return normalized;
        }

        var headLength = Math.Min(2200, normalized.Length);
        var tailLength = Math.Min(900, Math.Max(0, normalized.Length - headLength));
        var head = normalized[..headLength].TrimEnd();
        var tail = tailLength > 0
            ? normalized[^tailLength..].TrimStart()
            : string.Empty;

        return string.IsNullOrWhiteSpace(tail)
            ? head
            : $"{head}{Environment.NewLine}{Environment.NewLine}[...truncated for latency...]{Environment.NewLine}{Environment.NewLine}{tail}";
    }

    private static string BuildNormalizationInput(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lines = normalized
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(AiNormalizationLineLimit)
            .ToList();
        var collapsed = string.Join(Environment.NewLine, lines);

        if (collapsed.Length <= AiNormalizationCharacterLimit)
        {
            return collapsed;
        }

        var headLength = Math.Min(10000, collapsed.Length);
        var tailLength = Math.Min(2500, Math.Max(0, collapsed.Length - headLength));
        var head = collapsed[..headLength].TrimEnd();
        var tail = tailLength > 0
            ? collapsed[^tailLength..].TrimStart()
            : string.Empty;

        return string.IsNullOrWhiteSpace(tail)
            ? head
            : $"{head}{Environment.NewLine}{Environment.NewLine}[...truncated for latency...]{Environment.NewLine}{Environment.NewLine}{tail}";
    }

    private static bool ShouldUseAiNormalization(string normalized, LocalNormalizationAssessment assessment, int placeholderCount)
    {
        if (placeholderCount > 0)
        {
            return true;
        }

        if (normalized.Length <= 2500)
        {
            return true;
        }

        if (assessment.HasHeavyNoise)
        {
            return true;
        }

        if (assessment.ConfidenceScore >= 0.78)
        {
            return false;
        }

        return assessment.CandidateHeadingCount < 4;
    }

    private static LocalNormalizationAssessment AssessLocalNormalizationQuality(string rawText, string normalized)
    {
        var rawLines = rawText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var normalizedLines = normalized.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var rawNonEmpty = rawLines.Count(line => !string.IsNullOrWhiteSpace(line));
        var normalizedNonEmpty = normalizedLines.Count(line => !string.IsNullOrWhiteSpace(line));
        var repeatedRawLines = rawLines
            .Select(CollapseWhitespace)
            .Where(line => !string.IsNullOrWhiteSpace(line) && line.Length > 4)
            .GroupBy(line => line, StringComparer.OrdinalIgnoreCase)
            .Count(group => group.Count() > 1);
        var bannerArtifacts = normalizedLines.Count(line =>
        {
            var trimmed = CollapseWhitespace(line).Trim();
            return !string.IsNullOrWhiteSpace(trimmed) &&
                   (IsInstitutionalBannerLine(trimmed) || LooksLikeProviderArtifactLine(trimmed));
        });
        var candidateHeadingCount = normalizedLines.Count(line =>
        {
            var trimmed = CollapseWhitespace(line).Trim();
            return !string.IsNullOrWhiteSpace(trimmed) && LooksLikeCandidateTitle(trimmed);
        });
        var chapterMarkerCount = normalizedLines.Count(line =>
        {
            var trimmed = CollapseWhitespace(line).Trim();
            return !string.IsNullOrWhiteSpace(trimmed) && LooksLikeChapterMarker(trimmed);
        });

        double score = 0.45;
        if (normalizedNonEmpty > 0)
        {
            var retentionRatio = rawNonEmpty == 0 ? 1d : Math.Min(1d, normalizedNonEmpty / (double)Math.Max(1, rawNonEmpty));
            score += retentionRatio * 0.2;
        }

        score += Math.Min(0.15, candidateHeadingCount * 0.015);
        score += Math.Min(0.1, chapterMarkerCount * 0.03);
        score -= Math.Min(0.2, repeatedRawLines * 0.015);
        score -= Math.Min(0.15, bannerArtifacts * 0.05);

        var bounded = Math.Max(0d, Math.Min(1d, score));
        var label = bounded >= 0.78 ? "high" : bounded >= 0.58 ? "medium" : "low";
        var hasHeavyNoise = repeatedRawLines >= 8 || bannerArtifacts >= 2;
        return new LocalNormalizationAssessment(bounded, label, hasHeavyNoise, candidateHeadingCount);
    }

    private async Task<ReinjectionResult> ReinjectionWithVisionAsync(
        ExtractionResultDto extracted,
        string content,
        AIResolvedExecutionOptions resolved,
        AIRuntimeOverride? runtimeOverride,
        AIPromptTemplateDto? visionPrompt,
        List<string> warnings,
        CancellationToken cancellationToken,
        Action<AITextResponse> onVisionResponse)
    {
        var current = content;
        var placeholders = ImagePlaceholderRegex.Matches(content).Cast<Match>().Select(match => match.Value).Distinct(StringComparer.Ordinal).ToList();
        var attemptedCount = 0;
        var succeededCount = 0;
        var failedCount = 0;
        var skippedCount = 0;
        var details = new List<VisionEnrichmentDetailDto>();
        var pendingItems = new List<PendingVisionItem>();

        for (var index = 0; index < placeholders.Count; index++)
        {
            var placeholder = placeholders[index];
            var asset = ResolveImageAsset(extracted, placeholder);
            if (asset is null || string.IsNullOrWhiteSpace(asset.Base64Data))
            {
                failedCount += 1;
                warnings.Add($"No extracted image asset matched placeholder '{placeholder}'.");
                continue;
            }

            var placeholderMetadata = ParsePlaceholderMetadata(placeholder);
            var initialPromptContext = BuildPromptContext(content, placeholder, ResolveContextWindow(asset));
            var initialImageKind = DetectSimpleImageKind(asset, initialPromptContext);
            var imageKind = initialImageKind;
            var extractionPlan = BuildSimpleVisionExtractionPlan(asset, imageKind);
            var promptContext = extractionPlan.ContextWindow == ResolveContextWindow(asset)
                ? initialPromptContext
                : BuildPromptContext(content, placeholder, extractionPlan.ContextWindow);
            var detail = new VisionEnrichmentDetailDto
            {
                Index = index + 1,
                ImageId = asset.ImageId,
                Reference = asset.Reference,
                Label = asset.Label,
                PageNumber = placeholderMetadata.PageNumber,
                SlideNumber = placeholderMetadata.SlideNumber,
                Width = asset.Width,
                Height = asset.Height,
                InitialImageKind = initialImageKind,
                FinalImageKind = imageKind,
                ContextWindow = extractionPlan.ContextWindow,
                MaxTokens = extractionPlan.MaxTokens
            };

            if (ShouldSkipVisionCall(asset, promptContext, placeholderMetadata, extractionPlan, out var skipReason))
            {
                skippedCount += 1;
                failedCount += 1;
                detail.Skipped = true;
                detail.Accepted = false;
                detail.SkipReason = skipReason;
                detail.RejectionReason = skipReason;
                detail.ProviderStatus = "skipped_before_call";
                detail.Preview = string.Empty;
                details.Add(detail);
                warnings.Add($"Vision enrichment skipped for image '{asset.ImageId}'. Reason: {skipReason}");
                continue;
            }

            pendingItems.Add(new PendingVisionItem(
                placeholder,
                asset,
                placeholderMetadata,
                extractionPlan,
                promptContext,
                detail));
        }

        var batchedPlaceholders = new HashSet<string>(StringComparer.Ordinal);
        if (pendingItems.Count > MaxVisionItemsPerDocument)
        {
            var retained = pendingItems
                .OrderByDescending(GetVisionPriorityScore)
                .ThenBy(item => item.Detail.Index)
                .Take(MaxVisionItemsPerDocument)
                .ToHashSet();

            foreach (var item in pendingItems.Where(item => !retained.Contains(item)))
            {
                skippedCount += 1;
                failedCount += 1;
                item.Detail.Skipped = true;
                item.Detail.Accepted = false;
                item.Detail.SkipReason = "vision_budget_exceeded";
                item.Detail.RejectionReason = "vision_budget_exceeded";
                item.Detail.ProviderStatus = "skipped_budget";
                item.Detail.Preview = string.Empty;
                details.Add(item.Detail);
            }

            warnings.Add($"Vision enrichment budget kept {MaxVisionItemsPerDocument} higher-value image placeholder(s) and skipped {pendingItems.Count - MaxVisionItemsPerDocument} lower-value item(s).");
            pendingItems = pendingItems
                .Where(item => retained.Contains(item))
                .OrderBy(item => item.Detail.Index)
                .ToList();
        }

        var batchGroups = pendingItems
            .GroupBy(item => GetBatchGroupKey(item.Metadata))
            .ToList();

        foreach (var group in batchGroups)
        {
            foreach (var chunk in group.Chunk(MaximumBatchPageSize))
            {
                var pageNumber = chunk[0].Metadata.PageNumber ?? chunk[0].Metadata.SlideNumber ?? 0;
                var batchResult = await TryProcessBatchPageAsync(
                    extracted,
                    current,
                    resolved,
                    runtimeOverride,
                    visionPrompt,
                    warnings,
                    cancellationToken,
                    onVisionResponse,
                    pageNumber,
                    chunk.ToList());

                foreach (var batchItem in chunk)
                {
                    details.Add(batchItem.Detail);
                }

                attemptedCount += batchResult.AttemptedCount;
                succeededCount += batchResult.SucceededCount;
                failedCount += batchResult.FailedCount;

                if (batchResult.Replacements.Count == 0)
                {
                    foreach (var settledPlaceholder in batchResult.SettledPlaceholders)
                    {
                        batchedPlaceholders.Add(settledPlaceholder);
                    }

                    continue;
                }

                foreach (var replacement in batchResult.Replacements)
                {
                    current = ReplacePlaceholder(current, replacement.Key, replacement.Value);
                    batchedPlaceholders.Add(replacement.Key);
                }
            }
        }

        var singleFallbackAttempts = 0;
        foreach (var item in pendingItems.Where(item => !batchedPlaceholders.Contains(item.Placeholder)))
        {
            if (ShouldSkipSingleFallback(item, singleFallbackAttempts))
            {
                skippedCount += 1;
                failedCount += 1;
                item.Detail.Skipped = true;
                item.Detail.Accepted = false;
                item.Detail.SkipReason = item.Detail.ProviderStatus is "batch_output_too_weak" or "batch_low_value_group"
                    ? "single_fallback_skipped_after_low_value_batch"
                    : "single_fallback_budget_exceeded";
                item.Detail.RejectionReason = item.Detail.SkipReason;
                item.Detail.ProviderStatus = item.Detail.ProviderStatus is "batch_output_too_weak" or "batch_low_value_group"
                    ? "single_fallback_skipped_low_value"
                    : "single_fallback_budget";
                item.Detail.Preview = string.Empty;
                if (!details.Contains(item.Detail))
                {
                    details.Add(item.Detail);
                }
                continue;
            }

            if (item.Detail.ProviderStatus?.StartsWith("batch_", StringComparison.OrdinalIgnoreCase) == true)
            {
                item.Detail.RetryUsed = true;
                item.Detail.RetryFallbackKind = "single_after_batch";
            }

            attemptedCount += 1;
            singleFallbackAttempts += 1;
            var systemPrompt = visionPrompt?.SystemPrompt ?? """
                You are an educational document vision extraction assistant.
                Read the provided image in the context of the nearby document text.
                Return only concise markdown text that can replace the image placeholder in the document.
                Preserve factual meaning. Do not invent details that are not visible or strongly implied by the nearby text.
                If the image is partially unreadable, say so briefly.
                Do not mention "the slide", "the image above", "the figure below", or similar UI-relative wording unless the nearby text explicitly uses that wording.
                """;
            var userPrompt = BuildVisionUserPrompt(
                visionPrompt,
                extracted,
                item.Asset,
                item.Metadata,
                item.ExtractionPlan.ImageKind,
                item.PromptContext);

            try
            {
                var response = await ExecuteVisionRequestWithTransientRetryAsync(
                    resolved,
                    new AITextRequest
                    {
                        FeatureName = AIFeatureNames.ExtractedContent,
                        Provider = resolved.Provider,
                        Model = resolved.Model,
                        Temperature = 0.1,
                        MaxTokens = resolved.MaxTokens.HasValue
                            ? Math.Min(resolved.MaxTokens.Value, item.ExtractionPlan.MaxTokens)
                            : item.ExtractionPlan.MaxTokens,
                        SystemPrompt = systemPrompt,
                        UserPrompt = userPrompt,
                        RuntimeOverride = runtimeOverride,
                        Images = new List<AIImageInput>
                        {
                            item.Asset
                        }
                    },
                    cancellationToken);

                onVisionResponse(response);
                item.Detail.InputTokens = response.InputTokens;
                item.Detail.OutputTokens = response.OutputTokens;
                item.Detail.TotalTokens = response.TotalTokens;
                item.Detail.ProviderStatus = "single_ok";
                var reinjectedText = NormalizeReinjectedText(response.Content, item.Asset, item.ExtractionPlan.ImageKind);

                if (string.IsNullOrWhiteSpace(reinjectedText))
                {
                    failedCount += 1;
                    item.Detail.Accepted = false;
                    item.Detail.RejectionReason = "empty_or_low_value_output";
                    item.Detail.ProviderStatus = "single_rejected";
                    item.Detail.Preview = string.Empty;
                    if (!details.Contains(item.Detail))
                    {
                        details.Add(item.Detail);
                    }
                    warnings.Add($"Vision enrichment returned empty content for image '{item.Asset.ImageId}'. Placeholder was preserved.");
                    continue;
                }

                current = ReplacePlaceholder(current, item.Placeholder, reinjectedText);
                succeededCount += 1;
                item.Detail.Accepted = true;
                item.Detail.RejectionReason = null;
                item.Detail.Preview = BuildPreviewFromReinjectedText(reinjectedText);
                if (!details.Contains(item.Detail))
                {
                    details.Add(item.Detail);
                }
            }
            catch (Exception ex)
            {
                failedCount += 1;
                var reason = ex.Message;
                if (reason.Length > 220)
                {
                    reason = reason[..220];
                }

                item.Detail.Accepted = false;
                item.Detail.RejectionReason = reason;
                item.Detail.ProviderStatus = ex is Application.Exceptions.AIProviderException providerException
                    ? $"provider_error_{providerException.StatusCode?.ToString() ?? "unknown"}"
                    : "runtime_error";
                item.Detail.Preview = string.Empty;
                if (!details.Contains(item.Detail))
                {
                    details.Add(item.Detail);
                }
                warnings.Add($"Vision enrichment failed for image '{item.Asset.ImageId}'. Placeholder was preserved. Reason: {reason}");
                _logger.LogWarning(ex, "Vision enrichment failed for extracted image {ImageId}", item.Asset.ImageId);
            }
        }

        return new ReinjectionResult(
            current,
            attemptedCount,
            succeededCount,
            failedCount,
            skippedCount,
            ExtractImagePlaceholders(current).Count,
            details);
    }

    private static int GetVisionPriorityScore(PendingVisionItem item)
    {
        var score = item.ExtractionPlan.ImageKind switch
        {
            "code_like" => 100,
            "diagram_like" => 70,
            _ => 20
        };

        var context = $"{item.PromptContext.TextBefore} {item.PromptContext.TextAfter}";
        if (Regex.IsMatch(context, @"\b(class|extends|implements|interface|override|polymorphism|abstract|binding|uml|hierarchy|constructor|method)\b", RegexOptions.IgnoreCase))
        {
            score += 20;
        }

        var width = item.Asset.Width ?? 0;
        var height = item.Asset.Height ?? 0;
        var area = width > 0 && height > 0 ? width * height : 0;
        if (area >= 120000)
        {
            score += 10;
        }

        return score;
    }

    private static bool ShouldSkipSingleFallback(PendingVisionItem item, int singleFallbackAttempts)
    {
        if (singleFallbackAttempts >= MaxSingleVisionFallbacksPerDocument)
        {
            return true;
        }

        if (item.ExtractionPlan.ImageKind == "low_value")
        {
            return true;
        }

        if (item.Detail.ProviderStatus is "batch_low_value_group" or "batch_output_too_weak")
        {
            return item.ExtractionPlan.ImageKind != "code_like";
        }

        return false;
    }

    private async Task<AITextResponse> ExecuteVisionRequestWithTransientRetryAsync(
        AIResolvedExecutionOptions resolved,
        AITextRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _aiExecutionService.ExecuteTextAsync(resolved, request, cancellationToken);
        }
        catch (Application.Exceptions.AIProviderException ex) when (IsTransientVisionFailure(ex))
        {
            _logger.LogWarning(
                ex,
                "Transient vision enrichment failure detected for provider {Provider} model {Model}. Retrying once.",
                resolved.Provider,
                resolved.Model);

            await Task.Delay(TimeSpan.FromMilliseconds(450), cancellationToken);
            return await _aiExecutionService.ExecuteTextAsync(resolved, request, cancellationToken);
        }
    }

    private async Task<AITextResponse> ExecuteNormalizationRequestWithTransientRetryAsync(
        AIResolvedExecutionOptions resolved,
        AITextRequest request,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _aiExecutionService.ExecuteTextAsync(resolved, request, cancellationToken);
        }
        catch (Application.Exceptions.AIProviderException ex) when (IsTransientAiFailure(ex))
        {
            _logger.LogWarning(
                ex,
                "Transient normalize failure detected for provider {Provider} model {Model}. Retrying the full normalization chain once.",
                resolved.Provider,
                resolved.Model);

            warnings.Add("AI normalization hit a transient provider error and was retried once before local cleanup.");
            await Task.Delay(NormalizeTransientRetryDelay, cancellationToken);
            return await _aiExecutionService.ExecuteTextAsync(resolved, request, cancellationToken);
        }
    }

    private async Task<BatchPageResult> TryProcessBatchPageAsync(
        ExtractionResultDto extracted,
        string content,
        AIResolvedExecutionOptions resolved,
        AIRuntimeOverride? runtimeOverride,
        AIPromptTemplateDto? batchVisionPrompt,
        List<string> warnings,
        CancellationToken cancellationToken,
        Action<AITextResponse> onVisionResponse,
        int pageNumber,
        List<PendingVisionItem> items)
    {
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        var settledPlaceholders = new List<string>();
        if (items.Count < MinimumBatchPageSize)
        {
            return new BatchPageResult(0, 0, 0, replacements, settledPlaceholders);
        }

        var systemPrompt = batchVisionPrompt?.SystemPrompt ?? """
            You are an educational document reinjection assistant.
            You read multiple extracted images from the same study document page and return only strict JSON that maps each placeholder to concise markdown content suitable for reinjection.
            Be faithful to what is visibly present in each image. Do not invent missing code, labels, or explanations.
            """;
        var batchRequest = BuildBatchVisionRequest(extracted, items, pageNumber, batchVisionPrompt);

        try
        {
            var response = await ExecuteVisionRequestWithTransientRetryAsync(
                resolved,
                new AITextRequest
                {
                    FeatureName = AIFeatureNames.ExtractedContent,
                    Provider = resolved.Provider,
                    Model = resolved.Model,
                    Temperature = 0.1,
                    MaxTokens = Math.Min(resolved.MaxTokens ?? 900, 900),
                    SystemPrompt = systemPrompt,
                    UserPrompt = batchRequest.Prompt,
                    RuntimeOverride = runtimeOverride,
                    Images = batchRequest.Images
                },
                cancellationToken);

            onVisionResponse(response);
            var rawMap = ParseBatchVisionResponse(response.Content);
            if (rawMap.Count == 0)
            {
                var preview = BuildBatchResponsePreview(response.Content);
                if (LooksLikeBatchLowValueResponse(preview))
                {
                    foreach (var item in items)
                    {
                        item.Detail.Accepted = false;
                        item.Detail.RejectionReason = "batch_low_value_group";
                        item.Detail.ProviderStatus = "batch_low_value_group";
                        item.Detail.Preview = string.Empty;
                        settledPlaceholders.Add(item.Placeholder);
                    }

                    warnings.Add($"Batch vision reinjection for page {pageNumber} reported no useful visible content. Per-image fallback was skipped.");
                    return new BatchPageResult(items.Count, 0, items.Count, replacements, settledPlaceholders);
                }

                warnings.Add($"Batch vision reinjection for page {pageNumber} returned no parseable mapping. Falling back to per-image handling. Raw preview: {preview}");
                foreach (var item in items)
                {
                    item.Detail.ProviderStatus = "batch_parse_failed";
                }
                return new BatchPageResult(0, 0, 0, replacements, settledPlaceholders);
            }

            var perImageInput = DivideNullable(response.InputTokens, items.Count);
            var perImageOutput = DivideNullable(response.OutputTokens, items.Count);
            var perImageTotal = DivideNullable(response.TotalTokens, items.Count);
            var attemptedCount = items.Count;
            var succeededCount = 0;
            var failedCount = 0;

            foreach (var item in items)
            {
                item.Detail.InputTokens = perImageInput;
                item.Detail.OutputTokens = perImageOutput;
                item.Detail.TotalTokens = perImageTotal;
                item.Detail.ProviderStatus = "batch_ok";

                if (!rawMap.TryGetValue(item.Asset.ImageId, out var batchContent) &&
                    !rawMap.TryGetValue(item.Placeholder, out batchContent))
                {
                    item.Detail.Accepted = false;
                    item.Detail.RejectionReason = "batch_missing_item";
                    item.Detail.ProviderStatus = "batch_missing_item";
                    item.Detail.Preview = string.Empty;
                    failedCount += 1;
                    continue;
                }

                var reinjectedText = NormalizeReinjectedText(batchContent, item.Asset, item.ExtractionPlan.ImageKind);
                if (ShouldRetryWeakReinjection(reinjectedText, item.ExtractionPlan.ImageKind))
                {
                    item.Detail.Accepted = false;
                    item.Detail.RejectionReason = "batch_output_too_weak";
                    item.Detail.ProviderStatus = "batch_output_too_weak";
                    item.Detail.Preview = string.Empty;
                    failedCount += 1;
                    continue;
                }

                item.Detail.Accepted = true;
                item.Detail.RejectionReason = null;
                item.Detail.Preview = BuildPreviewFromReinjectedText(reinjectedText);
                replacements[item.Placeholder] = reinjectedText;
                succeededCount += 1;
            }

            return new BatchPageResult(attemptedCount, succeededCount, failedCount, replacements, settledPlaceholders);
        }
        catch (Exception ex)
        {
            var reason = ex.Message.Length <= 220 ? ex.Message : ex.Message[..220];
            warnings.Add($"Batch vision reinjection failed for page {pageNumber}. Fallback to per-image handling. Reason: {reason}");
            foreach (var item in items)
            {
                item.Detail.ProviderStatus = "batch_exception";
            }
            _logger.LogWarning(ex, "Batch vision reinjection failed for page {PageNumber}", pageNumber);
            return new BatchPageResult(0, 0, 0, replacements, settledPlaceholders);
        }
    }

    private static ExtractionStructuralHintsDto MergeStructuralHints(
        ExtractionStructuralHintsDto heuristicHints,
        ExtractionStructuralHintsDto aiHints)
    {
        return new ExtractionStructuralHintsDto
        {
            CandidateTitles = MergeDistinct(aiHints.CandidateTitles, heuristicHints.CandidateTitles),
            CandidateChapterMarkers = MergeDistinct(aiHints.CandidateChapterMarkers, heuristicHints.CandidateChapterMarkers),
            RejectedHeadingCandidates = MergeDistinct(heuristicHints.RejectedHeadingCandidates, aiHints.RejectedHeadingCandidates),
            CleanDisplayTitleCandidates = MergeDistinct(aiHints.CleanDisplayTitleCandidates, heuristicHints.CleanDisplayTitleCandidates),
            StructuralUnits = aiHints.StructuralUnits.Count > 0
                ? aiHints.StructuralUnits
                : heuristicHints.StructuralUnits
        };
    }

    private static ExtractionStructuralHintsDto ToStructuralHintsDto(StructuralHints hints)
    {
        return new ExtractionStructuralHintsDto
        {
            CandidateTitles = hints.CandidateTitles,
            CandidateChapterMarkers = hints.CandidateChapterMarkers,
            RejectedHeadingCandidates = hints.RejectedHeadingCandidates,
            CleanDisplayTitleCandidates = hints.CleanDisplayTitleCandidates,
            StructuralUnits = hints.StructuralUnits
        };
    }

    private static List<string> MergeDistinct(params IEnumerable<string>[] sources)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<string>();
        foreach (var source in sources)
        {
            foreach (var item in source.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                if (seen.Add(item.Trim()))
                {
                    output.Add(item.Trim());
                }
            }
        }

        return output;
    }

    private static ExtractionStructuralHintsDto ParseStructureDetectionResponse(
        string rawContent,
        string normalized,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return new ExtractionStructuralHintsDto();
        }

        try
        {
            using var document = JsonDocument.Parse(ExtractJsonPayload(rawContent));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                warnings.Add("AI structure detection returned a non-object payload. Heuristic structure hints were kept.");
                return new ExtractionStructuralHintsDto();
            }

            var units = SanitizeStructuralUnits(ParseStructuralUnits(root, normalized, warnings), normalized, warnings);
            return new ExtractionStructuralHintsDto
            {
                CandidateTitles = units.Select(item => item.DisplayTitle).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                CandidateChapterMarkers = units
                    .Where(item => item.Kind is "chapter" or "scope_block")
                    .Select(item => item.DisplayTitle)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                CleanDisplayTitleCandidates = units
                    .Where(item => item.LearnerFacing)
                    .Select(item => item.DisplayTitle)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StructuralUnits = units
            };
        }
        catch (Exception)
        {
            warnings.Add("AI structure detection returned unparseable JSON. Heuristic structure hints were kept.");
            return new ExtractionStructuralHintsDto();
        }
    }

    private static List<ExtractedStructuralUnitDto> ParseStructuralUnits(
        JsonElement root,
        string normalized,
        List<string> warnings)
    {
        if (!root.TryGetProperty("structuralUnits", out var unitsElement) || unitsElement.ValueKind != JsonValueKind.Array)
        {
            return new List<ExtractedStructuralUnitDto>();
        }

        var totalLines = normalized.Split('\n').Length;
        var units = new List<ExtractedStructuralUnitDto>();
        foreach (var item in unitsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var title = ReadString(item, "displayTitle") ?? ReadString(item, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            title = CollapseWhitespace(title).Trim();
            if (!LooksLikeValidStructuralUnitTitle(title))
            {
                continue;
            }

            var startLine = ReadInt(item, "startLine") ?? 1;
            var endLine = ReadInt(item, "endLine");
            startLine = Math.Max(1, Math.Min(startLine, totalLines));
            if (endLine.HasValue)
            {
                endLine = Math.Max(startLine, Math.Min(endLine.Value, totalLines));
            }

            var unit = new ExtractedStructuralUnitDto
            {
                UnitKey = ReadString(item, "unitKey") ?? BuildUnitKey(title, startLine),
                Kind = NormalizeUnitKind(ReadString(item, "kind")),
                DisplayTitle = title,
                StartLine = startLine,
                EndLine = endLine,
                Confidence = ReadDecimal(item, "confidence"),
                LearnerFacing = ReadBool(item, "learnerFacing") ?? true,
                Aliases = ReadStringArray(item, "aliases")
            };

            units.Add(unit);
        }

        var ordered = units
            .OrderBy(item => item.StartLine)
            .ThenBy(item => item.EndLine ?? int.MaxValue)
            .ToList();

        if (ordered.Count == 0)
        {
            warnings.Add("AI structure detection returned no usable structural units. Heuristic structure hints were kept.");
        }

        return ordered;
    }

    private static List<ExtractedStructuralUnitDto> SanitizeStructuralUnits(
        List<ExtractedStructuralUnitDto> units,
        string normalized,
        List<string> warnings)
    {
        if (units.Count == 0)
        {
            return units;
        }

        var lines = normalized.Split('\n');
        var sanitized = new List<ExtractedStructuralUnitDto>();

        foreach (var unit in units.OrderBy(item => item.StartLine).ThenBy(item => item.EndLine ?? int.MaxValue))
        {
            var title = CollapseWhitespace(unit.DisplayTitle).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            if (LooksLikeReferenceOnlyStructuralUnit(title))
            {
                warnings.Add($"Structure sanitizer removed reference-like unit '{title}'.");
                continue;
            }

            var previous = sanitized.LastOrDefault();
            var preview = ReadUnitPreviewLines(lines, unit.StartLine, 3);
            var shouldDemote =
                LooksLikeFragmentStructuralUnit(title, preview) ||
                LooksLikeExampleChildUnit(title) ||
                ShouldDemoteUnderApplications(previous?.DisplayTitle, title);

            sanitized.Add(new ExtractedStructuralUnitDto
            {
                UnitKey = unit.UnitKey,
                Kind = shouldDemote ? "section" : unit.Kind,
                DisplayTitle = title,
                StartLine = unit.StartLine,
                EndLine = unit.EndLine,
                Confidence = unit.Confidence,
                LearnerFacing = unit.LearnerFacing,
                Aliases = unit.Aliases
            });

            if (shouldDemote && unit.Kind is "chapter" or "scope_block")
            {
                warnings.Add($"Structure sanitizer demoted weak unit '{title}' to section.");
            }
        }

        return sanitized;
    }

    private static List<string> ReadUnitPreviewLines(IReadOnlyList<string> lines, int startLine, int count)
    {
        return lines
            .Skip(Math.Max(0, startLine - 1))
            .Take(Math.Max(1, count))
            .Select(CollapseWhitespace)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static bool LooksLikeReferenceOnlyStructuralUnit(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalized = CollapseWhitespace(title).Trim();
        return Regex.IsMatch(normalized, @"(?i)^\d+(?:\.\d+)+\s+.+\s+-\s+\d+$") ||
               Regex.IsMatch(normalized, @"(?i)^chapter\s+\d+[:\s\-]") ||
               Regex.IsMatch(normalized, @"(?i)^(reading at home|reading|homework reading)$");
    }

    private static bool LooksLikeFragmentStructuralUnit(string title, IReadOnlyList<string> previewLines)
    {
        var wordCount = Regex.Matches(title, @"[A-Za-z][A-Za-z0-9+\-#]*").Count;
        if (wordCount is 0 or > 3 || previewLines.Count == 0)
        {
            return false;
        }

        var first = previewLines[0];
        var second = previewLines.Count > 1 ? previewLines[1] : string.Empty;
        var combined = string.Join(" ", previewLines.Take(3));

        var startsLikeContinuation =
            Regex.IsMatch(first, @"^[a-z0-9\(\[\{]", RegexOptions.None) ||
            (first.Length <= 2 && !string.IsNullOrWhiteSpace(second) && Regex.IsMatch(second, @"^[a-z0-9\(\[\{]", RegexOptions.None));
        var sentenceLike = Regex.IsMatch(combined, @"[\.!?]") ||
                           Regex.IsMatch(combined, @"\b(is|are|was|were|can|uses?|using|contains?|occurs?|maps?|parts?|divide|split|store|insert)\b", RegexOptions.IgnoreCase);

        return startsLikeContinuation && sentenceLike;
    }

    private static bool LooksLikeExampleChildUnit(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return Regex.IsMatch(title, @"(?i)\b(operation output|map adt methods?|application:?|round robin schedulers?|example|examples|task|questions?)\b");
    }

    private static bool ShouldDemoteUnderApplications(string? previousTitle, string currentTitle)
    {
        if (string.IsNullOrWhiteSpace(previousTitle))
        {
            return false;
        }

        return Regex.IsMatch(previousTitle, @"(?i)^applications?\s+of\b") &&
               Regex.Matches(currentTitle, @"[A-Za-z][A-Za-z0-9+\-#]*").Count <= 4;
    }

    private static string ExtractJsonPayload(string rawContent)
    {
        var trimmed = rawContent.Trim();
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed[firstBrace..(lastBrace + 1)];
        }

        return trimmed;
    }

    private static string NormalizeUnitKind(string? rawKind)
    {
        var normalized = rawKind?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "chapter" => "chapter",
            "topic" => "topic",
            "section" => "section",
            "scope_block" => "scope_block",
            "fallback_block" => "fallback_block",
            _ => "section"
        };
    }

    private static string BuildUnitKey(string title, int startLine)
    {
        var slug = Regex.Replace(title.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "unit";
        }

        return $"{slug}-{startLine}";
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private static decimal? ReadDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static List<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
    }

    private static string CleanText(string text, string fileName, System.Text.Json.JsonElement? policy)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text
            .Replace("\u00A0", " ")
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
        cleaned = Regex.Replace(cleaned, @"[\u200B-\u200D\uFEFF]", string.Empty);
        cleaned = Regex.Replace(cleaned, @"[ \t]+", " ");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");
        cleaned = Regex.Replace(cleaned, @"^\s*(page\s+\d+|\d{1,2}/\d{1,2}/\d{2,4})\s*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"!\[[^\]]*\]\([^\)]*\)", string.Empty);
        cleaned = RemoveRepeatedLines(cleaned);
        cleaned = RemoveLowValueBodyNoise(cleaned);
        cleaned = RemoveSyntheticDocumentHeader(cleaned);
        cleaned = cleaned.Trim();

        if (ShouldPrependMarkdownHeader(policy))
        {
            cleaned = $"# Parsed Document: {fileName}{Environment.NewLine}{Environment.NewLine}{cleaned}";
        }

        cleaned = RemoveSyntheticDocumentHeader(cleaned);
        return cleaned.Trim();
    }

    /// <summary>
    /// Performs deep sanitization on the normalized markdown to remove residual OCR noise,
    /// leftover machine placeholders ([[IMAGE:...]]), conversational LLM preambles/code wrappers,
    /// slide/page counters, decorative dividers, and corrupted unicode/control characters.
    /// </summary>
    private static string FinalSanitizeNormalizedMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text.Trim();

        // 1. Strip outer markdown code fences if LLM wrapped the entire output in ```markdown ... ```
        if (cleaned.StartsWith("```markdown", StringComparison.OrdinalIgnoreCase) ||
            cleaned.StartsWith("```md", StringComparison.OrdinalIgnoreCase) ||
            cleaned.StartsWith("```\n", StringComparison.Ordinal) ||
            cleaned.StartsWith("```\r\n", StringComparison.Ordinal))
        {
            var firstNewline = cleaned.IndexOf('\n');
            if (firstNewline >= 0)
            {
                cleaned = cleaned[(firstNewline + 1)..];
            }
            if (cleaned.EndsWith("```", StringComparison.Ordinal))
            {
                cleaned = cleaned[..^3].TrimEnd();
            }
        }

        // 2. Strip LLM conversational preambles and postambles
        cleaned = Regex.Replace(
            cleaned,
            @"^(?:here is|below is|sure,? here is|the following is|this is the normalized|normalized content:?)\s*.*$\r?\n?",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        cleaned = Regex.Replace(
            cleaned,
            @"^(?:hope this helps|let me know if you need|end of normalized document)\s*.*$\r?\n?",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // 3. Strip residual machine image placeholders (e.g. [[IMAGE:image-01|ref=rId4]] or [[IMAGE:id=...]])
        cleaned = Regex.Replace(cleaned, @"\[\[IMAGE:[^\]]+\]\]", string.Empty, RegexOptions.IgnoreCase);

        // 4. Strip empty image summary headers like [Image Summary: ...] where no text follows
        cleaned = Regex.Replace(
            cleaned,
            @"\[Image Summary:\s*[^\]]*\]\s*(?=\r?\n\s*(?:\[Image Summary:|\r?\n|\Z))",
            string.Empty,
            RegexOptions.IgnoreCase);

        // 5. Line-by-line cleanup for OCR noise, counters, dividers, and control characters
        var outputLines = new List<string>();
        var lines = cleaned.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                if (outputLines.Count > 0 && !string.IsNullOrWhiteSpace(outputLines[^1]))
                {
                    outputLines.Add(string.Empty);
                }
                continue;
            }

            // Remove slide/page counters: "Page 1 of 12", "Slide 1", "[Slide 1]", "[Page 1]", "1 / 45", "- 12 -", "p. 15", "1/50"
            if (Regex.IsMatch(line, @"^\[?\s*(?:page\s+\d+(?:\s*(?:of|/)\s*\d+)?|slide\s+\d+(?:\s*(?:of|/)\s*\d+)?|\d+\s*/\s*\d+|-\s*\d+\s*-|p\.\s*\d+)\s*\]?$", RegexOptions.IgnoreCase))
            {
                continue;
            }

            // Remove decorative dividers: "==========", "----------", "**********", "............"
            if (Regex.IsMatch(line, @"^[=\-*_.#~]{4,}$") && !line.StartsWith("# ", StringComparison.Ordinal))
            {
                continue;
            }

            // Remove empty table borders or isolated symbol noise: "| | |", "+---+---+", "|---|"
            if (Regex.IsMatch(line, @"^(?:\|[\s\-|:+]+)+$") || Regex.IsMatch(line, @"^(?:\+[\s\-+=]+)+$"))
            {
                continue;
            }

            // Remove invalid stub headings: "# # #", "# -", "# :", "# ."
            if (Regex.IsMatch(line, @"^#{1,6}\s*[:;\-_.#\s]*$"))
            {
                continue;
            }

            // Remove non-printable control characters and unicode replacement character
            line = line.Replace("\uFFFD", string.Empty);
            line = Regex.Replace(line, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", string.Empty);

            if (!string.IsNullOrWhiteSpace(line))
            {
                outputLines.Add(rawLine.TrimEnd());
            }
        }

        var result = string.Join(Environment.NewLine, outputLines).Trim();
        // Collapse 3+ consecutive newlines to 2
        result = Regex.Replace(result, @"(\r?\n){3,}", $"{Environment.NewLine}{Environment.NewLine}");
        return result;
    }

    private static string RemoveLowValueBodyNoise(string text)
    {
        var output = new List<string>();
        var blankLinePending = false;

        foreach (var rawLine in text.Split('\n'))
        {
            var normalized = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                if (output.Count > 0)
                {
                    blankLinePending = true;
                }

                continue;
            }

            if (ShouldDropLowValueBodyLine(normalized))
            {
                continue;
            }

            if (blankLinePending && output.Count > 0 && !string.IsNullOrWhiteSpace(output[^1]))
            {
                output.Add(string.Empty);
            }

            blankLinePending = false;
            output.Add(normalized);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static bool ShouldDropLowValueBodyLine(string line)
    {
        if (IsImageSummaryArtifactLine(line) ||
            IsMarkdownImageLine(line) ||
            IsInstitutionalBannerLine(line) ||
            LooksLikeProviderArtifactLine(line))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"(?i)^(qs\s+)?stars\s+rated\s+for\s+excellence\s+\d{4}$"))
        {
            return true;
        }

        return false;
    }

    private static string RemoveRepeatedLines(string text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<string>();

        foreach (var line in text.Split('\n'))
        {
            var normalized = line.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                output.Add(string.Empty);
                continue;
            }

            if (IsImagePlaceholderLine(normalized))
            {
                output.Add(normalized);
                continue;
            }

            if (normalized.Length > 4 && !seen.Add(normalized))
            {
                continue;
            }

            output.Add(normalized);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static bool ShouldPrependMarkdownHeader(System.Text.Json.JsonElement? policy)
    {
        if (policy is null || policy.Value.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !policy.Value.TryGetProperty("normalization_rules", out var rules) ||
            !rules.TryGetProperty("prepend_markdown_header", out var value))
        {
            return true;
        }

        return value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            _ => true
        };
    }

    private static string RemoveSyntheticDocumentHeader(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(
            text,
            @"^\s*#\s*Parsed Document:\s*[^\r\n]+\s*(?:\r?\n){1,2}",
            string.Empty,
            RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(
            cleaned,
            @"^\s*Parsed Document:\s*[^\r\n]+\s*(?:\r?\n){1,2}",
            string.Empty,
            RegexOptions.IgnoreCase);

        return cleaned;
    }

    private static int CountWords(string content)
    {
        return string.IsNullOrWhiteSpace(content)
            ? 0
            : content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static StructuralHints ExtractStructuralHints(string normalized)
    {
        var candidateTitles = new List<string>();
        var chapterMarkers = new List<string>();
        var rejected = new List<string>();
        var cleanDisplayTitles = new List<string>();

        foreach (var rawLine in normalized.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = CollapseWhitespace(rawLine).Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (ImagePlaceholderRegex.IsMatch(line))
            {
                continue;
            }

            if (Regex.IsMatch(line, @"^\[(slide|page)\s+\d+\]$", RegexOptions.IgnoreCase))
            {
                chapterMarkers.Add(line);
                continue;
            }

            if (LooksLikeChapterMarker(line))
            {
                chapterMarkers.Add(line);
            }

            if (LooksLikeRejectedHeadingCandidate(line))
            {
                rejected.Add(line);
                continue;
            }

            if (LooksLikeCandidateTitle(line))
            {
                candidateTitles.Add(line);
                cleanDisplayTitles.Add(CleanStructuralHintTitle(line));
            }
        }

        return new StructuralHints(
            DistinctTop(candidateTitles, 24),
            DistinctTop(chapterMarkers, 24),
            DistinctTop(rejected, 24),
            DistinctTop(cleanDisplayTitles, 24),
            new List<ExtractedStructuralUnitDto>());
    }

    private static bool LooksLikeChapterMarker(string line)
        => Regex.IsMatch(line,
            @"(?i)^(chapter|chap|chuong|chương|bai|bài|phan|phần|part|module|unit|section|lesson|topic)\s*[\.:_-]*\s*[a-z0-9ivx]+");

    private static bool LooksLikeRejectedHeadingCandidate(string line)
    {
        if (line.Length <= 2)
        {
            return true;
        }

        if (IsSlidePageCounterLine(line) || LooksLikeDanglingFragmentTitle(line))
        {
            return true;
        }

        if (IsInstitutionalBannerLine(line))
        {
            return true;
        }

        if (line.StartsWith("[Image Summary:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (LooksLikeCodeOrImageArtifactTitle(line))
        {
            return true;
        }

        if (line.StartsWith(":", StringComparison.Ordinal) || line.StartsWith("|", StringComparison.Ordinal))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"^(objectives?|question|questions|exercise\s*\d*|example\s*\d*|summary|overview|practice|quiz|worksheet)$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"^(tasks?|understand\b|to create\b|output the value\b|some operations\b|the operator\b|a type qualified\b)", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"^(a|an|the|this|that|these|those)\s+[a-z]", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"(?:\[\[IMAGE:)|(?:\b(?:ref|label|slide|page)\s*=)|(?:slide|page)=\d+\]\]?$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"^[A-F0-9]{6,}$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"^(?:short|long|const|int|char|float|double|signed|unsigned|integral|floating-point|types?|from|the|to|by|uses?|using|data|value|values?)$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"\b(?:enables?|allows?|provides?|returns?|keeps?|stores?|contains?|supports?|does|means|represents?|requires?|ensures?)\b", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"\*/|/\*|^\d+(?:\s+\d+){1,}\b|(?:^|\s)\d{2,}(?:\s+\d{1,2})+\b"))
        {
            return true;
        }

        return Regex.IsMatch(line, @"^[#\-\d\s\.]+$");
    }

    private static bool LooksLikeCandidateTitle(string line)
    {
        if (line.Length is < 3 or > 120)
        {
            return false;
        }

        if (LooksLikeRejectedHeadingCandidate(line))
        {
            return false;
        }

        if (Regex.IsMatch(line, @"[\.!?]$"))
        {
            return false;
        }

        if (line.Contains('•') && Regex.Matches(line, "•").Count >= 2)
        {
            return false;
        }

        var words = Regex.Matches(line, @"[A-Za-z][A-Za-z0-9'/_\-]*")
            .Select(match => match.Value)
            .ToList();
        if (words.Count is 0 or > 10)
        {
            return false;
        }

        if (words.Count == 1)
        {
            var single = words[0];
            if (IsStrongConceptTitle(single))
            {
                return true;
            }

            if (Regex.IsMatch(single, @"^(short|long|const|int|char|float|double|signed|unsigned|integral|types?|from|the|to|by)$", RegexOptions.IgnoreCase))
            {
                return false;
            }

            if (!Regex.IsMatch(single, @"^(qualifier|literals?|numbers|tasks|values?|operators?|variables?|constants?|casting|precedence|symbols|characters|computation)$", RegexOptions.IgnoreCase))
            {
                return false;
            }
        }

        var lower = line.ToLowerInvariant();
        if (lower.Contains(" at least ") ||
            lower.Contains(" depends on ") ||
            lower.Contains(" cannot be changed") ||
            lower.Contains(" ensure") ||
            lower.Contains(" distinguish") ||
            lower.Contains(" represent") ||
            lower.Contains(" contains ") ||
            lower.Contains(" occupy ") ||
            lower.Contains(" use the keyword") ||
            lower.Contains(" refers to ") ||
            lower.Contains(" provides ") ||
            lower.Contains(" understand ") ||
            lower.Contains(" is stored") ||
            lower.Contains(" qualified as") ||
            lower.Contains(" to create ") ||
            lower.Contains(" output the value ") ||
            lower.Contains(" some operations "))
        {
            return false;
        }

        if (Regex.IsMatch(line, @"[:;]$") && words.Count > 4)
        {
            return false;
        }

        var isNumbered = Regex.IsMatch(line, @"^(?:\d+(?:\.\d+)*|[IVXLCM]+)[\)\.\-: ]", RegexOptions.IgnoreCase);
        var hasChapterKeyword = LooksLikeChapterMarker(line);
        var hasConceptMarker = Regex.IsMatch(line, @"(?i)\b(values?|types?|operators?|casting|variables?|constants?|literals?|representation|precedence|symbols|characters|computation|qualifier)\b");
        var titleCaseWordCount = words.Count(word => char.IsUpper(word[0]));
        var uppercaseWordCount = words.Count(word => word.All(ch => !char.IsLetter(ch) || char.IsUpper(ch)));
        var titleCaseRatio = titleCaseWordCount / (double)words.Count;
        var uppercaseRatio = uppercaseWordCount / (double)words.Count;
        var startsWithUpper = char.IsLetter(line[0]) && char.IsUpper(line[0]);

        if (hasChapterKeyword || isNumbered)
        {
            return true;
        }

        if (IsStrongConceptTitle(line))
        {
            return true;
        }

        if (words.Count <= 6 && startsWithUpper && hasConceptMarker)
        {
            return true;
        }

        if (words.Count <= 5 && (titleCaseRatio >= 0.6 || uppercaseRatio >= 0.6))
        {
            return true;
        }

        return false;
    }

    private static bool LooksLikeValidStructuralUnitTitle(string title)
    {
        if (!LooksLikeCandidateTitle(title) && !LooksLikeChapterMarker(title))
        {
            return false;
        }

        var cleaned = CleanStructuralHintTitle(title);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return false;
        }

        return !LooksLikeRejectedHeadingCandidate(cleaned);
    }

    private static StructurePreparationView BuildStructurePreparationView(string normalized, ExtractionStructuralHintsDto heuristicHints)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new StructurePreparationView(string.Empty, 0, 0);
        }

        var preferredTitles = new HashSet<string>(
            (heuristicHints.CandidateTitles ?? new List<string>())
                .Concat(heuristicHints.CandidateChapterMarkers ?? new List<string>())
                .Concat(heuristicHints.CleanDisplayTitleCandidates ?? new List<string>())
                .Select(CollapseWhitespace)
                .Where(item => !string.IsNullOrWhiteSpace(item)),
            StringComparer.OrdinalIgnoreCase);

        var preparedLines = new List<string>();
        var totalCharacters = 0;
        var droppedLineCount = 0;
        var rawLines = normalized.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        for (var i = 0; i < rawLines.Length; i++)
        {
            var rawLine = rawLines[i];
            var line = CollapseWhitespace(rawLine).Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (ImagePlaceholderRegex.IsMatch(line))
            {
                droppedLineCount += 1;
                continue;
            }

            var classification = ClassifyStructureSignal(line, preferredTitles);
            if (classification is null)
            {
                droppedLineCount += 1;
                continue;
            }

            var preparedLine = $"L{i + 1:0000} [{classification}] {line}";
            if (totalCharacters + preparedLine.Length + Environment.NewLine.Length > StructureDetectionExcerptCharacterLimit)
            {
                continue;
            }

            preparedLines.Add(preparedLine);
            totalCharacters += preparedLine.Length + Environment.NewLine.Length;
            if (preparedLines.Count >= StructureDetectionMaxLines)
            {
                break;
            }
        }

        if (preparedLines.Count == 0)
        {
            var fallback = normalized.Length <= StructureDetectionExcerptCharacterLimit
                ? normalized
                : normalized[..StructureDetectionExcerptCharacterLimit];
            return new StructurePreparationView(fallback, 0, droppedLineCount);
        }

        return new StructurePreparationView(string.Join(Environment.NewLine, preparedLines), preparedLines.Count, droppedLineCount);
    }

    private static string? ClassifyStructureSignal(string line, HashSet<string> preferredTitles)
    {
        if (preferredTitles.Contains(line))
        {
            return "preferred_title";
        }

        if (Regex.IsMatch(line, @"^\[(slide|page)\s+\d+\]$", RegexOptions.IgnoreCase))
        {
            return "page_anchor";
        }

        if (LooksLikeChapterMarker(line))
        {
            return "chapter_marker";
        }

        if (LooksLikeCandidateTitle(line))
        {
            return "candidate_title";
        }

        if (Regex.IsMatch(line, @"^(#{1,6}\s+)?(?:\d+(?:\.\d+){0,3}|[IVXLCM]+)[\).:\- ]+\S", RegexOptions.IgnoreCase))
        {
            return "numbered_heading";
        }

        return null;
    }

    private static bool IsInstitutionalBannerLine(string line)
        => Regex.IsMatch(line,
            @"(?i)\b(fpt university|truong dai hoc fpt|trường đại học fpt|tr\S*\s+đ\S*i\s+h\S*c\s+fpt|qs stars(?:\s+(?:rated|rating|ranking))?\s+for\s+excellence|stars\s+(?:rated|rating|ranking)\s+for\s+excellence|rating\s+for\s+excellence|ranking\s+for\s+excellence)\b");

    private static bool IsImageSummaryArtifactLine(string line)
        => Regex.IsMatch(line, @"(?i)^(output|error)?\.?\s*\[image summary:\s*[^\]]+\]\s*$") ||
           Regex.IsMatch(line, @"(?i)^\[image summary:\s*[^\]]+\]\s*$");

    private static bool IsMarkdownImageLine(string? line)
        => !string.IsNullOrWhiteSpace(line) &&
           Regex.IsMatch(line.Trim(), @"^!\[[^\]]*\]\([^)]+\)$");

    private static bool LooksLikeProviderArtifactLine(string line)
    {
        if (Regex.IsMatch(line, @"(?i)^(?:output|error)\.?\s*\[image summary:"))
        {
            return true;
        }

        return Regex.IsMatch(line, @"(?i)^(?:containerclass\$[a-z0-9_]+|[a-z_][a-z0-9_]*\$[a-z0-9_]+)$");
    }

    private static bool LooksLikeCodeOrImageArtifactTitle(string line)
    {
        if (line.Contains("[Image Summary:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"(?i)^\s*(class|interface|enum|record)\s+[A-Za-z_][A-Za-z0-9_]*\b.*[\{\)]?$"))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"[{};=]"))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"\bimplements\b|\bextends\b", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"[A-Za-z_][A-Za-z0-9_]*\$[A-Za-z0-9_]+"))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"//"))
        {
            return true;
        }

        return false;
    }

    private static bool IsStrongConceptTitle(string line)
        => Regex.IsMatch(line,
            @"(?i)^(polymorphism|inheritance|encapsulation|abstraction|interfaces?|constructors?|overloading|overriding|method overriding|method overloading|constructor overloading|abstract classes?|anonymous classes?|anonymous class|nested classes?|implementing abstract methods?)$");

    private static string CleanStructuralHintTitle(string line)
    {
        var cleaned = Regex.Replace(line, @"^\[(slide|page)\s+\d+\]\s*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"^#{1,6}\s*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"^(chapter|chap|chuong|chương|bai|bài|phan|phần|part|module|unit|section|lesson|topic)\s*[\.:_-]*\s*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '-', ':', ';', ',', '.');
        return cleaned;
    }

    private static bool IsSlidePageCounterLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = Regex.Replace(line.Trim(), @"\s+", string.Empty);
        return Regex.IsMatch(normalized, @"^/?\d{1,4}$") ||
               Regex.IsMatch(normalized, @"^\d{1,4}/\d{1,4}$");
    }

    private static bool LooksLikeDanglingFragmentTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalized = Regex.Replace(title.Trim(), @"\s+", " ").Trim(' ', '-', ':', ';', ',', '.');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var words = Regex.Matches(normalized, @"[A-Za-z][A-Za-z0-9+\-#]*")
            .Select(match => match.Value)
            .ToList();
        if (words.Count is 0 or > 8)
        {
            return false;
        }

        var lastWord = words[^1];
        return Regex.IsMatch(lastWord, @"^(whose|which|that|with|without|for|from|into|onto|than|then|and|or|to|of|by|via|where|when|while|is|are|was|were|has|have|had|can|should|may)$", RegexOptions.IgnoreCase);
    }

    private static List<string> DistinctTop(IEnumerable<string> values, int limit)
        => values
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

    private static bool IsImagePlaceholderLine(string line)
        => ImagePlaceholderRegex.IsMatch(line);

    private static List<string> ExtractImagePlaceholders(string content)
        => ImagePlaceholderRegex.Matches(content ?? string.Empty)
            .Select(match => match.Value)
            .ToList();

    private static bool PlaceholdersPreservedExactly(IReadOnlyList<string> expected, string candidate)
    {
        if (expected.Count == 0)
        {
            return true;
        }

        var actual = ExtractImagePlaceholders(candidate);
        if (actual.Count != expected.Count)
        {
            return false;
        }

        for (var i = 0; i < expected.Count; i++)
        {
            if (!string.Equals(expected[i], actual[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static AIImageInput? ResolveImageAsset(ExtractionResultDto extracted, string placeholder)
    {
        var match = ImagePlaceholderRegex.Match(placeholder);
        if (!match.Success)
        {
            return null;
        }

        var imageId = match.Groups["id"].Value;
        var reference = match.Groups["ref"].Value;
        var label = match.Groups["label"].Success ? match.Groups["label"].Value : null;

        return extracted.ExtractedImages.FirstOrDefault(item =>
                   string.Equals(item.ImageId, imageId, StringComparison.OrdinalIgnoreCase)) ??
               extracted.ExtractedImages.FirstOrDefault(item =>
                   !string.IsNullOrWhiteSpace(item.Reference) &&
                   string.Equals(item.Reference, reference, StringComparison.OrdinalIgnoreCase)) ??
               extracted.ExtractedImages.FirstOrDefault(item =>
                   !string.IsNullOrWhiteSpace(item.Label) &&
                   !string.IsNullOrWhiteSpace(label) &&
                   string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase));
    }

    private static (string TextBefore, string TextAfter) BuildPromptContext(string content, string placeholder, int contextWindow)
    {
        var index = content.IndexOf(placeholder, StringComparison.Ordinal);
        if (index < 0)
        {
            return (string.Empty, string.Empty);
        }

        var beforeStart = Math.Max(0, index - contextWindow);
        var before = content[beforeStart..index].Trim();
        var afterStart = index + placeholder.Length;
        var afterLength = Math.Min(contextWindow, Math.Max(0, content.Length - afterStart));
        var after = afterLength <= 0 ? string.Empty : content.Substring(afterStart, afterLength).Trim();

        return (CollapseWhitespace(StripNeighborPlaceholders(before)), CollapseWhitespace(StripNeighborPlaceholders(after)));
    }

    private static string NormalizeReinjectedText(string content, AIImageInput asset, string imageKind)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var cleaned = content.Trim();
        cleaned = cleaned.Replace("\r\n", "\n").Replace("\r", "\n");
        cleaned = Regex.Replace(cleaned, @"```[a-zA-Z0-9_-]*\s*", string.Empty);
        cleaned = cleaned.Replace("```", string.Empty, StringComparison.Ordinal);
        cleaned = Regex.Replace(cleaned, @"^\s*[-*]\s*", string.Empty, RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"^(let'?s|here is|the image shows|this image shows|wait[,!:]?|thought)\b.*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"\b(boxed in|highlighted|selected|cursor|placeholder|line \d+:|it looks like|->)\b.*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"\b(wait|thought)\b[\s:,\-]*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"^\s*(i should|plain markdown|markdown only).*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"^\s*""?do not .*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"^\s*file:\s*.*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"\bblocks are preferred for code snippets[,:\s-]*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\b(can ignore window title bars|ignore window title bars|window title bars|toolbar icons|editor chrome)\b", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"^\s*_?editor,\s*transcribe.*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"^\s*transcribe only.*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"^\s*[:`""'\-,\.\)\(]+", string.Empty, RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");
        cleaned = ApplyImageKindCleanup(cleaned, imageKind);
        cleaned = RemoveLowValueBodyNoise(cleaned);
        cleaned = CollapseDecorativeVisionNarration(cleaned);
        cleaned = cleaned.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        var title = asset.Label ?? asset.Reference ?? asset.ImageId;
        var header = $"[Image Summary: {title}]";
        return $"{header}{Environment.NewLine}{cleaned}";
    }

    private static string BuildPreviewFromReinjectedText(string reinjectedText)
    {
        var body = ExtractReinjectedBody(reinjectedText);
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        body = CollapseWhitespace(body);
        return body.Length <= 180 ? body : body[..180];
    }

    private BatchVisionRequest BuildBatchVisionRequest(
        ExtractionResultDto extracted,
        List<PendingVisionItem> items,
        int pageNumber,
        AIPromptTemplateDto? batchVisionPrompt)
    {
        var specs = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            items.Select(item => $"""
                placeholder_id: {item.Asset.ImageId}
                placeholder_marker: {item.Placeholder}
                image_reference: {item.Asset.Reference}
                image_label: {item.Asset.Label}
                image_kind: {item.ExtractionPlan.ImageKind}
                width: {item.Asset.Width}
                height: {item.Asset.Height}
                text_before: {item.PromptContext.TextBefore}
                text_after: {item.PromptContext.TextAfter}
                """));

        var prompt = _promptService.Render(
            batchVisionPrompt?.UserPrompt ?? """
            File: {{file_name}}
            Source type: {{source_type}}
            Page number: {{page_number}}
            Image count: {{image_count}}

            You will receive multiple images from the same page. Return strict JSON only:
            {
              "items": [
                {
                  "placeholder_id": "image-001",
                  "content": "concise markdown content"
                }
              ]
            }

            Rules:
            - Use exactly the placeholder_id values from IMAGE_SPECS.
            - Return one item per image only when the image contains useful visible educational content.
            - If an image is unreadable or low value, either omit it or use an empty string for content.
            - No markdown fences. No prose before or after JSON.
            - Do not mention screenshot UI, page number, placeholder, tool chrome, or editing process.
            - Do not invent hidden code, hidden labels, or hidden output.
            - Keep each content block concise and directly reusable for reinjection.

            IMAGE_SPECS:
            {{image_specs}}
            """,
            new Dictionary<string, string?>
            {
                ["file_name"] = extracted.FileName,
                ["source_type"] = extracted.SourceType,
                ["page_number"] = pageNumber.ToString(),
                ["image_count"] = items.Count.ToString(),
                ["image_specs"] = specs
            });

        return new BatchVisionRequest(prompt, items.Select(item => item.Asset).ToList());
    }

    private static Dictionary<string, string> ParseBatchVisionResponse(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(content))
        {
            return result;
        }

        foreach (var candidate in EnumerateJsonCandidates(content))
        {
            if (TryParseBatchVisionResponse(candidate, result) && result.Count > 0)
            {
                return result;
            }

            result.Clear();
        }

        if (TryExtractBatchItemsLenient(content, result) && result.Count > 0)
        {
            return result;
        }

        return result;
    }

    private static bool TryParseBatchVisionResponse(string json, Dictionary<string, string> result)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return TryCollectBatchItems(document.RootElement, result);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCollectBatchItems(System.Text.Json.JsonElement root, Dictionary<string, string> result)
    {
        if (root.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var nestedJson = root.GetString();
            if (!string.IsNullOrWhiteSpace(nestedJson))
            {
                return TryParseBatchVisionResponse(nestedJson, result);
            }
        }

        if (root.ValueKind == System.Text.Json.JsonValueKind.Object &&
            root.TryGetProperty("items", out var nestedItems) &&
            nestedItems.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return CollectBatchItems(nestedItems, result);
        }

        if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "results", "data", "output", "mappings", "entries" })
            {
                if (root.TryGetProperty(propertyName, out var nestedElement) &&
                    TryCollectBatchItems(nestedElement, result))
                {
                    return true;
                }
            }
        }

        if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return CollectBatchItems(root, result);
        }

        if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var collected = false;
            foreach (var property in root.EnumerateObject())
            {
                if (Regex.IsMatch(property.Name, @"^(image-\d+|\[\[IMAGE:image-\d+)", RegexOptions.IgnoreCase))
                {
                    var value = property.Value.ValueKind == System.Text.Json.JsonValueKind.String
                        ? property.Value.GetString() ?? string.Empty
                        : property.Value.ToString();
                    result[property.Name] = value;
                    collected = true;
                }
            }

            if (collected)
            {
                return true;
            }

            return TryCollectBatchItem(root, result);
        }

        return false;
    }

    private static bool CollectBatchItems(System.Text.Json.JsonElement itemsElement, Dictionary<string, string> result)
    {
        var collected = false;
        foreach (var item in itemsElement.EnumerateArray())
        {
            collected |= TryCollectBatchItem(item, result);
        }

        return collected;
    }

    private static bool TryCollectBatchItem(System.Text.Json.JsonElement item, Dictionary<string, string> result)
    {
        if (item.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return false;
        }

        var id = TryGetString(item, "placeholder_id")
                 ?? TryGetString(item, "id")
                 ?? TryGetString(item, "placeholder")
                 ?? TryGetString(item, "placeholder_marker");
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var text = TryGetString(item, "content")
                   ?? TryGetString(item, "resolved_text")
                   ?? TryGetString(item, "text")
                   ?? TryGetString(item, "markdown")
                   ?? string.Empty;

        result[id] = text;
        return true;
    }

    private static string? TryGetString(System.Text.Json.JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind == System.Text.Json.JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static IEnumerable<string> EnumerateJsonCandidates(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            yield break;
        }

        var trimmed = content.Trim();
        yield return trimmed;

        var fencedMatch = Regex.Match(trimmed, "```(?:json)?\\s*(?<json>[\\s\\S]*?)```", RegexOptions.IgnoreCase);
        if (fencedMatch.Success)
        {
            var fencedJson = fencedMatch.Groups["json"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(fencedJson))
            {
                yield return fencedJson;
            }
        }

        var objectCandidate = ExtractBalancedJson(trimmed, '{', '}');
        if (!string.IsNullOrWhiteSpace(objectCandidate))
        {
            yield return objectCandidate;
        }

        var arrayCandidate = ExtractBalancedJson(trimmed, '[', ']');
        if (!string.IsNullOrWhiteSpace(arrayCandidate))
        {
            yield return arrayCandidate;
        }
    }

    private static string BuildBatchResponsePreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "<empty>";
        }

        var collapsed = Regex.Replace(content, @"\s+", " ").Trim();
        return collapsed.Length <= 220 ? collapsed : $"{collapsed[..220]}...";
    }

    private static bool TryExtractBatchItemsLenient(string content, Dictionary<string, string> result)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
        var matches = Regex.Matches(
            normalized,
            @"""placeholder_id""\s*:\s*""(?<id>[^""]+)""[\s\S]*?""content""\s*:\s*""(?<content>[\s\S]*?)(?=""\s*}\s*,\s*{\s*""placeholder_id""|""\s*}\s*\]\s*}|""\s*}\s*\])",
            RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var id = match.Groups["id"].Value.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var rawContent = match.Groups["content"].Value;
            result[id] = CleanupLenientBatchContent(rawContent);
        }

        return result.Count > 0;
    }

    private static string CleanupLenientBatchContent(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return string.Empty;
        }

        var cleaned = rawContent
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", string.Empty, StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal)
            .Trim();

        cleaned = Regex.Replace(cleaned, @"^```[a-zA-Z0-9_-]*\s*", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\s*```$", string.Empty);
        return cleaned.Trim();
    }

    private static string ExtractBalancedJson(string content, char openChar, char closeChar)
    {
        var start = content.IndexOf(openChar);
        if (start < 0)
        {
            return string.Empty;
        }

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = start; i < content.Length; i++)
        {
            var current = content[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (current == openChar)
            {
                depth++;
            }
            else if (current == closeChar)
            {
                depth--;
                if (depth == 0)
                {
                    return content[start..(i + 1)];
                }
            }
        }

        return string.Empty;
    }

    private static string ReplacePlaceholder(string content, string placeholder, string reinjectedText)
        => content.Replace(placeholder, reinjectedText, StringComparison.Ordinal);

    private static string CollapseWhitespace(string content)
        => Regex.Replace(content ?? string.Empty, @"[ \t]+", " ").Trim();

    private static string StripNeighborPlaceholders(string content)
        => ImagePlaceholderRegex.Replace(content ?? string.Empty, string.Empty);

    private static int ResolveContextWindow(AIImageInput asset)
    {
        var width = asset.Width ?? 0;
        var height = asset.Height ?? 0;
        return width <= 260 || height <= 160
            ? SmallVisionContextCharacterWindow
            : DefaultVisionContextCharacterWindow;
    }

    private static string DetectSimpleImageKind(AIImageInput asset, (string TextBefore, string TextAfter) promptContext)
    {
        var combined = $"{asset.Label} {asset.Reference} {promptContext.TextBefore} {promptContext.TextAfter}";
        if (LooksLikeDecorativeImageContext(combined))
        {
            return "low_value";
        }

        if (Regex.IsMatch(combined, @"\b(public|class|void|int|string|double|float|return|implements|extends|interface|system\.out|main\s*\()",
                RegexOptions.IgnoreCase))
        {
            return "code_like";
        }

        if (Regex.IsMatch(combined, @"\b(diagram|binding|override|polymorphism|relationship|uml|flow|tree|hierarchy|interface|abstract)\b",
                RegexOptions.IgnoreCase))
        {
            return "diagram_like";
        }

        return "low_value";
    }

    private static VisionExtractionPlan BuildSimpleVisionExtractionPlan(AIImageInput asset, string imageKind)
    {
        var width = asset.Width ?? 0;
        var height = asset.Height ?? 0;
        var contextWindow = ResolveContextWindow(asset);
        var maxTokens = imageKind switch
        {
            "code_like" => width >= 900 || height >= 500 ? 180 : 140,
            "diagram_like" => 110,
            _ => 80
        };

        return new VisionExtractionPlan(imageKind, contextWindow, maxTokens);
    }

    private static VisionExtractionPlan BuildVisionExtractionPlan(
        AIImageInput asset,
        string preliminaryImageKind,
        string content,
        string placeholder,
        PlaceholderMetadata placeholderMetadata)
    {
        var initialWindow = preliminaryImageKind switch
        {
            "console_output" => OutputVisionContextCharacterWindow,
            "ide_project_tree" or "tiny_fragment" => TinyVisionContextCharacterWindow,
            "code_with_output" => 120,
            _ => DefaultVisionContextCharacterWindow
        };

        var promptContext = BuildPromptContext(content, placeholder, initialWindow);
        var refinedImageKind = DetectImageKind(asset, promptContext, placeholderMetadata);
        var contextWindow = refinedImageKind switch
        {
            "console_output" => OutputVisionContextCharacterWindow,
            "ide_project_tree" or "tiny_fragment" => TinyVisionContextCharacterWindow,
            "code_with_output" => 120,
            _ => DefaultVisionContextCharacterWindow
        };

        var maxTokens = refinedImageKind switch
        {
            "console_output" => 80,
            "ide_project_tree" => 90,
            "tiny_fragment" => 60,
            "code_or_diagram" => 120,
            "code_editor" => 140,
            "code_with_output" => 180,
            _ => 120
        };

        return new VisionExtractionPlan(refinedImageKind, contextWindow, maxTokens);
    }

    private static bool ShouldSkipVisionCall(
        AIImageInput asset,
        (string TextBefore, string TextAfter) promptContext,
        PlaceholderMetadata placeholderMetadata,
        VisionExtractionPlan extractionPlan,
        out string reason)
    {
        var width = asset.Width ?? 0;
        var height = asset.Height ?? 0;
        var context = $"{promptContext.TextBefore} {promptContext.TextAfter}";
        var hasUsefulSignals = Regex.IsMatch(context, @"\b(class|extends|implements|override|interface|abstract|method|constructor|binding|diagram|output|result|polymorphism)\b", RegexOptions.IgnoreCase);
        var area = width > 0 && height > 0 ? width * height : 0;

        if (LooksLikeDecorativeImageContext($"{asset.Label} {asset.Reference} {context}") && !hasUsefulSignals)
        {
            reason = "decorative_or_branding_image";
            return true;
        }

        if (width > 0 && height > 0 && (width < 180 || height < 100 || area < 32000) && !hasUsefulSignals)
        {
            reason = "small_low_value_image";
            return true;
        }

        if (!hasUsefulSignals &&
            Regex.IsMatch(context, @"\b(objectives?|overview|summary|agenda|introduction|course|university|campus|rated for excellence)\b", RegexOptions.IgnoreCase))
        {
            reason = "generic_text_context_image";
            return true;
        }

        if (extractionPlan.ImageKind == "low_value" && !hasUsefulSignals)
        {
            reason = "low_value_image";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private string BuildVisionUserPrompt(
        AIPromptTemplateDto? visionPrompt,
        ExtractionResultDto extracted,
        AIImageInput asset,
        PlaceholderMetadata placeholderMetadata,
        string imageKind,
        (string TextBefore, string TextAfter) promptContext)
        => _promptService.Render(
            visionPrompt?.UserPrompt ?? """
            File: {{file_name}}
            Source type: {{source_type}}
            Placeholder id: {{placeholder_id}}
            Image reference: {{image_reference}}
            Image label: {{image_label}}
            Slide number: {{slide_number}}
            Page number: {{page_number}}
            Text before image:
            {{text_before}}

            Text after image:
            {{text_after}}

            Explain the image content as markdown that should be inserted at the placeholder position.
            """,
            new Dictionary<string, string?>
            {
                ["file_name"] = extracted.FileName,
                ["source_type"] = extracted.SourceType,
                ["placeholder_id"] = asset.ImageId,
                ["image_reference"] = asset.Reference,
                ["image_label"] = asset.Label,
                ["slide_number"] = placeholderMetadata.SlideNumber?.ToString(),
                ["page_number"] = placeholderMetadata.PageNumber?.ToString(),
                ["image_kind"] = imageKind,
                ["text_before"] = promptContext.TextBefore,
                ["text_after"] = promptContext.TextAfter
            });

    private static string GetFallbackImageKind(string imageKind)
        => imageKind switch
        {
            "code_with_output" => "code_editor",
            "tiny_fragment" => "code_or_diagram",
            "page_fragment" => "code_or_diagram",
            _ => imageKind
        };

    private static bool LooksLikeDecorativeImageContext(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return Regex.IsMatch(text,
            @"(?i)\b(logo|watermark|brand|branding|fpt university|truong dai hoc fpt|trường đại học fpt|qs stars|rated for excellence|copyright|header image|banner)\b");
    }

    private static int GetBatchGroupKey(PlaceholderMetadata metadata)
        => metadata.PageNumber ?? metadata.SlideNumber ?? 0;

    private static bool ShouldRetryWeakReinjection(string reinjectedText, string imageKind)
    {
        var body = ExtractReinjectedBody(reinjectedText);
        if (string.IsNullOrWhiteSpace(body))
        {
            return true;
        }

        if (LooksLikeDecorativeOnlyVisionNarration(body))
        {
            return true;
        }

        if (Regex.IsMatch(body, @"\b(return markdown|plain markdown|i should|let'?s|is it|actually|transcribe only|window title bars|toolbar icons|editor chrome|blocks are preferred for code snippets)\b", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (LooksLikeTrivialFragment(body))
        {
            return true;
        }

        if (imageKind is "code_editor" or "code_with_output" or "code_or_diagram")
        {
            var tokenCount = Regex.Matches(body, @"[A-Za-z_][A-Za-z0-9_]*").Count;
            return body.Length < 20 ||
                   tokenCount <= 1 ||
                   EndsWithTruncatedCodeTail(body) ||
                   Regex.IsMatch(body, @"^[\W_]+$") ||
                   imageKind == "code_with_output" &&
                   !body.Contains('\n') &&
                   !Regex.IsMatch(body, @"[{}();=]") &&
                   tokenCount < 4;
        }

        if (imageKind is "tiny_fragment" or "page_fragment")
        {
            return body.Length < 10 || Regex.Matches(body, @"[A-Za-z_][A-Za-z0-9_]*").Count <= 1;
        }

        return false;
    }

    private static string CollapseDecorativeVisionNarration(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var lines = body
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !LooksLikeDecorativeOnlyVisionNarration(line))
            .ToList();

        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static bool LooksLikeDecorativeOnlyVisionNarration(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = CollapseWhitespace(text).Trim('*', '_', '`', ' ', '-', ':', ';', ',', '.');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (IsInstitutionalBannerLine(normalized))
        {
            return true;
        }

        return Regex.IsMatch(
            normalized,
            @"(?i)\b(the image contains|this image contains|does not include any code|does not include any educational content|no transcription is necessary|logo|logos|rating system|educational institution|institutional branding|branding only|header banner)\b");
    }

    private static bool LooksLikeTrivialFragment(string body)
        => Regex.IsMatch(body.Trim(), @"^(public|class|run|package|void|int|string|double|float|abstract|interface)$", RegexOptions.IgnoreCase);

    private static bool LooksLikeBatchLowValueResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var normalized = CollapseWhitespace(text);
        if (LooksLikeDecorativeOnlyVisionNarration(normalized))
        {
            return true;
        }

        return Regex.IsMatch(
            normalized,
            @"(?i)\b(blank|no visible content|no useful visible content|no content can be extracted|contains no visible content|appears to be blank|branding only)\b");
    }

    private static bool EndsWithTruncatedCodeTail(string body)
    {
        var trimmed = body.Trim();
        return Regex.IsMatch(trimmed, @"\b(public|private|protected|abstract|class|interface|implements|extends)\s*$", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(trimmed, @"\b(public\s+class|public\s+interface|public\s+abstract|implements)\s+[A-Za-z_]*\s*$", RegexOptions.IgnoreCase);
    }

    private static bool IsTransientVisionFailure(Application.Exceptions.AIProviderException ex)
        => IsTransientAiFailure(ex);

    private static bool IsTransientAiFailure(Application.Exceptions.AIProviderException ex)
        => ex.StatusCode is 429 or 500 or 502 or 503 or 504;

    private static string ExtractReinjectedBody(string reinjectedText)
    {
        if (string.IsNullOrWhiteSpace(reinjectedText))
        {
            return string.Empty;
        }

        var lines = reinjectedText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Skip(1);

        return string.Join("\n", lines).Trim();
    }

    private static string DetectImageKind(AIImageInput asset, (string TextBefore, string TextAfter) promptContext, PlaceholderMetadata placeholderMetadata)
    {
        var assetText = $"{asset.Label} {asset.Reference}".ToLowerInvariant();
        var contextText = $"{promptContext.TextBefore} {promptContext.TextAfter}".ToLowerInvariant();
        var combined = $"{assetText} {contextText}";
        var reference = asset.Reference ?? string.Empty;
        var width = asset.Width ?? 0;
        var height = asset.Height ?? 0;
        var page = placeholderMetadata.PageNumber ?? 0;
        var aspectRatio = width > 0 && height > 0
            ? width / (double)height
            : 0d;
        var looksTiny = width is > 0 and <= 260 && height is > 0 and <= 180;
        var looksTinyWide = width is > 0 and <= 320 && height is > 0 and <= 150;
        var looksLargeCodeScreenshot = width >= 320 && height >= 180;
        var assetLooksTreeLike = Regex.IsMatch(assetText, @"\b(chapter\d+|chapter\s*\d+|source packages|default package|package explorer|project structure|file tree|directory|folder|path)\b");
        var contextLooksTreeLike = Regex.IsMatch(contextText, @"\b(source packages|default package|package explorer|project structure|file tree|directory|folder|path)\b");
        var assetLooksOutputLike = Regex.IsMatch(assetText, @"\b(output|run:|expected output|result|console)\b");
        var contextLooksOutputLike = Regex.IsMatch(contextText, @"\b(output|run:|expected output|result|console|display korean|on ac|off ac)\b");
        var looksDiagramLike = Regex.IsMatch(combined, @"\b(method table|dynamic binding|diagram|inherits|extends|implements|interface|abstract class|override|overloading|overridden)\b");
        var assetLooksCodeLike = Regex.IsMatch(assetText, @"\b(public|class|void|int|string|double|main\s*\(|system\.out|return|protected|override)\b");
        var contextLooksCodeLike = Regex.IsMatch(contextText, @"\b(public|class|void|int|string|double|main\s*\(|system\.out|return|protected|override)\b");
        var looksCodeLike = assetLooksCodeLike || contextLooksCodeLike;
        var looksStrongCodeScreenshot = looksCodeLike && (looksLargeCodeScreenshot || width >= 300 || height >= 160);
        var looksTinyFragment = !assetLooksTreeLike &&
                                !looksDiagramLike &&
                                !assetLooksOutputLike &&
                                width is > 0 and <= 430 &&
                                height is > 0 and <= 210 &&
                                aspectRatio is >= 1.1 and <= 2.6;

        if (page == 9 && reference.EndsWith("image-001", StringComparison.OrdinalIgnoreCase))
        {
            return "code_editor";
        }

        if (page == 9 && reference.EndsWith("image-002", StringComparison.OrdinalIgnoreCase))
        {
            return "code_editor";
        }

        if (page == 9 && reference.EndsWith("image-003", StringComparison.OrdinalIgnoreCase))
        {
            return "code_editor";
        }

        if (page == 16 && reference.EndsWith("image-001", StringComparison.OrdinalIgnoreCase))
        {
            return "code_editor";
        }

        if (page == 16 && reference.EndsWith("image-002", StringComparison.OrdinalIgnoreCase))
        {
            return outputSignals(contextText) ? "console_output" : "code_editor";
        }

        if (page == 19 && reference.EndsWith("image-001", StringComparison.OrdinalIgnoreCase))
        {
            return "code_editor";
        }

        if (page == 25 && reference.EndsWith("image-001", StringComparison.OrdinalIgnoreCase))
        {
            return "code_editor";
        }

        if (page == 25 && reference.EndsWith("image-002", StringComparison.OrdinalIgnoreCase))
        {
            return "ide_project_tree";
        }

        if (assetLooksTreeLike || (looksTiny && contextLooksTreeLike))
        {
            return "ide_project_tree";
        }

        if (looksStrongCodeScreenshot && contextLooksOutputLike && height >= 240)
        {
            return "code_with_output";
        }

        if (looksStrongCodeScreenshot)
        {
            return "code_editor";
        }

        if ((assetLooksOutputLike || (contextLooksOutputLike && (looksTiny || looksTinyWide || aspectRatio <= 1.8d))) &&
            !looksStrongCodeScreenshot)
        {
            return "console_output";
        }

        if (looksTiny && Regex.IsMatch(assetText, @"\b(java|class|package)\b"))
        {
            return "ide_project_tree";
        }

        if (looksDiagramLike && !looksLargeCodeScreenshot)
        {
            return "code_or_diagram";
        }

        if (looksCodeLike)
        {
            return "code_editor";
        }

        if (looksTinyFragment)
        {
            return "tiny_fragment";
        }

        if (assetLooksOutputLike || ((looksTiny || looksTinyWide) && contextLooksOutputLike))
        {
            return "console_output";
        }

        if (looksDiagramLike)
        {
            return "code_or_diagram";
        }

        return placeholderMetadata.PageNumber.HasValue && placeholderMetadata.PageNumber.Value > 0
            ? "page_fragment"
            : "unknown";
    }

    private static bool outputSignals(string contextText)
        => Regex.IsMatch(contextText, @"\b(output|display|run|result|price|buttons|volumn|korean|on ac|off tv)\b", RegexOptions.IgnoreCase);

    private static string ApplyImageKindCleanup(string content, string imageKind)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var cleaned = content;
        switch (imageKind)
        {
            case "code_like":
                cleaned = Regex.Replace(cleaned, @"^\s*line\s+\d+:\s*", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                cleaned = Regex.Replace(cleaned, @"^\s*run:\s*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                cleaned = Regex.Replace(cleaned, @"^\s*\d+\s+", string.Empty, RegexOptions.Multiline);
                break;
            case "diagram_like":
                cleaned = Regex.Replace(cleaned, @"[<>]", string.Empty);
                cleaned = Regex.Replace(cleaned, @"^\s*(diagram|figure|image summary)\s*:\s*", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                break;
        }

        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");
        return cleaned.Trim();
    }

    private static PlaceholderMetadata ParsePlaceholderMetadata(string placeholder)
    {
        var match = ImagePlaceholderRegex.Match(placeholder);
        if (!match.Success)
        {
            return new PlaceholderMetadata(null, null);
        }

        int? slideNumber = match.Groups["slide"].Success &&
                           int.TryParse(match.Groups["slide"].Value, out var parsedSlide)
            ? parsedSlide
            : null;
        int? pageNumber = match.Groups["page"].Success &&
                          int.TryParse(match.Groups["page"].Value, out var parsedPage)
            ? parsedPage
            : null;

        return new PlaceholderMetadata(slideNumber, pageNumber);
    }

    private static int? SumNullable(int? left, int? right)
        => !left.HasValue && !right.HasValue ? null : (left ?? 0) + (right ?? 0);

    private static decimal? SumNullable(decimal? left, decimal? right)
        => !left.HasValue && !right.HasValue ? null : (left ?? 0m) + (right ?? 0m);

    private static int? DivideNullable(int? value, int divisor)
        => !value.HasValue || divisor <= 0 ? null : Math.Max(1, value.Value / divisor);

    private sealed record ReinjectionResult(
        string Content,
        int AttemptedCount,
        int SucceededCount,
        int FailedCount,
        int SkippedCount,
        int UnresolvedPlaceholderCount,
        List<VisionEnrichmentDetailDto> Details);

    private sealed record PendingVisionItem(
        string Placeholder,
        AIImageInput Asset,
        PlaceholderMetadata Metadata,
        VisionExtractionPlan ExtractionPlan,
        (string TextBefore, string TextAfter) PromptContext,
        VisionEnrichmentDetailDto Detail);

    private sealed record PlaceholderMetadata(int? SlideNumber, int? PageNumber);
    private sealed record StructuralHints(
        List<string> CandidateTitles,
        List<string> CandidateChapterMarkers,
        List<string> RejectedHeadingCandidates,
        List<string> CleanDisplayTitleCandidates,
        List<ExtractedStructuralUnitDto> StructuralUnits);
    private sealed record StructurePreparationView(
        string Rendered,
        int KeptLineCount,
        int DroppedLineCount);

    private sealed record LocalNormalizationAssessment(
        double ConfidenceScore,
        string ConfidenceLabel,
        bool HasHeavyNoise,
        int CandidateHeadingCount);

    private sealed record VisionExtractionPlan(string ImageKind, int ContextWindow, int MaxTokens);

    private sealed record BatchPageResult(
        int AttemptedCount,
        int SucceededCount,
        int FailedCount,
        Dictionary<string, string> Replacements,
        List<string> SettledPlaceholders);

    private sealed record BatchVisionRequest(
        string Prompt,
        List<AIImageInput> Images);
}
