/**
 * CodeMentorService.cs
 * Implementation of Step 5 in the APE AI Pipeline: AI Code Mentor.
 * 
 * Responsibilities:
 * 1. Analyzes student programming code submissions, compiler errors, runtime exceptions, and failing test cases.
 * 2. Uses ExecutionFeedbackDiagnosticParser to pinpoint the exact failure location.
 * 3. Prompts the Mentor Agent (OpenAI / DeepSeek fallback) using Socratic guidance rules.
 * 4. Provides actionable, pedagogy-first hints without directly revealing solutions.
 * 5. Logs token usage and handles VND billing transactions.
 */

using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Options;
using Domain.Constants;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Service providing AI-powered Socratic mentoring and automated feedback for programming exercises.
/// </summary>
public class CodeMentorService
{
    private const int MaxMentorRequestsPerSubmission = 3;

    private readonly IPESubmissionRepository _peSubmissionRepository;
    private readonly IPracticeSessionRepository _practiceSessionRepository;
    private readonly IPEQuestionRepository _peQuestionRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IAIFeatureRoutingService _routingService;
    private readonly IAIExecutionService _aiExecutionService;
    private readonly IAIPromptService _promptService;
    private readonly IAIArtifactCatalogService _artifactCatalogService;
    private readonly IAIMentorFeedbackRepository _aiMentorFeedbackRepository;
    private readonly IAIUsageLogRepository _aiUsageLogRepository;
    private readonly IAIVndBillingService _vndBillingService;
    private readonly IAIVndBillingTransactionRepository _billingTransactionRepository;

    public CodeMentorService(
        IPESubmissionRepository peSubmissionRepository,
        IPracticeSessionRepository practiceSessionRepository,
        IPEQuestionRepository peQuestionRepository,
        ICourseRepository courseRepository,
        IAIFeatureRoutingService routingService,
        IAIExecutionService aiExecutionService,
        IAIPromptService promptService,
        IAIArtifactCatalogService artifactCatalogService,
        IAIMentorFeedbackRepository aiMentorFeedbackRepository,
        IAIUsageLogRepository aiUsageLogRepository,
        IAIVndBillingService vndBillingService,
        IAIVndBillingTransactionRepository billingTransactionRepository)
    {
        _peSubmissionRepository = peSubmissionRepository;
        _practiceSessionRepository = practiceSessionRepository;
        _peQuestionRepository = peQuestionRepository;
        _courseRepository = courseRepository;
        _routingService = routingService;
        _aiExecutionService = aiExecutionService;
        _promptService = promptService;
        _artifactCatalogService = artifactCatalogService;
        _aiMentorFeedbackRepository = aiMentorFeedbackRepository;
        _aiUsageLogRepository = aiUsageLogRepository;
        _vndBillingService = vndBillingService;
        _billingTransactionRepository = billingTransactionRepository;
    }

    public async Task<CodeMentorResultDto> RunAsync(
    string submissionId,
    string requesterUserId,
    CodeMentorRequestDto request,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(submissionId))
        {
            throw new ArgumentException(
                "SubmissionId is required.",
                nameof(submissionId));
        }
        if (string.IsNullOrWhiteSpace(requesterUserId))
        {
            throw new ArgumentException(
                "RequesterUserId is required.",
                nameof(requesterUserId));
        }

        ArgumentNullException.ThrowIfNull(request);

        await _vndBillingService.EnsureMinimumBalanceAsync(requesterUserId, cancellationToken);

        var submission =
            await _peSubmissionRepository.GetByIdAsync(
                submissionId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "PE submission not found.");

        await EnsureSubmissionOwnershipAsync(
            submission,
            requesterUserId,
            cancellationToken);

        var question =
            await _peQuestionRepository.GetByIdAsync(
                submission.QuestionId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "PE question not found for this submission.");

        var course = string.IsNullOrWhiteSpace(question.CourseId)
            ? null
            : await _courseRepository.GetByIdAsync(question.CourseId);

