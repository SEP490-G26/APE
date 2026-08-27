using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/admin/ai")]
[Authorize(Roles = "Admin")]
public class AdminAIController : ControllerBase
{
    private readonly IAIUsageLogRepository _aiUsageLogRepository;
    private readonly IAIVndBillingTransactionRepository _aiVndBillingTransactionRepository;
    private readonly IAIVndBillingService _aiVndBillingService;
    private readonly IAIMentorFeedbackRepository _aiMentorFeedbackRepository;
    private readonly IAIGatekeeperService _aiGatekeeperService;
    private readonly IAIExtractedContentService _aiExtractedContentService;
    private readonly IAIEmbeddingTaggingService _aiEmbeddingTaggingService;
    private readonly IAIProviderCredentialAdminService _aiProviderCredentialAdminService;
    private readonly IAIProviderSettingsResolver _aiProviderSettingsResolver;
    private readonly IAIProviderModelCatalogService _aiProviderModelCatalogService;
    private readonly IAIAgentAdminService _aiAgentAdminService;
    private readonly IRetrievalPlannerService _retrievalPlannerService;
    private readonly IQuestionGenerationReviewService _questionGenerationReviewService;
    private readonly CodeMentorService _codeMentorService;
    private readonly IPESubmissionRepository _peSubmissionRepository;
    private readonly IUserRepository _userRepository;

    public AdminAIController(
        IAIUsageLogRepository aiUsageLogRepository,
        IAIVndBillingTransactionRepository aiVndBillingTransactionRepository,
        IAIVndBillingService aiVndBillingService,
        IAIMentorFeedbackRepository aiMentorFeedbackRepository,
        IAIGatekeeperService aiGatekeeperService,
        IAIExtractedContentService aiExtractedContentService,
        IAIEmbeddingTaggingService aiEmbeddingTaggingService,
        IAIProviderCredentialAdminService aiProviderCredentialAdminService,
        IAIProviderSettingsResolver aiProviderSettingsResolver,
        IAIProviderModelCatalogService aiProviderModelCatalogService,
        IAIAgentAdminService aiAgentAdminService,
        IRetrievalPlannerService retrievalPlannerService,
        IQuestionGenerationReviewService questionGenerationReviewService,
        CodeMentorService codeMentorService,
        IPESubmissionRepository peSubmissionRepository,
        IUserRepository userRepository)
    {
        _aiUsageLogRepository = aiUsageLogRepository;
        _aiVndBillingTransactionRepository = aiVndBillingTransactionRepository;
        _aiVndBillingService = aiVndBillingService;
        _aiMentorFeedbackRepository = aiMentorFeedbackRepository;
        _aiGatekeeperService = aiGatekeeperService;
        _aiExtractedContentService = aiExtractedContentService;
        _aiEmbeddingTaggingService = aiEmbeddingTaggingService;
        _aiProviderCredentialAdminService = aiProviderCredentialAdminService;
        _aiProviderSettingsResolver = aiProviderSettingsResolver;
        _aiProviderModelCatalogService = aiProviderModelCatalogService;
        _aiAgentAdminService = aiAgentAdminService;
        _retrievalPlannerService = retrievalPlannerService;
        _questionGenerationReviewService = questionGenerationReviewService;
        _codeMentorService = codeMentorService;
        _peSubmissionRepository = peSubmissionRepository;
        _userRepository = userRepository;
    }

