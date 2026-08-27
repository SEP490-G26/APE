using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Application.Options.AI;
using Ape.AiModule.Domain.Entities.AI;
using Microsoft.Extensions.Options;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class RealAiProviderGateway : IAiProviderGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<AiProviderOptions> _optionsMonitor;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly IGatekeeperPolicyService _gatekeeperPolicyService;
    private readonly IEmbeddingTaggingPolicyService _embeddingTaggingPolicyService;
    private readonly ICodeMentorPolicyService _codeMentorPolicyService;
    private readonly IRubricCatalogService _rubricCatalogService;
    private readonly DemoAiProviderGateway _fallbackGateway = new();
    private static readonly AsyncLocal<Dictionary<string, TokenCostBreakdown>?> UsageSnapshots = new();
    private static readonly AsyncLocal<Dictionary<string, NormalizedError>?> ErrorSnapshots = new();

    public RealAiProviderGateway(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<AiProviderOptions> optionsMonitor,
        IPromptTemplateService promptTemplateService,
        IGatekeeperPolicyService gatekeeperPolicyService,
        IEmbeddingTaggingPolicyService embeddingTaggingPolicyService,
        ICodeMentorPolicyService codeMentorPolicyService,
        IRubricCatalogService rubricCatalogService)
    {
        _httpClientFactory = httpClientFactory;
        _optionsMonitor = optionsMonitor;
        _promptTemplateService = promptTemplateService;
        _gatekeeperPolicyService = gatekeeperPolicyService;
        _embeddingTaggingPolicyService = embeddingTaggingPolicyService;
        _codeMentorPolicyService = codeMentorPolicyService;
        _rubricCatalogService = rubricCatalogService;
    }

    public async Task<GatekeeperVerdict> EvaluateGatekeeperAsync(GatekeeperRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var gatekeeperPolicy = await _gatekeeperPolicyService.GetPolicyAsync(cancellationToken);
            var rendered = await _promptTemplateService.RenderAsync("gatekeeper", new Dictionary<string, string>
            {
                ["subject"] = request.Subject,
                ["language"] = request.Language,
                ["file_name"] = request.FileName,
                ["content"] = request.RawContent,
                ["whitelist_json"] = JsonSerializer.Serialize(gatekeeperPolicy)
            }, cancellationToken);

            var response = await GenerateTextAsync(request.Model, rendered.RenderedSystemPrompt, rendered.RenderedUserPrompt, "gatekeeper", cancellationToken);
            using var document = JsonDocument.Parse(ExtractJsonObject(response));
            var root = document.RootElement;
            var matchedSubjects = root.TryGetProperty("matched_subjects", out var matchedSubjectsElement) && matchedSubjectsElement.ValueKind == JsonValueKind.Array
                ? matchedSubjectsElement.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).Where(static item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : [];
            var detectedTopics = root.TryGetProperty("detected_topics", out var detectedTopicsElement) && detectedTopicsElement.ValueKind == JsonValueKind.Array
                ? detectedTopicsElement.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).Where(static item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : [];
            var verdict = root.TryGetProperty("verdict", out var verdictElement)
                ? NormalizeGatekeeperVerdict(verdictElement.GetString())
                : (root.TryGetProperty("isSupported", out var supportedElement) && supportedElement.ValueKind is JsonValueKind.True or JsonValueKind.False && supportedElement.GetBoolean()
                    ? "supported"
                    : "unsupported");
            var isSupported = verdict == "supported";
            var primaryDomain = root.TryGetProperty("primaryDomain", out var domainElement)
                ? domainElement.GetString() ?? matchedSubjects.FirstOrDefault() ?? request.Subject
                : matchedSubjects.FirstOrDefault() ?? request.Subject;
            var confidence = root.TryGetProperty("confidence", out var confidenceElement) && confidenceElement.TryGetDecimal(out var parsedConfidence)
                ? parsedConfidence
                : isSupported ? 0.8m : 0.4m;
            var rejectionReasonCode = root.TryGetProperty("rejection_reason_code", out var rejectionReasonCodeElement)
                ? rejectionReasonCodeElement.GetString()
                : verdict == "ambiguous" ? "mixed_supported_signals" : verdict == "unsupported" ? "out_of_whitelist_scope" : null;
            return new GatekeeperVerdict(
                isSupported,
                verdict,
                primaryDomain,
                matchedSubjects,
                confidence,
                root.TryGetProperty("reason", out var reasonElement) ? reasonElement.GetString() ?? "No reason returned." : "No reason returned.",
                rejectionReasonCode,
                detectedTopics,
                request.Model);
        }
        catch (Exception ex)
        {
            RecordErrorSnapshot("gatekeeper", request.Model, ex);
            return await _fallbackGateway.EvaluateGatekeeperAsync(request, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<float>> CreateEmbeddingAsync(string content, string model, CancellationToken cancellationToken)
    {
        try
        {
            var lowered = model.ToLowerInvariant();
            if (lowered.Contains("text-embedding") || lowered.Contains("openai"))
            {
            return await CreateOpenAiEmbeddingAsync(content, model, cancellationToken);
            }

            if (lowered.Contains("gemini"))
            {
                return await CreateGeminiEmbeddingAsync(content, model, cancellationToken);
            }

            return await CreateCohereEmbeddingAsync(content, model, cancellationToken);
        }
        catch (Exception ex)
        {
            RecordErrorSnapshot("embedding", model, ex);
            return await _fallbackGateway.CreateEmbeddingAsync(content, model, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<string>> CreateTagsAsync(string content, string subject, string language, IReadOnlyList<string> allowedTags, string model, CancellationToken cancellationToken)
    {
        try
        {
            var policy = await _embeddingTaggingPolicyService.GetPolicyAsync(cancellationToken);
            var rendered = await _promptTemplateService.RenderAsync("auto_tagging", new Dictionary<string, string>
            {
                ["allowed_tags"] = string.Join(", ", allowedTags),
                ["taxonomy_json"] = JsonSerializer.Serialize(policy),
                ["subject"] = subject,
                ["language"] = language,
                ["content"] = content
            }, cancellationToken);

            var response = await GenerateTextAsync(model, rendered.RenderedSystemPrompt, rendered.RenderedUserPrompt, "auto-tagging", cancellationToken);
            using var document = JsonDocument.Parse(ExtractJsonObject(response));
            if (document.RootElement.TryGetProperty("topic_tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                var tags = tagsElement.EnumerateArray()
                    .Select(static item => item.GetString() ?? string.Empty)
                    .Where(static item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (tags.Count > 0)
                {
                    return tags;
                }
            }
        }
        catch (Exception ex)
        {
            RecordErrorSnapshot("auto-tagging", model, ex);
        }

        return await _fallbackGateway.CreateTagsAsync(content, subject, language, allowedTags, model, cancellationToken);
    }

    public async Task<string> DescribeImageAsync(string imageReference, string model, CancellationToken cancellationToken)
    {
        try
        {
            var rendered = await _promptTemplateService.RenderAsync("extract_content_vision", new Dictionary<string, string>
            {
                ["image_ref"] = imageReference
            }, cancellationToken);

            if (ModelLooksLikeGemini(model))
            {
                return await GenerateGeminiVisionTextAsync(model, rendered.RenderedUserPrompt, imageReference, "extract-content", cancellationToken);
            }

            if (ModelLooksLikeOpenAi(model))
            {
                return await GenerateOpenAiVisionTextAsync(model, rendered.RenderedUserPrompt, imageReference, "extract-content", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            RecordErrorSnapshot("extract-content", model, ex);
        }

        return await _fallbackGateway.DescribeImageAsync(imageReference, model, cancellationToken);
    }

    public async Task<IReadOnlyList<GeneratedQuestion>> GenerateQuestionsAsync(QuestionGenerationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var rendered = await _promptTemplateService.RenderAsync("question_generation", new Dictionary<string, string>
            {
                ["subject"] = request.Subject,
                ["difficulty"] = request.Difficulty,
                ["question_type"] = request.QuestionType,
                ["count"] = request.Count.ToString(),
                ["chunks_json"] = JsonSerializer.Serialize(request.Chunks),
                ["revision_feedback"] = request.RevisionFeedback ?? "none",
                ["previous_questions_json"] = JsonSerializer.Serialize(request.PreviousQuestions ?? Array.Empty<GeneratedQuestion>())
            }, cancellationToken);

            var response = await GenerateTextAsync(request.GeneratorModel, rendered.RenderedSystemPrompt, rendered.RenderedUserPrompt, "question-generation", cancellationToken);
            var json = ExtractJsonArray(response);
            var questions = DeserializeQuestions(json);
            var normalizedQuestions = NormalizeQuestions(questions, request);
            return normalizedQuestions.Count > 0 ? normalizedQuestions : await _fallbackGateway.GenerateQuestionsAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            RecordErrorSnapshot("question-generation", request.GeneratorModel, ex);
            return await _fallbackGateway.GenerateQuestionsAsync(request, cancellationToken);
        }
    }

    public async Task<ReviewDecision> ReviewQuestionsAsync(QuestionReviewRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var rubric = await _rubricCatalogService.GetQuestionReviewRubricAsync(request.Subject, request.QuestionType, cancellationToken);
            var rendered = await _promptTemplateService.RenderAsync("question_review", new Dictionary<string, string>
            {
                ["subject"] = request.Subject,
                ["question_type"] = request.QuestionType,
                ["rubric_json"] = JsonSerializer.Serialize(rubric),
                ["chunks_json"] = JsonSerializer.Serialize(request.Chunks),
                ["questions_json"] = JsonSerializer.Serialize(request.Questions)
            }, cancellationToken);

            var response = await GenerateTextAsync(request.ReviewerModel, rendered.RenderedSystemPrompt, rendered.RenderedUserPrompt, "question-review", cancellationToken);
            using var document = JsonDocument.Parse(ExtractJsonObject(response));
            var root = document.RootElement;
            return new ReviewDecision(
                root.TryGetProperty("reviewStatus", out var statusElement) ? statusElement.GetString() ?? "needs_revision" : "needs_revision",
                root.TryGetProperty("issues", out var issuesElement) && issuesElement.ValueKind == JsonValueKind.Array
                    ? issuesElement.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).Where(static item => !string.IsNullOrWhiteSpace(item)).ToList()
                    : [],
                root.TryGetProperty("suggestions", out var suggestionsElement) && suggestionsElement.ValueKind == JsonValueKind.Array
                    ? suggestionsElement.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).Where(static item => !string.IsNullOrWhiteSpace(item)).ToList()
                    : [],
                root.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetDecimal(out var score) ? score : 0.5m,
                root.TryGetProperty("schemaValid", out var schemaValidElement) && schemaValidElement.ValueKind is JsonValueKind.True or JsonValueKind.False ? schemaValidElement.GetBoolean() : false,
                root.TryGetProperty("contentGrounded", out var groundedElement) && groundedElement.ValueKind is JsonValueKind.True or JsonValueKind.False ? groundedElement.GetBoolean() : false,
                root.TryGetProperty("needsRevision", out var revisionElement) && revisionElement.ValueKind is JsonValueKind.True or JsonValueKind.False ? revisionElement.GetBoolean() : true,
                request.ReviewerModel);
        }
        catch (Exception ex)
        {
            RecordErrorSnapshot("question-review", request.ReviewerModel, ex);
            return await _fallbackGateway.ReviewQuestionsAsync(request, cancellationToken);
        }
    }

    public async Task<MentorFeedback> MentorCodeAsync(CodeMentorRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var policy = await _codeMentorPolicyService.GetPolicyAsync(cancellationToken);
            var rubric = await _rubricCatalogService.GetCodeMentorRubricAsync(cancellationToken);
            var combinedCode = CombineSourceFiles(request.SourceFiles, request.Code);
            var rendered = await _promptTemplateService.RenderAsync("code_mentor", new Dictionary<string, string>
            {
                ["subject"] = request.Subject,
                ["language"] = request.Language,
                ["mentor_policy_json"] = JsonSerializer.Serialize(policy),
                ["mentor_rubric_json"] = JsonSerializer.Serialize(rubric),
                ["problem"] = request.Problem,
                ["code"] = combinedCode,
                ["source_files_json"] = JsonSerializer.Serialize(request.SourceFiles ?? Array.Empty<CodeFile>())
            }, cancellationToken);

            var response = await GenerateTextAsync(request.MentorModel, rendered.RenderedSystemPrompt, rendered.RenderedUserPrompt, "code-mentor", cancellationToken);
            using var document = JsonDocument.Parse(ExtractJsonObject(response));
            var root = document.RootElement;
            return new MentorFeedback(
                root.TryGetProperty("verdict", out var verdictElement) ? verdictElement.GetString() ?? "needs_fix" : "needs_fix",
                root.TryGetProperty("issue_categories", out var categoriesElement) && categoriesElement.ValueKind == JsonValueKind.Array
                    ? categoriesElement.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).Where(static item => !string.IsNullOrWhiteSpace(item)).ToList()
                    : [],
                root.TryGetProperty("issues", out var issuesElement) && issuesElement.ValueKind == JsonValueKind.Array
                    ? issuesElement.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).Where(static item => !string.IsNullOrWhiteSpace(item)).ToList()
                    : [],
                root.TryGetProperty("suggestions", out var suggestionsElement) && suggestionsElement.ValueKind == JsonValueKind.Array
                    ? suggestionsElement.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).Where(static item => !string.IsNullOrWhiteSpace(item)).ToList()
                    : [],
                root.TryGetProperty("failing_scenarios", out var failingScenariosElement) && failingScenariosElement.ValueKind == JsonValueKind.Array
                    ? failingScenariosElement.EnumerateArray().Select(static item => item.GetString() ?? string.Empty).Where(static item => !string.IsNullOrWhiteSpace(item)).ToList()
                    : [],
                root.TryGetProperty("confidence", out var confidenceElement) && confidenceElement.TryGetDecimal(out var confidence)
                    ? confidence
                    : 0.6m,
                root.TryGetProperty("complexity", out var complexityElement) ? complexityElement.GetString() : null,
                request.MentorModel);
        }
        catch (Exception ex)
        {
            RecordErrorSnapshot("code-mentor", request.MentorModel, ex);
            return await _fallbackGateway.MentorCodeAsync(request, cancellationToken);
        }
    }

    public TokenCostBreakdown EstimateCost(string stageName, string modelName, int inputSize, int outputSize)
    {
        var inputTokens = Math.Max(1, (long)Math.Ceiling(inputSize / 4.0));
        var outputTokens = Math.Max(1, (long)Math.Ceiling(outputSize / 4.0));
        var latency = Math.Max(120m, (inputTokens + outputTokens) * 1.8m);
        return BuildCostFromUsage(stageName, modelName, inputTokens, outputTokens, latency);
    }

    public TokenCostBreakdown BuildCostFromUsage(string stageName, string modelName, long inputTokens, long outputTokens, decimal latencyMs)
    {
        var baseCost = _fallbackGateway.BuildCostFromUsage(stageName, modelName, inputTokens, outputTokens, latencyMs);
        return baseCost with
        {
            UsageCapture = new UsageCapture("estimated", null, null, null, null)
        };
    }

    public TokenCostBreakdown? ConsumeUsageSnapshot(string usageKey)
    {
        var snapshots = UsageSnapshots.Value;
        if (snapshots is null || !snapshots.Remove(usageKey, out var snapshot))
        {
            return null;
        }

        return snapshot;
    }

    public NormalizedError? ConsumeErrorSnapshot(string usageKey)
    {
        var snapshots = ErrorSnapshots.Value;
        if (snapshots is null || !snapshots.Remove(usageKey, out var snapshot))
        {
            return null;
        }

        return snapshot;
    }

    private async Task<IReadOnlyList<float>> CreateOpenAiEmbeddingAsync(string content, string model, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var options = _optionsMonitor.CurrentValue.OpenAI;
        var client = _httpClientFactory.CreateClient("ai-provider-openai");
        using var request = new HttpRequestMessage(HttpMethod.Post, "embeddings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent(new { model, input = content });
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        TryRecordUsage("embedding", model, payload, stopwatch.ElapsedMilliseconds);
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("data")[0].GetProperty("embedding").EnumerateArray().Select(static item => item.GetSingle()).ToList();
    }

    private async Task<IReadOnlyList<float>> CreateCohereEmbeddingAsync(string content, string model, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var options = _optionsMonitor.CurrentValue.Cohere;
        var client = _httpClientFactory.CreateClient("ai-provider-cohere");
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/embed");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Headers.Add("X-Client-Name", "ape-ai-module");
        request.Content = JsonContent(new { model, texts = new[] { content }, input_type = "search_document" });
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        TryRecordUsage("embedding", model, payload, stopwatch.ElapsedMilliseconds);
        using var document = JsonDocument.Parse(payload);
        var embeddings = document.RootElement.GetProperty("embeddings");
        var firstEmbedding = embeddings.ValueKind == JsonValueKind.Array ? embeddings[0] : embeddings.GetProperty("float")[0];
        return firstEmbedding.EnumerateArray().Select(static item => item.GetSingle()).ToList();
    }

    private async Task<IReadOnlyList<float>> CreateGeminiEmbeddingAsync(string content, string model, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var options = _optionsMonitor.CurrentValue.Gemini;
        var client = _httpClientFactory.CreateClient("ai-provider-gemini");
        var versionPrefix = string.IsNullOrWhiteSpace(options.ApiVersion) ? "v1beta" : options.ApiVersion.Trim('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{versionPrefix}/models/{model}:embedContent?key={Uri.EscapeDataString(options.ApiKey)}");
        request.Content = JsonContent(new
        {
            model = $"models/{model}",
            content = new
            {
                parts = new[] { new { text = content } }
            }
        });
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        TryRecordUsage("embedding", model, payload, stopwatch.ElapsedMilliseconds);
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("embedding").GetProperty("values").EnumerateArray().Select(static item => item.GetSingle()).ToList();
    }

    private async Task<string> GenerateTextAsync(string model, string systemPrompt, string userPrompt, string usageKey, CancellationToken cancellationToken)
    {
        if (ModelLooksLikeGemini(model))
        {
            return await GenerateGeminiTextAsync(model, $"{systemPrompt}\n\n{userPrompt}", usageKey, cancellationToken);
        }

        if (ModelLooksLikeCohere(model))
        {
            return await GenerateCohereTextAsync(model, $"{systemPrompt}\n\n{userPrompt}", usageKey, cancellationToken);
        }

        return await GenerateOpenAiTextAsync(model, systemPrompt, userPrompt, usageKey, cancellationToken);
    }

    private async Task<string> GenerateOpenAiTextAsync(string model, string systemPrompt, string userPrompt, string usageKey, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var options = _optionsMonitor.CurrentValue.OpenAI;
        var client = _httpClientFactory.CreateClient("ai-provider-openai");
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent(new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        });
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        TryRecordUsage(usageKey, model, payload, stopwatch.ElapsedMilliseconds);
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    private async Task<string> GenerateGeminiTextAsync(string model, string prompt, string usageKey, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var options = _optionsMonitor.CurrentValue.Gemini;
        var client = _httpClientFactory.CreateClient("ai-provider-gemini");
        var versionPrefix = string.IsNullOrWhiteSpace(options.ApiVersion) ? "v1beta" : options.ApiVersion.Trim('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{versionPrefix}/models/{model}:generateContent?key={Uri.EscapeDataString(options.ApiKey)}");
        request.Content = JsonContent(new { contents = new[] { new { parts = new[] { new { text = prompt } } } } });
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        TryRecordUsage(usageKey, model, payload, stopwatch.ElapsedMilliseconds);
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? string.Empty;
    }

    private async Task<string> GenerateCohereTextAsync(string model, string prompt, string usageKey, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var options = _optionsMonitor.CurrentValue.Cohere;
        var client = _httpClientFactory.CreateClient("ai-provider-cohere");
        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/chat");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Headers.Add("X-Client-Name", "ape-ai-module");
        request.Content = JsonContent(new { model, messages = new object[] { new { role = "user", content = prompt } } });
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        TryRecordUsage(usageKey, model, payload, stopwatch.ElapsedMilliseconds);
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var contentArray) &&
            contentArray.ValueKind == JsonValueKind.Array &&
            contentArray.GetArrayLength() > 0)
        {
            return contentArray[0].GetProperty("text").GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private async Task<string> GenerateGeminiVisionTextAsync(string model, string prompt, string imageReference, string usageKey, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var options = _optionsMonitor.CurrentValue.Gemini;
        var client = _httpClientFactory.CreateClient("ai-provider-gemini");
        var versionPrefix = string.IsNullOrWhiteSpace(options.ApiVersion) ? "v1beta" : options.ApiVersion.Trim('/');
        var imageBytes = await ReadImageBytesAsync(imageReference, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{versionPrefix}/models/{model}:generateContent?key={Uri.EscapeDataString(options.ApiKey)}");
        request.Content = JsonContent(new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = GuessMimeType(imageReference),
                                data = Convert.ToBase64String(imageBytes)
                            }
                        }
                    }
                }
            }
        });
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        TryRecordUsage(usageKey, model, payload, stopwatch.ElapsedMilliseconds);
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? string.Empty;
    }

    private async Task<string> GenerateOpenAiVisionTextAsync(string model, string prompt, string imageReference, string usageKey, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var options = _optionsMonitor.CurrentValue.OpenAI;
        var client = _httpClientFactory.CreateClient("ai-provider-openai");
        var imageBytes = await ReadImageBytesAsync(imageReference, cancellationToken);
        var mimeType = GuessMimeType(imageReference);
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent(new
        {
            model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}" } }
                    }
                }
            }
        });
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        TryRecordUsage(usageKey, model, payload, stopwatch.ElapsedMilliseconds);
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    private static string ExtractJsonObject(string response)
    {
        var trimmed = response.Trim();
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace ? trimmed[firstBrace..(lastBrace + 1)] : trimmed;
    }

    private static string ExtractJsonArray(string response)
    {
        var trimmed = response.Trim();
        var firstBracket = trimmed.IndexOf('[');
        var lastBracket = trimmed.LastIndexOf(']');
        return firstBracket >= 0 && lastBracket > firstBracket ? trimmed[firstBracket..(lastBracket + 1)] : trimmed;
    }

    private static StringContent JsonContent(object payload)
        => new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static JsonSerializerOptions JsonWebOptions()
        => new(JsonSerializerOptions.Default) { PropertyNameCaseInsensitive = true };

    private static List<GeneratedQuestion> DeserializeQuestions(string json)
    {
        var serializerOptions = JsonWebOptions();
        var questions = JsonSerializer.Deserialize<List<GeneratedQuestion>>(json, serializerOptions);
        if (questions is not null && questions.Count > 0)
        {
            return questions;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<GeneratedQuestion>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            result.Add(new GeneratedQuestion(
                ReadString(item, "type") ?? string.Empty,
                ReadStringArray(item, "topic_tags") ?? [],
                ReadString(item, "difficulty") ?? string.Empty,
                ReadString(item, "title") ?? string.Empty,
                ReadString(item, "description") ?? string.Empty,
                ReadStringArray(item, "source_chunk_ids"),
                ReadCodeFiles(item, "skeleton_code", includeReadonly: true),
                ReadCodeFiles(item, "solution_code", includeReadonly: false),
                ReadTestCases(item, "test_cases"),
                ReadStringArray(item, "options"),
                ReadStringArray(item, "correct_answer"),
                ReadString(item, "explanation")));
        }

        return result;
    }

    private static List<GeneratedQuestion> NormalizeQuestions(IReadOnlyList<GeneratedQuestion> questions, QuestionGenerationRequest request)
    {
        var expectedType = NormalizeQuestionType(request.QuestionType);
        var allowedChunkIds = request.Chunks.Select(static chunk => chunk.ChunkId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<GeneratedQuestion>();

        foreach (var question in questions)
        {
            var candidate = NormalizeQuestion(question, request, expectedType, allowedChunkIds);
            if (candidate is not null)
            {
                normalized.Add(candidate);
            }
        }

        return normalized;
    }

    private static GeneratedQuestion? NormalizeQuestion(
        GeneratedQuestion question,
        QuestionGenerationRequest request,
        string expectedType,
        HashSet<string> allowedChunkIds)
    {
        var type = string.IsNullOrWhiteSpace(question.Type) ? expectedType : NormalizeQuestionType(question.Type);
        if (!type.Equals(expectedType, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var tags = question.TopicTags?
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        if (tags.Count == 0)
        {
            tags = request.Chunks.SelectMany(static chunk => chunk.TopicTags).Where(static tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
        }

        var sourceChunkIds = question.SourceChunkIds?
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Where(allowedChunkIds.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        if (sourceChunkIds.Count == 0)
        {
            sourceChunkIds = request.Chunks.Take(Math.Max(1, Math.Min(3, request.Chunks.Count))).Select(static chunk => chunk.ChunkId).ToList();
        }

        var difficulty = string.IsNullOrWhiteSpace(question.Difficulty) ? request.Difficulty : question.Difficulty.Trim();
        var title = SanitizeText(question.Title);
        var description = SanitizeText(question.Description);
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        if (type.Equals("FE", StringComparison.OrdinalIgnoreCase))
        {
            var options = question.Options?
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(static item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
            var correctAnswer = question.CorrectAnswer?
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(static item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
            var explanation = SanitizeNullableText(question.Explanation);

            if (options.Count < 2)
            {
                return null;
            }

            if (correctAnswer.Count == 0)
            {
                var firstOption = options[0];
                var label = firstOption.Split('.', 2)[0].Trim();
                correctAnswer = [label];
            }
            else
            {
                correctAnswer = NormalizeCorrectAnswers(correctAnswer, options).ToList();
            }

            if (correctAnswer.Count == 0)
            {
                return null;
            }

            return new GeneratedQuestion(
                "FE",
                tags,
                difficulty,
                title,
                description,
                sourceChunkIds,
                null,
                null,
                null,
                options,
                correctAnswer,
                explanation);
        }

        var skeletonCode = NormalizeCodeFiles(question.SkeletonCode, includeReadonly: true);
        var solutionCode = NormalizeCodeFiles(question.SolutionCode, includeReadonly: false);
        var testCases = NormalizeTestCases(question.TestCases);

        if (skeletonCode.Count == 0 || solutionCode.Count == 0 || testCases.Count == 0)
        {
            return null;
        }

        return new GeneratedQuestion(
            "PE",
            tags,
            difficulty,
            title,
            description,
            sourceChunkIds,
            skeletonCode,
            solutionCode,
            testCases,
            null,
            null,
            null);
    }

    private static IReadOnlyList<string> NormalizeCorrectAnswers(IReadOnlyList<string> correctAnswers, IReadOnlyList<string> options)
    {
        var normalized = new List<string>();
        var optionLabels = options
            .Select(static option => option.Split('.', 2)[0].Trim())
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var answer in correctAnswers)
        {
            var trimmed = answer.Trim();
            if (optionLabels.Contains(trimmed))
            {
                normalized.Add(trimmed);
                continue;
            }

            var matchedOption = options.FirstOrDefault(option => option.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (matchedOption is not null)
            {
                normalized.Add(matchedOption.Split('.', 2)[0].Trim());
            }
        }

        return normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<CodeFile> NormalizeCodeFiles(IReadOnlyList<CodeFile>? codeFiles, bool includeReadonly)
    {
        return codeFiles?
            .Where(static item => !string.IsNullOrWhiteSpace(item.FileName) && !string.IsNullOrWhiteSpace(item.Content))
            .Select(item => new CodeFile(item.FileName.Trim(), item.Content.Trim(), includeReadonly ? item.IsReadonly : false))
            .ToList() ?? [];
    }

    private static IReadOnlyList<QuestionTestCase> NormalizeTestCases(IReadOnlyList<QuestionTestCase>? testCases)
    {
        return testCases?
            .Where(static item => !string.IsNullOrWhiteSpace(item.Input) || !string.IsNullOrWhiteSpace(item.ExpectedOutput))
            .Select(item => new QuestionTestCase(
                item.Input?.Trim() ?? string.Empty,
                item.ExpectedOutput?.Trim() ?? string.Empty,
                item.IsHidden))
            .ToList() ?? [];
    }

    private static string NormalizeQuestionType(string questionType)
        => questionType.Trim().Equals("PE", StringComparison.OrdinalIgnoreCase) ? "PE" : "FE";

    private static string SanitizeText(string? value)
        => (value ?? string.Empty).Trim();

    private static string? SanitizeNullableText(string? value)
    {
        var sanitized = SanitizeText(value);
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static List<string>? ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Array)
        {
            return property.EnumerateArray()
                .Select(static item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString())
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }

        var single = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        return string.IsNullOrWhiteSpace(single) ? null : [single];
    }

    private static List<CodeFile>? ReadCodeFiles(JsonElement element, string propertyName, bool includeReadonly)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<CodeFile>();
        foreach (var item in property.EnumerateArray())
        {
            var fileName = ReadString(item, "filename") ?? ReadString(item, "fileName") ?? string.Empty;
            var content = ReadString(item, "content") ?? string.Empty;
            var isReadonly = includeReadonly && item.TryGetProperty("is_readonly", out var readonlyA) && readonlyA.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? readonlyA.GetBoolean()
                : includeReadonly && item.TryGetProperty("isReadonly", out var readonlyB) && readonlyB.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? readonlyB.GetBoolean()
                    : false;

            result.Add(new CodeFile(fileName, content, isReadonly));
        }

        return result;
    }

    private static List<QuestionTestCase>? ReadTestCases(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<QuestionTestCase>();
        foreach (var item in property.EnumerateArray())
        {
            var input = ReadString(item, "input") ?? string.Empty;
            var expectedOutput = ReadString(item, "expected_output") ?? ReadString(item, "expectedOutput") ?? string.Empty;
            var isHidden = item.TryGetProperty("is_hidden", out var hiddenA) && hiddenA.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? hiddenA.GetBoolean()
                : item.TryGetProperty("isHidden", out var hiddenB) && hiddenB.ValueKind is JsonValueKind.True or JsonValueKind.False && hiddenB.GetBoolean();

            result.Add(new QuestionTestCase(input, expectedOutput, isHidden));
        }

        return result;
    }

    private static bool ModelLooksLikeGemini(string model)
        => model.Contains("gemini", StringComparison.OrdinalIgnoreCase);

    private static bool ModelLooksLikeOpenAi(string model)
        => model.Contains("gpt", StringComparison.OrdinalIgnoreCase) || model.Contains("text-embedding", StringComparison.OrdinalIgnoreCase);

    private static bool ModelLooksLikeCohere(string model)
        => model.Contains("command", StringComparison.OrdinalIgnoreCase) || model.Contains("cohere", StringComparison.OrdinalIgnoreCase);

    private static string CombineSourceFiles(IReadOnlyList<CodeFile>? sourceFiles, string? fallbackCode)
    {
        if (sourceFiles is null || sourceFiles.Count == 0)
        {
            return fallbackCode ?? string.Empty;
        }

        return string.Join(
            "\n\n",
            sourceFiles.Select(static file =>
                $"// FILE: {file.FileName}\n{file.Content}".Trim()));
    }

    private static string NormalizeGatekeeperVerdict(string? verdict)
        => verdict?.Trim().ToLowerInvariant() switch
        {
            "supported" => "supported",
            "ambiguous" => "ambiguous",
            _ => "unsupported"
        };

    private static string GuessMimeType(string imageReference)
        => Path.GetExtension(imageReference).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

    private static async Task<byte[]> ReadImageBytesAsync(string imageReference, CancellationToken cancellationToken)
    {
        if (File.Exists(imageReference))
        {
            return await File.ReadAllBytesAsync(imageReference, cancellationToken);
        }

        return Encoding.UTF8.GetBytes(imageReference);
    }

    private void TryRecordUsage(string usageKey, string model, string payload, long latencyMs)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var usage = ExtractUsage(document.RootElement);
            if (usage is null)
            {
                return;
            }

            var snapshots = UsageSnapshots.Value ??= new Dictionary<string, TokenCostBreakdown>(StringComparer.OrdinalIgnoreCase);
            var estimated = _fallbackGateway.BuildCostFromUsage(usageKey, model, usage.Value.InputTokens, usage.Value.OutputTokens, latencyMs);
            var current = estimated with
            {
                UsageCapture = new UsageCapture(
                    "raw",
                    usage.Value.CacheInputTokens,
                    usage.Value.CacheReadTokens,
                    usage.Value.ReasoningTokens,
                    CompactJson(payload))
            };
            snapshots[usageKey] = snapshots.TryGetValue(usageKey, out var existing)
                ? MergeUsageSnapshots(existing, current)
                : current;
            ConsumeErrorSnapshot(usageKey);
        }
        catch
        {
        }
    }

    private void RecordErrorSnapshot(string usageKey, string model, Exception exception)
    {
        try
        {
            var snapshots = ErrorSnapshots.Value ??= new Dictionary<string, NormalizedError>(StringComparer.OrdinalIgnoreCase);
            snapshots[usageKey] = NormalizeProviderError(usageKey, model, exception);
        }
        catch
        {
        }
    }

    private static NormalizedError NormalizeProviderError(string stage, string model, Exception exception)
    {
        var provider = InferProvider(model);
        var rawMessage = BuildRawErrorMessage(exception);
        var providerStatus = FindProviderStatus(exception);
        var lowerMessage = rawMessage.ToLowerInvariant();
        var retryable =
            exception is TaskCanceledException ||
            providerStatus is 408 or 409 or 425 or 429 or 500 or 502 or 503 or 504 ||
            lowerMessage.Contains("quota exceeded", StringComparison.Ordinal) ||
            lowerMessage.Contains("rate limit", StringComparison.Ordinal) ||
            lowerMessage.Contains("temporar", StringComparison.Ordinal) ||
            lowerMessage.Contains("high demand", StringComparison.Ordinal) ||
            lowerMessage.Contains("service unavailable", StringComparison.Ordinal);

        var (category, code) = ClassifyProviderError(exception, providerStatus, lowerMessage, provider);
        return new NormalizedError(category, code, providerStatus, retryable, stage, rawMessage);
    }

    private static (string Category, string Code) ClassifyProviderError(Exception exception, int? providerStatus, string lowerMessage, string provider)
    {
        if (exception is TaskCanceledException)
        {
            return ("timeout", $"{provider}_timeout");
        }

        if (providerStatus == 401 || lowerMessage.Contains("api key", StringComparison.Ordinal) || lowerMessage.Contains("unauthorized", StringComparison.Ordinal))
        {
            return ("auth", $"{provider}_auth_failed");
        }

        if (providerStatus == 403)
        {
            return ("permission", $"{provider}_forbidden");
        }

        if (providerStatus == 404 || lowerMessage.Contains("not found", StringComparison.Ordinal))
        {
            return ("model", $"{provider}_model_not_found");
        }

        if (providerStatus == 429 || lowerMessage.Contains("quota exceeded", StringComparison.Ordinal) || lowerMessage.Contains("rate limit", StringComparison.Ordinal))
        {
            return ("quota", $"{provider}_quota_exceeded");
        }

        if (providerStatus == 400 && (lowerMessage.Contains("image", StringComparison.Ordinal) || lowerMessage.Contains("vision", StringComparison.Ordinal)))
        {
            return ("vision_input", $"{provider}_vision_input_invalid");
        }

        if (providerStatus == 400 || lowerMessage.Contains("invalid", StringComparison.Ordinal) || lowerMessage.Contains("bad request", StringComparison.Ordinal))
        {
            return ("request", $"{provider}_bad_request");
        }

        if (providerStatus >= 500 || lowerMessage.Contains("service unavailable", StringComparison.Ordinal) || lowerMessage.Contains("high demand", StringComparison.Ordinal))
        {
            return ("provider", $"{provider}_service_unavailable");
        }

        if (exception is JsonException)
        {
            return ("response_parse", $"{provider}_response_parse_failed");
        }

        return ("unknown", $"{provider}_unknown_error");
    }

    private static int? FindProviderStatus(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException httpRequestException && httpRequestException.StatusCode is not null)
            {
                return (int)httpRequestException.StatusCode.Value;
            }
        }

        return null;
    }

    private static string BuildRawErrorMessage(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                messages.Add(current.Message.Trim());
            }
        }

        return messages.Count == 0 ? exception.GetType().Name : string.Join(" | ", messages);
    }

    private static string InferProvider(string model)
    {
        var lowered = model.ToLowerInvariant();
        return lowered switch
        {
            var value when value.Contains("gpt") || value.Contains("text-embedding") => "openai",
            var value when value.Contains("gemini") => "gemini",
            var value when value.Contains("cohere") || value.Contains("embed") || value.Contains("command") => "cohere",
            _ => "custom"
        };
    }

    private static (long InputTokens, long OutputTokens, long? CacheInputTokens, long? CacheReadTokens, long? ReasoningTokens)? ExtractUsage(JsonElement root)
    {
        if (root.TryGetProperty("usage", out var usage))
        {
            var inputTokens = ReadLong(usage, "prompt_tokens")
                              ?? ReadLong(usage, "input_tokens")
                              ?? ReadLong(usage, "promptTokens")
                              ?? 0;
            var outputTokens = ReadLong(usage, "completion_tokens")
                               ?? ReadLong(usage, "output_tokens")
                               ?? ReadLong(usage, "completionTokens")
                               ?? 0;
            var cacheInputTokens = ReadLong(usage, "cached_tokens") ?? ReadLong(usage, "cache_creation_input_tokens");
            var cacheReadTokens = ReadLong(usage, "cache_read_input_tokens");
            var reasoningTokens = ReadLong(usage, "reasoning_tokens");
            if (inputTokens > 0 || outputTokens > 0)
            {
                return (inputTokens, outputTokens, cacheInputTokens, cacheReadTokens, reasoningTokens);
            }
        }

        if (root.TryGetProperty("usageMetadata", out var usageMetadata))
        {
            var inputTokens = ReadLong(usageMetadata, "promptTokenCount") ?? 0;
            var outputTokens = ReadLong(usageMetadata, "candidatesTokenCount") ?? 0;
            var cacheInputTokens = ReadLong(usageMetadata, "cachedContentTokenCount");
            if (inputTokens > 0 || outputTokens > 0)
            {
                return (inputTokens, outputTokens, cacheInputTokens, null, null);
            }
        }

        if (root.TryGetProperty("meta", out var meta))
        {
            if (meta.TryGetProperty("billed_units", out var billedUnits))
            {
                var inputTokens = ReadLong(billedUnits, "input_tokens") ?? 0;
                var outputTokens = ReadLong(billedUnits, "output_tokens") ?? 0;
                if (inputTokens > 0 || outputTokens > 0)
                {
                    return (inputTokens, outputTokens, null, null, null);
                }
            }

            if (meta.TryGetProperty("tokens", out var tokens))
            {
                var inputTokens = ReadLong(tokens, "input_tokens") ?? 0;
                var outputTokens = ReadLong(tokens, "output_tokens") ?? 0;
                if (inputTokens > 0 || outputTokens > 0)
                {
                    return (inputTokens, outputTokens, null, null, null);
                }
            }
        }

        return null;
    }

    private static long? ReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value))
        {
            return value;
        }

        return property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private static string CompactJson(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch
        {
            return payload;
        }
    }

    private static TokenCostBreakdown MergeUsageSnapshots(TokenCostBreakdown existing, TokenCostBreakdown current)
    {
        var usageSource = existing.UsageCapture?.UsageSource == current.UsageCapture?.UsageSource
            ? current.UsageCapture?.UsageSource ?? existing.UsageCapture?.UsageSource
            : "mixed";

        return new TokenCostBreakdown(
            existing.InputTokens + current.InputTokens,
            existing.OutputTokens + current.OutputTokens,
            existing.InputCostUsd + current.InputCostUsd,
            existing.OutputCostUsd + current.OutputCostUsd,
            existing.TotalCostUsd + current.TotalCostUsd,
            existing.LatencyMs + current.LatencyMs,
            usageSource is null
                ? null
                : new UsageCapture(
                    usageSource,
                    SumNullable(existing.UsageCapture?.CacheInputTokens, current.UsageCapture?.CacheInputTokens),
                    SumNullable(existing.UsageCapture?.CacheReadTokens, current.UsageCapture?.CacheReadTokens),
                    SumNullable(existing.UsageCapture?.ReasoningTokens, current.UsageCapture?.ReasoningTokens),
                    current.UsageCapture?.RawUsageJson),
            current.Error ?? existing.Error);
    }

    private static long? SumNullable(long? left, long? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return left.Value + right.Value;
    }
}