        await EnsureMentorQuotaAsync(
            submission.Id,
            cancellationToken);

        var prompt = await _artifactCatalogService.GetPromptAsync("code_mentor", cancellationToken)
            ?? throw new InvalidOperationException("Code mentor prompt is not configured.");
        var policyArtifact = await _artifactCatalogService.GetArtifactAsync("code-mentor-policy.json", cancellationToken)
            ?? throw new InvalidOperationException("Code mentor policy is not configured.");
        var rubricArtifact = await _artifactCatalogService.GetArtifactAsync(Path.Combine("ai-rubrics", "CODE_MENTOR.json"), cancellationToken)
            ?? throw new InvalidOperationException("Code mentor rubric is not configured.");
        var policy = policyArtifact.Content;
        var rubric = rubricArtifact.Content;

        var resolved = _routingService.ResolveText(AIFeatureNames.CodeMentor, request.RuntimeOverride);

        var combinedCode = CombineSourceFiles(submission.SubmittedCode);
        var renderedUserPrompt = _promptService.Render(prompt.UserPrompt, new Dictionary<string, string?>
        {
            ["subject"] = NormalizeSubject(course?.Code, submission.TopicTags),
            ["language"] = MapLanguage(submission.LanguageId),
            ["mentor_policy_json"] = JsonSerializer.Serialize(policy, JsonOptions),
            ["mentor_rubric_json"] = JsonSerializer.Serialize(rubric, JsonOptions),
            ["problem"] = BuildProblemStatement(question),
            ["code"] = combinedCode,
            ["source_files_json"] =
    JsonSerializer.Serialize(
        submission.SubmittedCode,
        JsonOptions)
        });

        var response = await _aiExecutionService.ExecuteTextAsync(
            resolved,
            new AITextRequest
            {
                FeatureName = AIFeatureNames.CodeMentor,
                SystemPrompt = prompt.SystemPrompt,
                UserPrompt = renderedUserPrompt,
                Provider = resolved.Provider,
                Model = resolved.Model,
                Temperature = resolved.Temperature,
                MaxTokens = resolved.MaxTokens
            },
            cancellationToken);

