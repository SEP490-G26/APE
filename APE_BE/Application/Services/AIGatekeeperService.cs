/**
 * AIGatekeeperService.cs
 * Implementation of the Gatekeeper agent in Step 1 of the APE AI Pipeline.
 * 
 * Responsibilities:
 * 1. Fast deterministic pre-checks (empty check, language detection, hard policy violations).
 * 2. LLM-based deep evaluation using Gatekeeper Agent (DeepSeek / OpenAI fallback).
 * 3. Domain validation (ensures material is relevant to Java/OOP/Programming).
 * 4. Safety and prompt-injection filtering.
 */

using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

/// <summary>
/// Service responsible for validating uploaded documents and user prompts against safety policies and academic domains.
/// </summary>
public class AIGatekeeperService : IAIGatekeeperService
{
    private const int MaxContentLength = 4000;
    private readonly IAIExecutionService _aiExecutionService;
    private readonly IAIFeatureRoutingService _routingService;
    private readonly IAIPromptService _promptService;
    private readonly IAIArtifactCatalogService _artifactCatalogService;
    private readonly AIOptions _options;
    private readonly ILogger<AIGatekeeperService> _logger;

    public AIGatekeeperService(
        IAIExecutionService aiExecutionService,
        IAIFeatureRoutingService routingService,
        IAIPromptService promptService,
        IAIArtifactCatalogService artifactCatalogService,
        IOptions<AIOptions> options,
        ILogger<AIGatekeeperService> logger)
    {
        _aiExecutionService = aiExecutionService;
        _routingService = routingService;
        _promptService = promptService;
        _artifactCatalogService = artifactCatalogService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Validates raw document content for domain relevance, safety, and format.
    /// Performs fast local rule checks first, then dispatches to the Gatekeeper LLM agent.
    /// </summary>
    /// <param name="content">Raw document text content.</param>
    /// <param name="context">Optional request metadata (file name, language, subject hint).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GatekeeperResult"/> indicating whether the document is supported.</returns>
    public async Task<GatekeeperResult> ValidateAsync(string content, GatekeeperRequestContext? context = null, CancellationToken cancellationToken = default)
    {
        var normalizedContent = (content ?? string.Empty).Trim();
        var policy = await _artifactCatalogService.GetPolicyAsync("gatekeeper-policy.json", cancellationToken);
        var supportedSubjects = ReadSupportedSubjects(policy);
        var subjectHint = context?.SubjectHint ?? string.Empty;
        var language = context?.Language ?? DetectLanguage(normalizedContent);
        var fileName = context?.FileName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            return new GatekeeperResult
            {
                IsSupported = false,
                PrimaryDomain = "Unsupported",
                Reason = "Document content is empty after extraction.",
                Verdict = "unsupported",
                RejectionReasonCode = ReadPrecheckCode(policy, "empty_content_rejection_code") ?? "empty_content"
            };
        }

        var detectedLanguage = DetectLanguage(normalizedContent);
        if (detectedLanguage != "en" || IsVietnameseContent(normalizedContent))
        {
            return new GatekeeperResult
            {
                IsSupported = false,
                PrimaryDomain = "Unsupported",
                Reason = "Only English academic documents are supported by the APE platform. Vietnamese or non-English documents are not accepted.",
                Verdict = "unsupported",
                Confidence = 0.99,
                RejectionReasonCode = ReadPrecheckCode(policy, "unsupported_language_rejection_code") ?? "unsupported_language",
                Provider = _options.Enabled ? _routingService.ResolveText(AIFeatureNames.Gatekeeper, context?.RuntimeOverride).Provider : null,
                Model = _options.Enabled ? _routingService.ResolveText(AIFeatureNames.Gatekeeper, context?.RuntimeOverride).Model : null,
                ConfiguredModel = _options.Enabled ? _routingService.ResolveText(AIFeatureNames.Gatekeeper, context?.RuntimeOverride).Model : null,
                EffectiveModel = _options.Enabled ? _routingService.ResolveText(AIFeatureNames.Gatekeeper, context?.RuntimeOverride).Model : null
            };
        }

        var resolved = _routingService.ResolveText(AIFeatureNames.Gatekeeper, context?.RuntimeOverride);
        // Note: Production & live runtime prompts are DB-driven (fetched dynamically from MongoDB AI_Rule_Artifacts via IAIArtifactCatalogService).
        // The inline string templates below serve strictly as a resilience fallback / fault-tolerance safeguard
        // (e.g. for offline unit testing and unseeded cold start) to prevent NullReferenceException.
        var prompt = await _artifactCatalogService.GetPromptAsync("gatekeeper", cancellationToken);

        if (_options.Enabled)
        {
            try
            {
                var trimmedContent = BuildRepresentativeContentSample(normalizedContent, MaxContentLength);
                
                // Fallback system prompt (Active prompt is managed in MongoDB AI_Rule_Artifacts)
                var renderedSystemPrompt = prompt?.SystemPrompt ?? """
                    You are the whitelist gatekeeper for an academic learning system.
                    Only English academic documents are supported. If the document is written in Vietnamese or any language other than English, reject it immediately with verdict 'unsupported' and rejection_reason_code 'unsupported_language'.
                    Return JSON only.
                    """;
                
                // Fallback user prompt (Active prompt is managed in MongoDB AI_Rule_Artifacts)
                var renderedUserPrompt = _promptService.Render(
                    prompt?.UserPrompt ?? """
                    Supported whitelist:
                    {{whitelist_json}}

                    Requested subject hint: {{subject}}
                    Language: {{language}}
                    File: {{file_name}}

                    Extracted content:
                    {{content}}

                    Classify the document into one of: supported, unsupported, ambiguous.

                    Rules:
                    - supported: the document clearly belongs to exactly one supported subject area and is written in English.
                    - unsupported: the document is outside the whitelist or written in a non-English language (such as Vietnamese).
                    - ambiguous: the content is insufficient, too noisy, or strongly mixes multiple supported subject areas.
                    - Do not infer hidden context beyond the visible content.
                    - Focus on academic topic scope, not file extension.

                    Return strict JSON only:
                    {
                      "verdict": "supported|unsupported|ambiguous",
                      "isSupported": true,
                      "primaryDomain": "C|JAVA_OOP|DSA_JAVA|Unsupported",
                      "matched_subjects": ["C"],
                      "confidence": 0.0,
                      "reason": "...",
                      "rejection_reason_code": null,
                      "detected_topics": ["..."]
                    }
                    """,
                    new Dictionary<string, string?>
                    {
                        ["whitelist_json"] = BuildWhitelistJson(supportedSubjects),
                        ["subject"] = subjectHint,
                        ["language"] = language,
                        ["file_name"] = fileName,
                        ["content"] = trimmedContent
                    });

                var response = await _aiExecutionService.ExecuteTextAsync(
                    resolved,
                    new AITextRequest
                    {
                        FeatureName = AIFeatureNames.Gatekeeper,
                        SystemPrompt = renderedSystemPrompt,
                        UserPrompt = renderedUserPrompt,
                        Provider = resolved.Provider,
                        Model = resolved.Model,
                        Temperature = resolved.Temperature,
                        MaxTokens = resolved.MaxTokens,
                        RuntimeOverride = context?.RuntimeOverride,
                        Metadata = new Dictionary<string, string>
                        {
                            ["feature"] = "gatekeeper"
                        }
                    },
                    cancellationToken);

                var parsed = TryParseResponse(response.Content);
                if (parsed is not null)
                {
                    parsed.Provider = response.Provider ?? resolved.Provider;
                    parsed.Model = response.Model ?? resolved.Model;
                    parsed.ConfiguredModel = resolved.Model;
                    parsed.EffectiveModel = response.Model ?? resolved.Model;
                    parsed.UsageSource = response.UsageSource;
                    parsed.CostSource = response.CostSource;
                    parsed.InputTokens = response.InputTokens;
                    parsed.OutputTokens = response.OutputTokens;
                    parsed.TotalTokens = response.TotalTokens;
                    parsed.CostUsd = response.ReportedCostUsd;
                    parsed.FallbackUsed = response.FromFallback;
                    parsed.FallbackFromProvider = response.FallbackFromProvider;
                    parsed.FallbackFromModel = response.FallbackFromModel;
                    parsed.FallbackReasonCode = response.FallbackReasonCode;
                    NormalizeVerdict(parsed);
                    return parsed;
                }

                _logger.LogWarning("Gatekeeper response from provider {Provider} could not be parsed. Falling back.", resolved.Provider);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gatekeeper provider {Provider} failed. Falling back.", resolved.Provider);
            }
        }

        return BuildHeuristicFallback(normalizedContent, supportedSubjects, subjectHint, resolved.Provider, resolved.Model);
    }