    [HttpGet("billing-config")]
    [ProducesResponseType(typeof(ApiResponse<AIVndBillingConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBillingConfig(CancellationToken cancellationToken = default)
    {
        var result = await _aiVndBillingService.GetConfigAsync(cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("billing-config")]
    [ProducesResponseType(typeof(ApiResponse<AIVndBillingConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertBillingConfig([FromBody] AIVndBillingConfigUpsertRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (!TryGetRequiredUserId(out var userId, out var unauthorized))
        {
            return unauthorized!;
        }

        var result = await _aiVndBillingService.UpsertConfigAsync(dto, userId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("billing-summary")]
    [ProducesResponseType(typeof(ApiResponse<List<AIVndBillingTransactionSummaryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBillingSummary(
        [FromQuery] string? featureKey,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await _aiVndBillingTransactionRepository.SummarizeAsync(featureKey, status, fromDate, toDate, take, cancellationToken);
        return Ok(ApiResponse.Ok(items));
    }

    [HttpGet("billing-summary/daily")]
    [ProducesResponseType(typeof(ApiResponse<List<AIVndBillingDailySummaryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBillingDailySummary(
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default)
    {
        var items = await _aiVndBillingTransactionRepository.SummarizeDailyAsync(status, fromDate, toDate, take, cancellationToken);
        return Ok(ApiResponse.Ok(items));
    }

    private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("usage-logs")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<AIUsageLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsageLogs(
        [FromQuery] string? triggeredBy,
        [FromQuery] string? agentId,
        [FromQuery] string? provider,
        [FromQuery] string? model,
        [FromQuery] string? feature,
        [FromQuery] string? step,
        [FromQuery] bool? fallbackUsed,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _aiUsageLogRepository.ListAsync(
            triggeredBy,
            agentId,
            provider,
            model,
            feature,
            step,
            fallbackUsed,
            fromDate,
            toDate,
            page,
            limit);
        var agents = await _aiAgentAdminService.ListAgentsAsync(cancellationToken);
        var roleByAgentId = agents
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary(item => item.Id, item => item.AgentRole, StringComparer.OrdinalIgnoreCase);
        var triggeredByIds = items
            .Select(item => item.TriggeredBy)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var userLookupTasks = triggeredByIds.ToDictionary(
            id => id,
            id => _userRepository.GetByIdAsync(id),
            StringComparer.OrdinalIgnoreCase);
        await Task.WhenAll(userLookupTasks.Values);
        var userById = userLookupTasks
            .Where(item => item.Value.Result is not null)
            .ToDictionary(item => item.Key, item => item.Value.Result!, StringComparer.OrdinalIgnoreCase);

        var result = new PaginatedResult<AIUsageLogDto>
        {
            Items = items.Select(item =>
            {
                userById.TryGetValue(item.TriggeredBy, out var user);
                roleByAgentId.TryGetValue(item.AgentId, out var role);

                return new AIUsageLogDto
                {
                    Id = item.Id,
                    TriggeredBy = item.TriggeredBy,
                    TriggeredByName = user?.FullName,
                    TriggeredByEmail = user?.Email,
                    AgentId = item.AgentId,
                    AgentRole = role,
                    CreditsDeducted = item.CreditsDeducted,
                    TokensUsed = item.TokensUsed,
                    CostUsd = item.CostUsd,
                    CreatedAt = item.CreatedAt,
                    ParsedPayload = BuildParsedUsagePayload(item.PayloadData),
                    PayloadData = item.PayloadData
                };
            }).ToList(),
            Total = total,
            Page = page,
            Limit = limit,
            TotalPages = (total + limit - 1L) / Math.Max(1, limit)
        };

        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("usage-logs/filter-options")]
    [ProducesResponseType(typeof(ApiResponse<AIUsageLogFilterOptionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsageLogFilterOptions(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        var (triggeredByIds, features, providers, models) = await _aiUsageLogRepository.ListFilterOptionsAsync(fromDate, toDate);
        var userLookupTasks = triggeredByIds.ToDictionary(
            id => id,
            id => _userRepository.GetByIdAsync(id),
            StringComparer.OrdinalIgnoreCase);
        await Task.WhenAll(userLookupTasks.Values);

        var result = new AIUsageLogFilterOptionsDto
        {
            TriggeredBy = triggeredByIds
                .Select(id =>
                {
                    userLookupTasks.TryGetValue(id, out var userTask);
                    var user = userTask?.Result;
                    var label = !string.IsNullOrWhiteSpace(user?.FullName)
                        ? user!.FullName!
                        : !string.IsNullOrWhiteSpace(user?.Email)
                            ? user!.Email!
                            : id;

                    return new AIUsageLogFilterOptionDto
                    {
                        Value = id,
                        Label = label,
                        Email = user?.Email
                    };
                })
                .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Features = features,
            Providers = providers,
            Models = models
        };

        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("usage-summary")]
    [ProducesResponseType(typeof(ApiResponse<List<AIUsageSummaryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsageSummary(
        [FromQuery] string? agentId,
        [FromQuery] string? provider,
        [FromQuery] string? model,
        [FromQuery] string? feature,
        [FromQuery] string? step,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await _aiUsageLogRepository.SummarizeAsync(agentId, provider, model, feature, step, fromDate, toDate, take);
        var agents = await _aiAgentAdminService.ListAgentsAsync(cancellationToken);
        var roleByAgentId = agents
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary(item => item.Id, item => item.AgentRole, StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (roleByAgentId.TryGetValue(item.AgentId, out var role))
            {
                item.AgentRole = role;
            }

            item.AvgTokensPerCall = item.CallCount <= 0 ? 0 : Math.Round(item.TotalTokens / (decimal)item.CallCount, 2);
            item.AvgCostUsdPerCall = item.CallCount <= 0 ? 0 : Math.Round(item.TotalCostUsd / item.CallCount, 6);
            item.FallbackRate = item.CallCount <= 0 ? 0 : Math.Round(item.FallbackCallCount / (decimal)item.CallCount, 4);
        }

        return Ok(ApiResponse.Ok(items));
    }

    [HttpGet("usage-summary/artifacts")]
    [ProducesResponseType(typeof(ApiResponse<List<AIArtifactUsageSummaryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArtifactUsageSummary(
        [FromQuery] string? agentId,
        [FromQuery] string? feature,
        [FromQuery] string? step,
        [FromQuery] string? promptKey,
        [FromQuery] string? promptVersion,
        [FromQuery] string? policyKey,
        [FromQuery] string? policyVersion,
        [FromQuery] string? rubricKey,
        [FromQuery] string? rubricVersion,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await _aiUsageLogRepository.SummarizeByArtifactsAsync(
            agentId,
            feature,
            step,
            promptKey,
            promptVersion,
            policyKey,
            policyVersion,
            rubricKey,
            rubricVersion,
            fromDate,
            toDate,
            take);

        var agents = await _aiAgentAdminService.ListAgentsAsync(cancellationToken);
        var roleByAgentId = agents
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary(item => item.Id, item => item.AgentRole, StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (roleByAgentId.TryGetValue(item.AgentId, out var role))
            {
                item.AgentRole = role;
            }

            item.AvgTokensPerCall = item.CallCount <= 0 ? 0 : Math.Round(item.TotalTokens / (decimal)item.CallCount, 2);
            item.AvgCostUsdPerCall = item.CallCount <= 0 ? 0 : Math.Round(item.TotalCostUsd / item.CallCount, 6);
            item.FallbackRate = item.CallCount <= 0 ? 0 : Math.Round(item.FallbackCallCount / (decimal)item.CallCount, 4);
        }

        return Ok(ApiResponse.Ok(items));
    }

    [HttpGet("usage-summary/users")]
    [ProducesResponseType(typeof(ApiResponse<List<AIUserUsageSummaryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserUsageSummary(
        [FromQuery] string? triggeredBy,
        [FromQuery] string? provider,
        [FromQuery] string? model,
        [FromQuery] string? feature,
        [FromQuery] string? step,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var items = await _aiUsageLogRepository.SummarizeByUsersAsync(
            triggeredBy,
            provider,
            model,
            feature,
            step,
            fromDate,
            toDate,
            take);

        return Ok(ApiResponse.Ok(items));
    }

    [HttpGet("runtime-health")]
    [ProducesResponseType(typeof(ApiResponse<List<AIRuntimeHealthItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRuntimeHealth(CancellationToken cancellationToken)
    {
        var agents = await _aiAgentAdminService.ListAgentsAsync(cancellationToken);
        var items = agents
            .OrderBy(item => item.AgentRole)
            .Select(BuildRuntimeHealthItem)
            .ToList();

        return Ok(ApiResponse.Ok(items));
    }

    [HttpGet("mentor-feedbacks")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<AIMentorFeedbackAdminDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMentorFeedbacks(
        [FromQuery] string? submissionId,
        [FromQuery] string? verdict,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var (items, total) = await _aiMentorFeedbackRepository.ListAsync(submissionId, verdict, fromDate, toDate, page, limit);
        var result = new PaginatedResult<AIMentorFeedbackAdminDto>
        {
            Items = items.Select(item => new AIMentorFeedbackAdminDto
            {
                Id = item.Id,
                AgentId = item.AgentId,
                SubmissionId = item.SubmissionId,
                Date = item.Date,
                QuestionType = item.QuestionType,
                Verdict = item.Verdict,
                QualityScore = item.QualityScore,
                PerformanceSummary = item.PerformanceSummary,
                ErrorAnalysis = item.ErrorAnalysis,
                ImprovementSuggestions = item.ImprovementSuggestions,
                IssueCategories = item.IssueCategories,
                FeedbackText = item.FeedbackText,
                SuggestedComplexity = item.SuggestedComplexity,
                ModelName = item.ModelName,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            }).ToList(),
            Total = total,
            Page = page,
            Limit = limit,
            TotalPages = (total + limit - 1L) / Math.Max(1, limit)
        };

        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("provider-credentials")]
    [ProducesResponseType(typeof(ApiResponse<List<AIProviderCredentialRecordDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProviderCredentials(CancellationToken cancellationToken)
    {
        var result = await _aiProviderCredentialAdminService.ListAsync(cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("provider-credentials")]
    [ProducesResponseType(typeof(ApiResponse<AIProviderCredentialRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateProviderCredentials([FromBody] CreateAIProviderCredentialRequestDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetRequiredUserId(out var userId, out var unauthorized))
        {
            return unauthorized!;
        }

        var result = await _aiProviderCredentialAdminService.CreateAsync(dto, userId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("provider-credentials")]
    [ProducesResponseType(typeof(ApiResponse<AIProviderCredentialRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProviderCredentials([FromBody] UpdateAIProviderCredentialRequestDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetRequiredUserId(out var userId, out var unauthorized))
        {
            return unauthorized!;
        }

        var providerName = string.IsNullOrWhiteSpace(dto.Provider) ? string.Empty : dto.Provider;
        var result = await _aiProviderCredentialAdminService.UpdateAsync(providerName, dto, userId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpDelete("provider-credentials/{providerName}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteProviderCredentials(string providerName, CancellationToken cancellationToken)
    {
        if (!TryGetRequiredUserId(out var userId, out var unauthorized))
        {
            return unauthorized!;
        }

        await _aiProviderCredentialAdminService.DeleteAsync(providerName, userId, cancellationToken);
        return Ok(ApiResponse.Ok(new
        {
            deleted = true,
            provider = providerName
        }));
    }

    [HttpPut("provider-credentials/{providerName}")]
    [ProducesResponseType(typeof(ApiResponse<AIProviderCredentialRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProviderCredentialsByRoute(string providerName, [FromBody] UpdateAIProviderCredentialRequestDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetRequiredUserId(out var userId, out var unauthorized))
        {
            return unauthorized!;
        }

        var result = await _aiProviderCredentialAdminService.UpdateAsync(providerName, dto, userId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("provider-credentials/batch")]
    [ProducesResponseType(typeof(ApiResponse<AIProviderCredentialsConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertProviderCredentials([FromBody] AIProviderCredentialsUpsertRequestDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetRequiredUserId(out var userId, out var unauthorized))
        {
            return unauthorized!;
        }

        var result = await _aiProviderCredentialAdminService.UpsertAsync(dto, userId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("providers/reload")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult ReloadProviders()
    {
        _aiProviderSettingsResolver.Reload();
        return Ok(ApiResponse.Ok(new
        {
            reloaded = true,
            reloaded_at = DateTime.UtcNow
        }));
    }

    [HttpGet("providers/{providerName}/models")]
    [ProducesResponseType(typeof(ApiResponse<List<AIProviderModelOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProviderModels(string providerName, CancellationToken cancellationToken)
    {
        var result = await _aiProviderModelCatalogService.ListModelsAsync(providerName, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("agents")]
    [ProducesResponseType(typeof(ApiResponse<List<AIAgentAdminDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAgents(CancellationToken cancellationToken)
    {
        var result = await _aiAgentAdminService.ListAgentsAsync(cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("agents/{agentRole}")]
    [ProducesResponseType(typeof(ApiResponse<AIAgentAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAgent(string agentRole, CancellationToken cancellationToken)
    {
        var result = await _aiAgentAdminService.GetAgentAsync(agentRole, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail($"AI agent '{agentRole}' not found."));
        }

        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("agents/{agentRole}")]
    [ProducesResponseType(typeof(ApiResponse<AIAgentAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertAgent(string agentRole, [FromBody] UpsertAIAgentRequestDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetRequiredUserId(out var userId, out var unauthorized))
        {
            return unauthorized!;
        }

        var result = await _aiAgentAdminService.UpsertAgentAsync(agentRole, dto, userId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("smoke/gatekeeper")]
    [ProducesResponseType(typeof(ApiResponse<GatekeeperResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SmokeGatekeeper([FromBody] GatekeeperSmokeRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _aiGatekeeperService.ValidateAsync(dto.Content, new GatekeeperRequestContext
        {
            FileName = dto.FileName,
            SubjectHint = dto.SubjectHint,
            Language = dto.Language,
            RuntimeOverride = dto.RuntimeOverride
        }, cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("smoke/extracted")]
    [ProducesResponseType(typeof(ApiResponse<ExtractedContentResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SmokeExtractedContent([FromBody] ExtractedContentSmokeRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _aiExtractedContentService.NormalizeAsync(new ExtractionResultDto
        {
            FileName = dto.FileName,
            FileType = Path.GetExtension(dto.FileName),
            RawText = dto.RawText,
            SourceType = dto.SourceType,
            IsVisionRecommended = dto.IsVisionRecommended,
            ParserName = dto.ParserName,
            EstimatedPageCount = dto.EstimatedPageCount,
            DetectedImageReferences = dto.DetectedImageReferences,
            ExtractedImages = dto.ExtractedImages,
            EmbeddedImageCount = dto.EmbeddedImageCount,
            Warnings = dto.Warnings
        }, dto.RuntimeOverride, cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("smoke/embedding")]
    [ProducesResponseType(typeof(ApiResponse<EmbeddingTaggingRunResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SmokeEmbedding([FromBody] EmbeddingSmokeRequestDto dto, CancellationToken cancellationToken)
    {
        var documentId = string.IsNullOrWhiteSpace(dto.DocumentId) ? $"smoke-doc-{Guid.NewGuid():N}"[..24] : dto.DocumentId;
        var courseId = dto.CourseId ?? "smoke-course";
        var userId = UserId ?? "admin-smoke";

        var result = await _aiEmbeddingTaggingService.CreateChunksAsync(
            documentId,
            courseId,
            userId,
            dto.Content,
            dto.SourceType,
            null,
            null,
            dto.EmbeddingOverride,
            dto.TaggingOverride,
            cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("smoke/retrieval-plan")]
    [ProducesResponseType(typeof(ApiResponse<RetrievalPlanResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SmokeRetrievalPlan([FromBody] RetrievalSmokeRequestDto dto, CancellationToken cancellationToken)
    {
        dto.UserId = string.IsNullOrWhiteSpace(dto.UserId) ? (UserId ?? "admin-smoke") : dto.UserId;
        var result = await _retrievalPlannerService.PlanAsync(dto, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("smoke/question-generation-review")]
    [ProducesResponseType(typeof(ApiResponse<GenerationReviewResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SmokeQuestionGenerationReview([FromBody] GenerationReviewRequestDto dto, CancellationToken cancellationToken)
    {
        dto.UserId = string.IsNullOrWhiteSpace(dto.UserId) ? (UserId ?? "admin-smoke") : dto.UserId;
        dto.PersistQuestions = false;
        dto.SuppressPersistenceSideEffects = true;
        var result = await _questionGenerationReviewService.RunAsync(dto, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("smoke/code-mentor")]
    [ProducesResponseType(typeof(ApiResponse<CodeMentorResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SmokeCodeMentor([FromBody] CodeMentorSmokeRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _codeMentorService.RunSmokeAsync(dto, cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    private AIRuntimeHealthItemDto BuildRuntimeHealthItem(AIAgentAdminDto agent)
    {
        var primary = _aiProviderSettingsResolver.GetProvider(agent.Provider);
        var primaryEnabled = primary.Enabled;
        var primaryHasApiKey = !string.IsNullOrWhiteSpace(primary.ApiKey);
        var primaryReady = agent.IsEnabled && primaryEnabled && primaryHasApiKey;

        var fallbackConfigured = !string.IsNullOrWhiteSpace(agent.FallbackProvider) && !string.IsNullOrWhiteSpace(agent.FallbackModelName);
        var fallbackEnabled = false;
        var fallbackHasApiKey = false;
        var fallbackReady = false;

        if (fallbackConfigured)
        {
            var fallback = _aiProviderSettingsResolver.GetProvider(agent.FallbackProvider!);
            fallbackEnabled = fallback.Enabled;
            fallbackHasApiKey = !string.IsNullOrWhiteSpace(fallback.ApiKey);
            fallbackReady = fallbackEnabled && fallbackHasApiKey;
        }

        var warnings = new List<string>();
        if (!agent.IsEnabled)
        {
            warnings.Add("Agent is disabled.");
        }

        if (agent.IsEnabled && !primaryEnabled)
        {
            warnings.Add("Primary provider is disabled.");
        }

        if (agent.IsEnabled && primaryEnabled && !primaryHasApiKey)
        {
            warnings.Add("Primary provider API key is missing.");
        }

        if (fallbackConfigured && !fallbackEnabled)
        {
            warnings.Add("Fallback provider is disabled.");
        }

        if (fallbackConfigured && fallbackEnabled && !fallbackHasApiKey)
        {
            warnings.Add("Fallback provider API key is missing.");
        }

        return new AIRuntimeHealthItemDto
        {
            AgentId = agent.Id,
            AgentRole = agent.AgentRole,
            AgentEnabled = agent.IsEnabled,
            Provider = agent.Provider,
            ModelName = agent.ModelName,
            PrimaryProviderEnabled = primaryEnabled,
            PrimaryProviderHasApiKey = primaryHasApiKey,
            PrimaryRuntimeReady = primaryReady,
            FallbackProvider = agent.FallbackProvider,
            FallbackModelName = agent.FallbackModelName,
            FallbackConfigured = fallbackConfigured,
            FallbackProviderEnabled = fallbackEnabled,
            FallbackProviderHasApiKey = fallbackHasApiKey,
            FallbackRuntimeReady = fallbackReady,
            RuntimeStatus = !agent.IsEnabled
                ? "disabled"
                : primaryReady
                    ? fallbackConfigured
                        ? fallbackReady ? "ready_with_fallback" : "ready_primary_only_fallback_not_ready"
                        : "ready_primary_only"
                    : "primary_not_ready",
            Warnings = warnings,
            UpdatedAt = agent.UpdatedAt
        };
    }

    private static AIUsageLogParsedPayloadDto BuildParsedUsagePayload(object? payloadData)
    {
        var payload = ToDictionary(payloadData);

        return new AIUsageLogParsedPayloadDto
        {
            Feature = ReadString(payload, "feature"),
            Step = ReadString(payload, "step"),
            Provider = ReadString(payload, "provider"),
            ConfiguredModel = ReadString(payload, "configured_model"),
            EffectiveModel = ReadString(payload, "effective_model") ?? ReadString(payload, "model"),
            NormalizedModelKey = ReadString(payload, "normalized_model_key"),
            UsageSource = ReadString(payload, "usage_source"),
            CostSource = ReadString(payload, "cost_source"),
            FallbackUsed = ReadBool(payload, "fallback_used"),
            FallbackFromProvider = ReadString(payload, "fallback_from_provider"),
            FallbackFromModel = ReadString(payload, "fallback_from_model"),
            FallbackReasonCode = ReadString(payload, "fallback_reason_code"),
            PromptKey = ReadString(payload, "prompt_key"),
            PromptVersion = ReadString(payload, "prompt_version"),
            PolicyKey = ReadString(payload, "policy_key"),
            PolicyVersion = ReadString(payload, "policy_version"),
            RubricKey = ReadString(payload, "rubric_key"),
            RubricVersion = ReadString(payload, "rubric_version"),
            CourseId = ReadString(payload, "course_id"),
            DocumentId = ReadString(payload, "document_id"),
            SubmissionId = ReadString(payload, "submission_id"),
            ContextPackId = ReadString(payload, "context_pack_id"),
            QuestionType = ReadString(payload, "question_type"),
            Language = ReadString(payload, "language"),
            InputTokens = ReadInt(payload, "input_tokens"),
            OutputTokens = ReadInt(payload, "output_tokens"),
            TotalTokens = ReadInt(payload, "total_tokens") ?? ReadInt(payload, "tokens_used"),
            ChunkCount = ReadInt(payload, "chunk_count"),
            FallbackCalls = ReadInt(payload, "fallback_calls")
        };
    }

    private static IDictionary<string, object?> ToDictionary(object? payload)
    {
        if (payload is IDictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        if (payload is BsonDocument bsonDocument)
        {
            return bsonDocument.Elements.ToDictionary(
                item => item.Name,
                item => ConvertBsonValue(item.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static object? ConvertBsonValue(BsonValue value)
    {
        if (value.IsBsonNull)
        {
            return null;
        }

        return value.BsonType switch
        {
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Double => value.AsDouble,
            BsonType.Decimal128 => (double)value.AsDecimal128,
            BsonType.Boolean => value.AsBoolean,
            BsonType.String => value.AsString,
            BsonType.Document => value.AsBsonDocument.Elements.ToDictionary(
                item => item.Name,
                item => ConvertBsonValue(item.Value),
                StringComparer.OrdinalIgnoreCase),
            BsonType.Array => value.AsBsonArray.Select(ConvertBsonValue).ToList(),
            _ => value.ToString()
        };
    }

    private static string? ReadString(IDictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static int? ReadInt(IDictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is int intValue) return intValue;
        if (value is long longValue && longValue <= int.MaxValue && longValue >= int.MinValue) return (int)longValue;
        if (value is decimal decimalValue && decimalValue <= int.MaxValue && decimalValue >= int.MinValue) return (int)decimalValue;
        if (int.TryParse(value.ToString(), out var parsed)) return parsed;
        return null;
    }

    private static bool? ReadBool(IDictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is bool boolValue) return boolValue;
        if (bool.TryParse(value.ToString(), out var parsed)) return parsed;
        return null;
    }

    private static double? ReadDouble(IDictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is double doubleValue) return doubleValue;
        if (value is decimal decimalValue) return (double)decimalValue;
        if (value is int intValue) return intValue;
        if (value is long longValue) return longValue;
        if (double.TryParse(value.ToString(), out var parsed)) return parsed;
        return null;
    }

    private bool TryGetRequiredUserId(out string userId, out IActionResult? unauthorized)
    {
        userId = UserId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userId))
        {
            unauthorized = Unauthorized(ApiResponse.Fail("Unauthorized."));
            return false;
        }

        unauthorized = null;
        return true;
    }
}