        var feedback = ParseFeedback(response.Content, response.Model ?? resolved.Model);
        var usage = AIUsageAccounting.ForText(
            response.Provider ?? resolved.Provider,
            resolved.Model,
            response.Model,
            response.InputTokens,
            response.OutputTokens,
            response.TotalTokens,
            response.ReportedCostUsd,
            response.UsageSource,
            response.CostSource);
        var now = DateTime.UtcNow;
        var inputTokens = usage.InputTokens;
        var outputTokens = usage.OutputTokens;
        var totalTokens = usage.TotalTokens;
        var costUsd = usage.CostUsd;
        var entity = new AIMentorFeedback
        {
            AgentId = AIAgentCatalog.MentorAgentId,
            SubmissionId = submission.Id,
            Date = now,
            QuestionType = "PE",
            Verdict = feedback.Verdict,
            QualityScore = feedback.QualityScore,
            PerformanceSummary = feedback.PerformanceSummary,
            ErrorAnalysis = feedback.ErrorAnalysis,
            ImprovementSuggestions = feedback.ImprovementSuggestions,
            IssueCategories = feedback.IssueCategories,
            FeedbackText = feedback.FeedbackText,
            SuggestedComplexity = feedback.SuggestedComplexity,
            ModelName = feedback.ModelName,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _aiMentorFeedbackRepository.CreateAsync(entity);

        var usageLog = new AIUsageLog
        {
            TriggeredBy = requesterUserId,
            AgentId = AIAgentCatalog.MentorAgentId,
            CreditsDeducted = 0d,
            TokensUsed = totalTokens,
            CostUsd = costUsd,
            CreatedAt = now,
            PayloadData = new Dictionary<string, object?>
            {
                ["feature"] = "CodeMentor",
                ["step"] = "code_mentor",
                ["submission_id"] = submission.Id,
                ["question_id"] = submission.QuestionId,
                ["question_type"] = "PE",
                ["provider"] = usage.Provider,
                ["model"] = usage.EffectiveModel,
                ["configured_model"] = resolved.Model,
                ["effective_model"] = usage.EffectiveModel,
                ["model_family"] = usage.ModelFamily,
                ["normalized_model_key"] = usage.NormalizedModelKey,
                ["language"] = MapLanguage(submission.LanguageId),
                ["usage_source"] = usage.UsageSource,
                ["cost_source"] = usage.CostSource,
                ["fallback_used"] = response.FromFallback,
                ["fallback_from_provider"] = response.FallbackFromProvider,
                ["fallback_from_model"] = response.FallbackFromModel,
                ["fallback_reason_code"] = response.FallbackReasonCode,
                ["prompt_key"] = prompt.Key,
                ["prompt_version"] = prompt.Version,
                ["prompt_source"] = prompt.Source,
                ["policy_key"] = policyArtifact.ArtifactKey,
                ["policy_version"] = policyArtifact.Version,
                ["policy_source"] = policyArtifact.Source,
                ["rubric_key"] = rubricArtifact.ArtifactKey,
                ["rubric_version"] = rubricArtifact.Version,
                ["rubric_source"] = rubricArtifact.Source,
                ["runtime_snapshot"] = BuildRuntimeSnapshot(resolved, usage, response),
                ["artifact_snapshot"] = BuildArtifactSnapshot(prompt, policyArtifact, rubricArtifact),
                ["input_tokens"] = inputTokens,
                ["output_tokens"] = outputTokens,
                ["total_tokens"] = totalTokens,
                ["raw_response"] = response.RawResponse
            }
        };
        await _aiUsageLogRepository.CreateAsync(usageLog);

        var charge = await _vndBillingService.ChargeAsync(
            new AIVndChargeRequest
            {
                UserId = requesterUserId,
                FeatureKey = AICreditFeatureKeys.CodeMentor,
                FeatureName = "AI mentor",
                SourceEntityType = "Submission",
                SourceEntityId = submission.Id,
                CreatedBy = requesterUserId,
                ReportedCostUsd = costUsd,
                UsageLogIds = new List<string> { usageLog.Id },
                UsageSnapshot = new Dictionary<string, object?>
                {
                    ["mentor_total_tokens"] = totalTokens,
                    ["mentor_cost_usd"] = costUsd,
                    ["usage_source"] = usage.UsageSource,
                    ["cost_source"] = usage.CostSource
                }
            },
            cancellationToken);

        return new CodeMentorResultDto
        {
            SubmissionId = submission.Id,
            Feedback = feedback,
            Provider = usage.Provider,
            Model = usage.EffectiveModel,
            ConfiguredModel = resolved.Model,
            EffectiveModel = usage.EffectiveModel,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            CostUsd = costUsd,
            ReportedCostUsd = charge.ReportedCostUsd,
            UsdToVndRate = charge.UsdToVndRate,
            ChargedVnd = charge.ChargedVnd,
            ActualDeductedVnd = charge.ActualDeductedVnd,
            AbsorbedVnd = charge.AbsorbedVnd,
            RemainingBalanceVnd = charge.BalanceAfterVnd,
            UsageSource = usage.UsageSource,
            CostSource = usage.CostSource,
            FallbackUsed = response.FromFallback,
            FallbackFromProvider = response.FallbackFromProvider,
            FallbackFromModel = response.FallbackFromModel,
            FallbackReasonCode = response.FallbackReasonCode,
            CreatedAt = now
        };
    }

    public async Task<CodeMentorResultDto> RunSmokeAsync(
        CodeMentorSmokeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.SubmittedCode is null || request.SubmittedCode.Count == 0)
        {
            throw new InvalidOperationException("Smoke code mentor requires generated code input from the previous step.");
        }