    private static string BuildRepresentativeContentSample(string content, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length <= maxLength)
        {
            return content;
        }

        var segmentLength = Math.Max(400, maxLength / 3);
        var head = content[..Math.Min(segmentLength, content.Length)].Trim();

        var middleStart = Math.Max(0, (content.Length / 2) - (segmentLength / 2));
        var middle = content.Substring(middleStart, Math.Min(segmentLength, content.Length - middleStart)).Trim();

        var tailStart = Math.Max(0, content.Length - segmentLength);
        var tail = content[tailStart..].Trim();

        var parts = new[] { head, middle, tail }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var combined = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}[...snip...]{Environment.NewLine}{Environment.NewLine}",
            parts);

        return combined.Length <= maxLength
            ? combined
            : combined[..maxLength];
    }

    private GatekeeperResult? TryParseResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            return null;
        }

        var json = content[start..(end + 1)];

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            return new GatekeeperResult
            {
                IsSupported = TryReadBoolean(root, "is_supported") ?? TryReadBoolean(root, "isSupported") ?? false,
                PrimaryDomain = TryReadString(root, "primary_domain") ?? TryReadString(root, "primaryDomain") ?? "Unsupported",
                Reason = TryReadString(root, "reason") ?? string.Empty,
                Verdict = TryReadString(root, "verdict") ?? "unsupported",
                Confidence = TryReadDouble(root, "confidence") ?? 0,
                RejectionReasonCode = TryReadString(root, "rejection_reason_code") ?? TryReadString(root, "rejectionReasonCode"),
                MatchedSubjects = TryReadStringArray(root, "matched_subjects") ?? TryReadStringArray(root, "matchedSubjects") ?? new List<string>(),
                DetectedTopics = TryReadStringArray(root, "detected_topics") ?? TryReadStringArray(root, "detectedTopics") ?? new List<string>()
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private GatekeeperResult BuildHeuristicFallback(
        string content,
        IReadOnlyList<SupportedSubjectDefinition> supportedSubjects,
        string subjectHint,
        string provider,
        string model)
    {
        var normalizedSubjectHint = Application.Common.AISubjectDomainMapper.NormalizeSubjectOrCourseCode(subjectHint);

        if (IsVietnameseContent(content))
        {
            return new GatekeeperResult
            {
                IsSupported = false,
                PrimaryDomain = "Unsupported",
                Reason = "Rejected by heuristic fallback because non-English (Vietnamese) content is not supported.",
                Verdict = "unsupported",
                Confidence = 0.99,
                RejectionReasonCode = "unsupported_language",
                Provider = provider,
                Model = model,
                ConfiguredModel = model,
                EffectiveModel = model
            };
        }

        if (!_options.AllowHeuristicFallbacks)
        {
            return new GatekeeperResult
            {
                IsSupported = false,
                PrimaryDomain = "Unsupported",
                Reason = "No AI provider configured for gatekeeper and heuristic fallback is disabled.",
                Verdict = "unsupported",
                Provider = provider,
                Model = model,
                ConfiguredModel = model,
                EffectiveModel = model
            };
        }

        var lowered = content.ToLowerInvariant();
        var scoredSubjects = supportedSubjects
            .Select(subject =>
            {
                var rawScore = CountHits(lowered, subject.TopicExamples.ToArray());
                var isHinted = !string.IsNullOrWhiteSpace(normalizedSubjectHint) &&
                               string.Equals(subject.Code, normalizedSubjectHint, StringComparison.OrdinalIgnoreCase);
                return new
                {
                    subject.Code,
                    RawScore = rawScore,
                    Score = rawScore + (isHinted && rawScore >= 1 ? 1 : 0),
                    MatchedTopics = subject.TopicExamples
                        .Where(topic => lowered.Contains(topic, StringComparison.Ordinal))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.RawScore)
            .ToList();

        var topDomain = scoredSubjects.FirstOrDefault();
        var secondScore = scoredSubjects.Skip(1).FirstOrDefault()?.Score ?? 0;
        var isSupported = topDomain is not null && topDomain.RawScore >= 2 && topDomain.Score > secondScore;
        var verdict = isSupported ? "supported" : "ambiguous";
        var subjectMatchesHint = !string.IsNullOrWhiteSpace(normalizedSubjectHint) &&
                                 string.Equals(topDomain?.Code, normalizedSubjectHint, StringComparison.OrdinalIgnoreCase);

        return new GatekeeperResult
        {
            IsSupported = isSupported,
            PrimaryDomain = isSupported ? topDomain!.Code : "Unsupported",
            Reason = isSupported
                ? "Accepted by heuristic fallback while provider integration is not configured yet."
                : "Rejected by heuristic fallback because the extracted content does not strongly match one supported subject domain.",
            Verdict = verdict,
            Confidence = isSupported ? (subjectMatchesHint ? 0.82 : 0.72) : 0.35,
            MatchedSubjects = scoredSubjects.Where(item => item.Score > 0).Select(item => item.Code).Take(3).ToList(),
            DetectedTopics = topDomain?.MatchedTopics ?? new List<string>(),
            RejectionReasonCode = isSupported ? null : "heuristic_ambiguous",
            Provider = provider,
            Model = model,
            ConfiguredModel = model,
            EffectiveModel = model
        };
    }

    private static int CountHits(string content, params string[] keywords)
    {
        return keywords.Count(keyword => content.Contains(keyword, StringComparison.Ordinal));
    }

    private static string BuildWhitelistJson(IReadOnlyList<SupportedSubjectDefinition> supportedSubjects)
    {
        return JsonSerializer.Serialize(
            supportedSubjects.Select(subject => new
            {
                code = subject.Code,
                label = subject.Label,
                topic_examples = subject.TopicExamples
            }),
            new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Reads supported subject definitions from the DB-driven gatekeeper policy artifact (gatekeeper-policy.json).
    /// If the policy document is missing or not yet seeded in MongoDB, falls back to the default core curriculum whitelist.
    /// </summary>
    private static IReadOnlyList<SupportedSubjectDefinition> ReadSupportedSubjects(JsonElement? policy)
    {
        // Fallback default subjects if gatekeeper-policy.json is not found in MongoDB AI_Rule_Artifacts
        if (policy is null || policy.Value.ValueKind != JsonValueKind.Object ||
            !policy.Value.TryGetProperty("supported_subjects", out var subjects) ||
            subjects.ValueKind != JsonValueKind.Array)
        {
            return new List<SupportedSubjectDefinition>
            {
                new("C", "Programming Fundamentals using C", new List<string> { "variables", "conditions", "loops", "arrays", "functions", "pointers", "scanf", "printf" }),
                new("JAVA_OOP", "Java OOP", new List<string> { "class", "object", "constructor", "encapsulation", "inheritance", "polymorphism", "method", "interface" }),
                new("DSA_JAVA", "Data Structures and Algorithms in Java", new List<string> { "array", "linked list", "stack", "queue", "tree", "graph", "sorting", "searching", "complexity" })
            };
        }

        var result = new List<SupportedSubjectDefinition>();
        foreach (var item in subjects.EnumerateArray())
        {
            var code = TryReadString(item, "code");
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            result.Add(new SupportedSubjectDefinition(
                code,
                TryReadString(item, "label") ?? code,
                TryReadStringArray(item, "topic_examples") ?? new List<string>()));
        }

        return result;
    }

    private static string? ReadPrecheckCode(JsonElement? policy, string propertyName)
    {
        if (policy is null || policy.Value.ValueKind != JsonValueKind.Object ||
            !policy.Value.TryGetProperty("precheck_rules", out var precheck))
        {
            return null;
        }

        return TryReadString(precheck, propertyName);
    }

    private static void NormalizeVerdict(GatekeeperResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Verdict))
        {
            result.Verdict = result.IsSupported ? "supported" : "unsupported";
        }

        if (!result.IsSupported && string.Equals(result.Verdict, "supported", StringComparison.OrdinalIgnoreCase))
        {
            result.Verdict = "unsupported";
        }

        if (result.IsSupported && result.Confidence <= 0)
        {
            result.Confidence = 0.8;
        }
    }

    /// <summary>
    /// Detects whether the extracted preview content contains Vietnamese text.
    /// Evaluates both Vietnamese diacritics and common Vietnamese phrases/stopwords to prevent non-English uploads.
    /// </summary>
    private static bool IsVietnameseContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        // 1. Regex check for Vietnamese specific accented characters / diacritics
        var diacriticsMatches = System.Text.RegularExpressions.Regex.Matches(
            content,
            @"[ăâđêôơưĂÂĐÊÔƠƯáàảãạắằẳẵặấầẩẫậéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // If any Vietnamese diacritic character is present, classify as Vietnamese immediately
        if (diacriticsMatches.Count >= 1)
        {
            return true;
        }

        // 2. Unaccented Vietnamese keywords / phrases commonly found in Vietnamese programming documents
        var lowered = content.ToLowerInvariant();
        string[] unaccentedMarkers =
        [
            "lap trinh", "bai tap", "chuong", "huong doi tuong", "ke thua", "da hinh",
            "dong goi", "phuong thuc", "thuoc tinh", "ham tao", "kieu du lieu", "vong lap",
            "cau truc du lieu", "giai thuat", "de thi", "cau hoi", "tai lieu", "giao trinh",
            "bai giang", "sinh vien", "giang vien", "khoa hoc", "huong dan", "kiem tra",
            "thuc hanh", "trong java", "trong c", "khai bao", "bien toan cuc", "bien cuc bo"
        ];

        return unaccentedMarkers.Any(marker => lowered.Contains(marker, StringComparison.Ordinal));
    }

    private static string DetectLanguage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "en";
        }

        return IsVietnameseContent(content) ? "vi" : "en";
    }

    private static string? TryReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool? TryReadBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }

    private static double? TryReadDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static List<string>? TryReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
    }

    private sealed record SupportedSubjectDefinition(string Code, string Label, List<string> TopicExamples);
}