        var prompt = await _artifactCatalogService.GetPromptAsync("code_mentor", cancellationToken)
            ?? throw new InvalidOperationException("Code mentor prompt is not configured.");
        var policyArtifact = await _artifactCatalogService.GetArtifactAsync("code-mentor-policy.json", cancellationToken)
            ?? throw new InvalidOperationException("Code mentor policy is not configured.");
        var rubricArtifact = await _artifactCatalogService.GetArtifactAsync(Path.Combine("ai-rubrics", "CODE_MENTOR.json"), cancellationToken)
            ?? throw new InvalidOperationException("Code mentor rubric is not configured.");
        var policy = policyArtifact.Content;
        var rubric = rubricArtifact.Content;
        var resolved = _routingService.ResolveText(AIFeatureNames.CodeMentor, request.RuntimeOverride);
        var combinedCode = CombineSourceFiles(request.SubmittedCode);
        var subject = NormalizeSmokeSubject(request.Subject, request.TopicTags);
        var language = NormalizeSmokeLanguage(request.Language);
        var renderedUserPrompt = _promptService.Render(prompt.UserPrompt, new Dictionary<string, string?>
        {
            ["subject"] = subject,
            ["language"] = language,
            ["mentor_policy_json"] = JsonSerializer.Serialize(policy, JsonOptions),
            ["mentor_rubric_json"] = JsonSerializer.Serialize(rubric, JsonOptions),
            ["problem"] = BuildProblemStatement(request.QuestionTitle, request.QuestionDescription),
            ["code"] = combinedCode,
            ["source_files_json"] = JsonSerializer.Serialize(request.SubmittedCode, JsonOptions)
        });

        var response = await _aiExecutionService.ExecuteTextAsync(
            resolved,
            new AITextRequest
            {
                FeatureName = AIFeatureNames.CodeMentor,
                SystemPrompt = prompt.SystemPrompt,
                UserPrompt = renderedUserPrompt,
                Provider = resolved.Provider,
                Model = resolved.Model,
                Temperature = resolved.Temperature,
                MaxTokens = resolved.MaxTokens
            },
            cancellationToken);

        var feedback = ParseFeedback(response.Content, response.Model ?? resolved.Model);
        var usage = AIUsageAccounting.ForText(
            response.Provider ?? resolved.Provider,
            resolved.Model,
            response.Model,
            response.InputTokens,
            response.OutputTokens,
            response.TotalTokens,
            response.ReportedCostUsd,
            response.UsageSource,
            response.CostSource);
        var now = DateTime.UtcNow;

        return new CodeMentorResultDto
        {
            SubmissionId = string.IsNullOrWhiteSpace(request.SubmissionId) ? $"smoke-{Guid.NewGuid():N}"[..18] : request.SubmissionId,
            Feedback = feedback,
            Provider = usage.Provider,
            Model = usage.EffectiveModel,
            ConfiguredModel = resolved.Model,
            EffectiveModel = usage.EffectiveModel,
            InputTokens = usage.InputTokens,
            OutputTokens = usage.OutputTokens,
            TotalTokens = usage.TotalTokens,
            CostUsd = usage.CostUsd,
            ReportedCostUsd = usage.CostUsd,
            UsdToVndRate = 0m,
            ChargedVnd = 0,
            ActualDeductedVnd = 0,
            AbsorbedVnd = 0,
            RemainingBalanceVnd = 0,
            UsageSource = usage.UsageSource,
            CostSource = usage.CostSource,
            FallbackUsed = response.FromFallback,
            FallbackFromProvider = response.FallbackFromProvider,
            FallbackFromModel = response.FallbackFromModel,
            FallbackReasonCode = response.FallbackReasonCode,
            CreatedAt = now
        };
    }

    public async Task<MentorFeedbackDto?> GetLatestAsync(
      string submissionId,
      string requesterUserId,
      CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId))
        {
            throw new ArgumentException(
                "RequesterUserId is required.",
                nameof(requesterUserId));
        }

        var submission =
            await _peSubmissionRepository.GetByIdAsync(
                submissionId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "PE submission not found.");

        await EnsureSubmissionOwnershipAsync(
            submission,
            requesterUserId,
            cancellationToken);

        var feedback =
            await _aiMentorFeedbackRepository
                .GetLatestBySubmissionIdAsync(
                    submission.Id);

        return feedback is null
            ? null
            : await MapFeedbackAsync(feedback, cancellationToken);
    }

    public async Task<List<MentorFeedbackDto>> GetHistoryAsync(
       string submissionId,
       string requesterUserId,
       int take = 20,
       CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId))
        {
            throw new ArgumentException(
                "RequesterUserId is required.",
                nameof(requesterUserId));
        }

        var submission =
            await _peSubmissionRepository.GetByIdAsync(
                submissionId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "PE submission not found.");

        await EnsureSubmissionOwnershipAsync(
            submission,
            requesterUserId,
            cancellationToken);

        var normalizedTake = Math.Clamp(
            take,
            1,
            100);

        var items =
            await _aiMentorFeedbackRepository
                .GetBySubmissionIdAsync(
                    submission.Id,
                    normalizedTake);

        var mappedItems = new List<MentorFeedbackDto>(items.Count);
        foreach (var item in items)
        {
            mappedItems.Add(await MapFeedbackAsync(item, cancellationToken));
        }

        return mappedItems;
    }

    private static string BuildProblemStatement(PEQuestion question)
    {
        return $"{question.Title}\n\n{question.Description}";
    }

    private static string BuildProblemStatement(string? title, string? description)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? "Smoke mentor problem" : title.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? "Evaluate the submitted solution and provide mentor feedback." : description.Trim();
        return $"{normalizedTitle}\n\n{normalizedDescription}";
    }

    private static string CombineSourceFiles(
    IEnumerable<CodeFile>? sourceFiles)
    {
        if (sourceFiles is null)
        {
            return string.Empty;
        }

        var files = sourceFiles.ToList();

        if (files.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "\n\n",
            files.Select(file =>
                $"// File: {file.Filename}\n{file.Content}"
                    .Trim()));
    }

   
   

    private static string MapLanguage(int languageId)
    {
        return languageId switch
        {
            50 => "c",
            62 => "java",
            _ => "unknown"
        };
    }

    private static string NormalizeSubject(
    string? courseCode,
    IEnumerable<string>? topicTags)
    {
        var normalizedCourse = AISubjectDomainMapper.NormalizeSubjectOrCourseCode(courseCode);
        if (!string.IsNullOrWhiteSpace(normalizedCourse) &&
            (normalizedCourse == AISubjectDomainMapper.C ||
             normalizedCourse == AISubjectDomainMapper.JavaOop ||
             normalizedCourse == AISubjectDomainMapper.DsaJava))
        {
            return normalizedCourse;
        }

        var joined = string.Join(" ", topicTags ?? Array.Empty<string>()).ToUpperInvariant();
        if (joined.Contains("DSA", StringComparison.OrdinalIgnoreCase)) return AISubjectDomainMapper.DsaJava;
        if (joined.Contains("JAVA", StringComparison.OrdinalIgnoreCase) || joined.Contains("OOP", StringComparison.OrdinalIgnoreCase)) return AISubjectDomainMapper.JavaOop;
        return AISubjectDomainMapper.C;
    }

    private static string NormalizeSmokeSubject(string? subject, IEnumerable<string>? topicTags)
    {
        var normalized = AISubjectDomainMapper.NormalizeSubjectOrCourseCode(subject);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return NormalizeSubject(subject, topicTags);
    }

    private static string NormalizeSmokeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "java";
        }

        return language.Trim().ToLowerInvariant() switch
        {
            "50" or "c" => "c",
            "62" or "java" => "java",
            _ => language.Trim().ToLowerInvariant()
        };
    }

    private static MentorFeedbackDto ParseFeedback(string content, string modelName)
    {
        var json = ExtractJsonObject(content);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var errorAnalysis = ReadArray(root, "error_analysis")
            .Select(item => new MentorErrorAnalysisItem
            {
                Category = ReadString(item, "category"),
                Severity = ReadString(item, "severity"),
                Title = ReadString(item, "title"),
                Detail = ReadString(item, "detail"),
                FailingScenarios = ReadStringList(item, "failing_scenarios")
            }).ToList();

        var suggestions = ReadArray(root, "improvement_suggestions")
            .Select(item => new MentorImprovementSuggestion
            {
                Priority = ReadString(item, "priority"),
                Title = ReadString(item, "title"),
                Detail = ReadString(item, "detail"),
                ExpectedImpact = ReadString(item, "expected_impact")
            }).ToList();

        return new MentorFeedbackDto
        {
            QuestionType = ReadString(root, "question_type") ?? "PE",
            Verdict = ReadString(root, "verdict") ?? "needs_fix",
            QualityScore = ReadObject(root, "quality_score") is JsonElement quality
                ? new MentorQualityScore
                {
                    Overall = ReadDouble(quality, "overall") ?? 0,
                    Correctness = ReadDouble(quality, "correctness") ?? 0,
                    Robustness = ReadDouble(quality, "robustness") ?? 0,
                    CodeQuality = ReadDouble(quality, "code_quality") ?? 0,
                    Efficiency = ReadDouble(quality, "efficiency") ?? 0,
                    Confidence = ReadDouble(quality, "confidence") ?? 0
                }
                : null,
            PerformanceSummary = ReadObject(root, "performance_summary") is JsonElement performance
                ? new MentorPerformanceSummary
                {
                    Summary = ReadString(performance, "summary"),
                    TimeComplexity = ReadString(performance, "time_complexity"),
                    SpaceComplexity = ReadString(performance, "space_complexity"),
                    Notes = ReadStringList(performance, "notes")
                }
                : null,
            ErrorAnalysis = errorAnalysis,
            ImprovementSuggestions = suggestions,
            IssueCategories = ReadStringList(root, "issue_categories"),
            FeedbackText = ReadString(root, "feedback_text"),
            SuggestedComplexity = ReadString(root, "suggested_complexity"),
            ModelName = modelName
        };
    }

    private async Task<MentorFeedbackDto> MapFeedbackAsync(
        AIMentorFeedback feedback,
        CancellationToken cancellationToken)
    {
        var billing = await _billingTransactionRepository.GetLatestBySourceEntityAsync(
            "Submission",
            feedback.SubmissionId,
            AICreditFeatureKeys.CodeMentor,
            cancellationToken);

        return new MentorFeedbackDto
        {
            SubmissionId = feedback.SubmissionId,
            QuestionType = feedback.QuestionType,
            Verdict = feedback.Verdict ?? "needs_fix",
            QualityScore = feedback.QualityScore,
            PerformanceSummary = feedback.PerformanceSummary,
            ErrorAnalysis = feedback.ErrorAnalysis,
            ImprovementSuggestions = feedback.ImprovementSuggestions,
            IssueCategories = feedback.IssueCategories,
            FeedbackText = feedback.FeedbackText,
            SuggestedComplexity = feedback.SuggestedComplexity,
            ModelName = feedback.ModelName,
            ChargedVnd = billing?.ChargedVnd ?? 0,
            ActualDeductedVnd = billing?.ActualDeductedVnd ?? 0,
            AbsorbedVnd = billing?.AbsorbedVnd ?? 0,
            RemainingBalanceVnd = billing?.BalanceAfterVnd ?? 0,
            ReportedCostUsd = billing?.ReportedCostUsd ?? 0,
            UsdToVndRate = billing?.UsdToVndRate ?? 0,
            BillingStatus = billing?.Status,
            FallbackUsed = false,
            Date = feedback.Date,
            CreatedAt = feedback.CreatedAt
        };
    }

    private static string ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            throw new InvalidOperationException("Code mentor output could not be parsed as JSON.");
        }

        return content[start..(end + 1)];
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static double? ReadDouble(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value)
            ? value
            : null;

    private static JsonElement? ReadObject(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object
            ? property
            : null;

    private static List<JsonElement> ReadArray(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.EnumerateArray().Select(item => item.Clone()).ToList()
            : new List<JsonElement>();

    private static List<string> ReadStringList(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToList()
            : new List<string>();

    private async Task EnsureMentorQuotaAsync(
        string submissionId,
        CancellationToken cancellationToken)
    {
        var mentorCount = await _aiMentorFeedbackRepository.CountBySubmissionIdsAsync([submissionId]);
        if (mentorCount >= MaxMentorRequestsPerSubmission)
        {
            throw new InvalidOperationException(
                $"You have used all {MaxMentorRequestsPerSubmission} AI mentor requests for this submission.");
        }
    }

    private async Task EnsureSubmissionOwnershipAsync(
        PE_Submission submission,
        string requesterUserId,
        CancellationToken cancellationToken)
    {
        var session =
            await _practiceSessionRepository.GetByIdAsync(
                submission.SessionId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Practice session not found for this submission.");

        if (!string.Equals(session.StudentId, requesterUserId, StringComparison.Ordinal))
        {
            throw new KeyNotFoundException(
                "PE submission not found.");
        }
    }

    private static Dictionary<string, object?> BuildRuntimeSnapshot(
        AIResolvedExecutionOptions resolved,
        AITextUsageSnapshot usage,
        AITextResponse response)
    {
        return new Dictionary<string, object?>
        {
            ["feature_name"] = resolved.FeatureName,
            ["configured_provider"] = resolved.Provider,
            ["configured_model"] = resolved.Model,
            ["effective_provider"] = usage.Provider,
            ["effective_model"] = usage.EffectiveModel,
            ["frontend_override_used"] = resolved.UsedFrontendOverride,
            ["fallback_configured"] = !string.IsNullOrWhiteSpace(resolved.FallbackProvider) && !string.IsNullOrWhiteSpace(resolved.FallbackModel),
            ["fallback_provider"] = resolved.FallbackProvider,
            ["fallback_model"] = resolved.FallbackModel,
            ["fallback_used"] = response.FromFallback,
            ["usage_source"] = usage.UsageSource,
            ["cost_source"] = usage.CostSource
        };
    }

    private static Dictionary<string, object?> BuildArtifactSnapshot(
        AIPromptTemplateDto promptArtifact,
        AIJsonArtifactDto policyArtifact,
        AIJsonArtifactDto rubricArtifact)
    {
        return new Dictionary<string, object?>
        {
            ["prompt"] = new Dictionary<string, object?>
            {
                ["key"] = promptArtifact.Key,
                ["version"] = promptArtifact.Version,
                ["source"] = promptArtifact.Source,
                ["artifact_type"] = promptArtifact.ArtifactType
            },
            ["policy"] = new Dictionary<string, object?>
            {
                ["key"] = policyArtifact.ArtifactKey,
                ["version"] = policyArtifact.Version,
                ["source"] = policyArtifact.Source,
                ["artifact_type"] = policyArtifact.ArtifactType
            },
            ["rubric"] = new Dictionary<string, object?>
            {
                ["key"] = rubricArtifact.ArtifactKey,
                ["version"] = rubricArtifact.Version,
                ["source"] = rubricArtifact.Source,
                ["artifact_type"] = rubricArtifact.ArtifactType
            }
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

}
