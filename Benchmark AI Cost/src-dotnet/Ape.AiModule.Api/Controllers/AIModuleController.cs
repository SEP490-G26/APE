using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ape.AiModule.Api.Controllers;

[ApiController]
[Route("api/ai-module")]
public sealed class AIModuleController : ControllerBase
{
    private readonly IAiPipelineOrchestrator _orchestrator;
    private readonly IAiRunHistoryService _runHistoryService;
    private readonly IBenchmarkSessionService _benchmarkSessionService;
    private readonly IGatekeeperPolicyService _gatekeeperPolicyService;
    private readonly IExtractedContentPolicyService _extractedContentPolicyService;
    private readonly IEmbeddingTaggingPolicyService _embeddingTaggingPolicyService;
    private readonly ICodeMentorPolicyService _codeMentorPolicyService;
    private readonly IRubricCatalogService _rubricCatalogService;
    private readonly IGroundTruthCatalogService _groundTruthCatalogService;
    private readonly PrototypeFileIngestService _prototypeFileIngestService;

    public AIModuleController(
        IAiPipelineOrchestrator orchestrator,
        IAiRunHistoryService runHistoryService,
        IBenchmarkSessionService benchmarkSessionService,
        IGatekeeperPolicyService gatekeeperPolicyService,
        IExtractedContentPolicyService extractedContentPolicyService,
        IEmbeddingTaggingPolicyService embeddingTaggingPolicyService,
        ICodeMentorPolicyService codeMentorPolicyService,
        IRubricCatalogService rubricCatalogService,
        IGroundTruthCatalogService groundTruthCatalogService,
        PrototypeFileIngestService prototypeFileIngestService)
    {
        _orchestrator = orchestrator;
        _runHistoryService = runHistoryService;
        _benchmarkSessionService = benchmarkSessionService;
        _gatekeeperPolicyService = gatekeeperPolicyService;
        _extractedContentPolicyService = extractedContentPolicyService;
        _embeddingTaggingPolicyService = embeddingTaggingPolicyService;
        _codeMentorPolicyService = codeMentorPolicyService;
        _rubricCatalogService = rubricCatalogService;
        _groundTruthCatalogService = groundTruthCatalogService;
        _prototypeFileIngestService = prototypeFileIngestService;
    }

    [HttpPost("uploads/ingest")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> IngestUploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file was uploaded." });
        }

        return Ok(await _prototypeFileIngestService.IngestAsync(file, cancellationToken));
    }

    [HttpPost("gatekeeper")]
    public async Task<IActionResult> RunGatekeeperAsync([FromBody] GatekeeperRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("gatekeeper", "gatekeeper", request, () => _orchestrator.RunGatekeeperAsync(request, cancellationToken), cancellationToken));

    [HttpPost("gatekeeper/debug")]
    public async Task<IActionResult> RunGatekeeperDebugAsync([FromBody] GatekeeperRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("gatekeeper", "gatekeeper-debug", request, () => _orchestrator.RunGatekeeperDebugAsync(request, cancellationToken), cancellationToken));

    [HttpGet("gatekeeper/policy")]
    public async Task<IActionResult> GetGatekeeperPolicyAsync(CancellationToken cancellationToken)
        => Ok(await _gatekeeperPolicyService.GetPolicyAsync(cancellationToken));

    [HttpGet("gatekeeper/rubric")]
    public async Task<IActionResult> GetGatekeeperRubricAsync(CancellationToken cancellationToken)
        => Ok(await _rubricCatalogService.GetGatekeeperRubricAsync(cancellationToken));

    [HttpGet("gatekeeper/ground-truth")]
    public async Task<IActionResult> GetGatekeeperGroundTruthAsync(CancellationToken cancellationToken)
        => Ok(await _groundTruthCatalogService.GetGatekeeperGroundTruthAsync(cancellationToken));

    [HttpPost("extracted-content")]
    public async Task<IActionResult> RunExtractedContentAsync([FromBody] ExtractedContentRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("extracted-content", "extracted-content", request, () => _orchestrator.RunExtractedContentAsync(request, cancellationToken), cancellationToken));

    [HttpPost("extracted-content/debug")]
    public async Task<IActionResult> RunExtractedContentDebugAsync([FromBody] ExtractedContentRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("extracted-content", "extracted-content-debug", request, () => _orchestrator.RunExtractedContentDebugAsync(request, cancellationToken), cancellationToken));

    [HttpGet("extracted-content/policy")]
    public async Task<IActionResult> GetExtractedContentPolicyAsync(CancellationToken cancellationToken)
        => Ok(await _extractedContentPolicyService.GetPolicyAsync(cancellationToken));

    [HttpGet("extracted-content/rubric")]
    public async Task<IActionResult> GetExtractedContentRubricAsync(CancellationToken cancellationToken)
        => Ok(await _rubricCatalogService.GetExtractedContentRubricAsync(cancellationToken));

    [HttpGet("extracted-content/ground-truth")]
    public async Task<IActionResult> GetExtractedContentGroundTruthAsync(CancellationToken cancellationToken)
        => Ok(await _groundTruthCatalogService.GetExtractedContentGroundTruthAsync(cancellationToken));

    [HttpPost("embedding-tagging")]
    public async Task<IActionResult> RunEmbeddingTaggingAsync([FromBody] EmbeddingTaggingRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("embedding-tagging", "embedding-tagging", request, () => _orchestrator.RunEmbeddingTaggingAsync(request, cancellationToken), cancellationToken));

    [HttpPost("embedding-tagging/debug")]
    public async Task<IActionResult> RunEmbeddingTaggingDebugAsync([FromBody] EmbeddingTaggingRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("embedding-tagging", "embedding-tagging-debug", request, () => _orchestrator.RunEmbeddingTaggingDebugAsync(request, cancellationToken), cancellationToken));

    [HttpGet("embedding-tagging/policy")]
    public async Task<IActionResult> GetEmbeddingTaggingPolicyAsync(CancellationToken cancellationToken)
        => Ok(await _embeddingTaggingPolicyService.GetPolicyAsync(cancellationToken));

    [HttpGet("embedding-tagging/rubric")]
    public async Task<IActionResult> GetEmbeddingTaggingRubricAsync(CancellationToken cancellationToken)
        => Ok(await _rubricCatalogService.GetEmbeddingTaggingRubricAsync(cancellationToken));

    [HttpGet("embedding-tagging/ground-truth")]
    public async Task<IActionResult> GetEmbeddingTaggingGroundTruthAsync(CancellationToken cancellationToken)
        => Ok(await _groundTruthCatalogService.GetEmbeddingTaggingGroundTruthAsync(cancellationToken));

    [HttpPost("ingestion")]
    public async Task<IActionResult> RunIngestionAsync([FromBody] IngestionRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("ingestion", "ingestion", request, () => _orchestrator.RunIngestionAsync(request, cancellationToken), cancellationToken));

    [HttpPost("question-generation")]
    public async Task<IActionResult> RunQuestionGenerationAsync([FromBody] QuestionGenerationRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("question-generation", "question-generation", request, () => _orchestrator.RunQuestionGenerationAsync(request, cancellationToken), cancellationToken));

    [HttpPost("question-generation/debug")]
    public async Task<IActionResult> RunQuestionGenerationDebugAsync([FromBody] QuestionGenerationRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("question-generation", "question-generation-debug", request, () => _orchestrator.RunQuestionGenerationAsync(request, cancellationToken), cancellationToken));

    [HttpGet("question-generation/rubric")]
    public async Task<IActionResult> GetQuestionGenerationRubricAsync([FromQuery] string subject, [FromQuery] string questionType, CancellationToken cancellationToken)
        => Ok(await _rubricCatalogService.GetQuestionReviewRubricAsync(subject, questionType, cancellationToken));

    [HttpGet("question-generation/ground-truth")]
    public async Task<IActionResult> GetQuestionGenerationGroundTruthAsync([FromQuery] string subject, [FromQuery] string questionType, CancellationToken cancellationToken)
        => Ok(await _groundTruthCatalogService.GetQuestionGenerationGroundTruthAsync(subject, questionType, cancellationToken));

    [HttpPost("question-generation/regression")]
    public async Task<IActionResult> RunQuestionGenerationRegressionAsync([FromBody] QuestionRegressionRunRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("question-generation", "question-generation-regression", request, () => _orchestrator.RunQuestionRegressionAsync(request, cancellationToken), cancellationToken));

    [HttpPost("question-review")]
    public async Task<IActionResult> RunQuestionReviewAsync([FromBody] QuestionReviewRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("question-review", "question-review", request, () => _orchestrator.RunQuestionReviewAsync(request, cancellationToken), cancellationToken));

    [HttpPost("generation-review")]
    public async Task<IActionResult> RunGenerationReviewAsync([FromBody] GenerationReviewRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("generation-review", "generation-review", request, () => _orchestrator.RunGenerationReviewAsync(request, cancellationToken), cancellationToken));

    [HttpPost("generation-review/debug")]
    public async Task<IActionResult> RunGenerationReviewDebugAsync([FromBody] GenerationReviewRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("generation-review", "generation-review-debug", request, () => _orchestrator.RunGenerationReviewAsync(request, cancellationToken), cancellationToken));

    [HttpPost("generation-review/export")]
    public async Task<IActionResult> RunGenerationReviewExportAsync([FromBody] GenerationReviewRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("generation-review", "generation-review-export", request, () => _orchestrator.RunGenerationReviewExportAsync(request, cancellationToken), cancellationToken));

    [HttpPost("code-mentor")]
    public async Task<IActionResult> RunCodeMentorAsync([FromBody] CodeMentorRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("code-mentor", "code-mentor", request, () => _orchestrator.RunCodeMentorAsync(request, cancellationToken), cancellationToken));

    [HttpPost("code-mentor/debug")]
    public async Task<IActionResult> RunCodeMentorDebugAsync([FromBody] CodeMentorRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("code-mentor", "code-mentor-debug", request, () => _orchestrator.RunCodeMentorDebugAsync(request, cancellationToken), cancellationToken));

    [HttpGet("code-mentor/policy")]
    public async Task<IActionResult> GetCodeMentorPolicyAsync(CancellationToken cancellationToken)
        => Ok(await _codeMentorPolicyService.GetPolicyAsync(cancellationToken));

    [HttpGet("code-mentor/rubric")]
    public async Task<IActionResult> GetCodeMentorRubricAsync(CancellationToken cancellationToken)
        => Ok(await _rubricCatalogService.GetCodeMentorRubricAsync(cancellationToken));

    [HttpGet("code-mentor/ground-truth")]
    public async Task<IActionResult> GetCodeMentorGroundTruthAsync(CancellationToken cancellationToken)
        => Ok(await _groundTruthCatalogService.GetCodeMentorGroundTruthAsync(cancellationToken));

    [HttpPost("full-pipeline")]
    public async Task<IActionResult> RunFullPipelineAsync([FromBody] FullPipelineRequest request, CancellationToken cancellationToken)
        => Ok(await ExecuteWithHistoryAsync("full-pipeline", "full-pipeline", request, () => _orchestrator.RunFullPipelineAsync(request, cancellationToken), cancellationToken));

    [HttpGet("history")]
    public async Task<IActionResult> GetHistoryAsync([FromQuery] string? functionName, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
        => Ok(await _runHistoryService.ListAsync(functionName, take, cancellationToken));

    [HttpGet("history/{functionName}")]
    public async Task<IActionResult> GetHistoryByFunctionAsync(string functionName, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
        => Ok(await _runHistoryService.ListAsync(functionName, take, cancellationToken));

    [HttpGet("history/{functionName}/{runId}")]
    public async Task<IActionResult> GetHistoryDetailAsync(string functionName, string runId, CancellationToken cancellationToken = default)
        => Ok(await _runHistoryService.GetAsync(functionName, runId, cancellationToken));

    [HttpPost("benchmark-sessions")]
    public async Task<IActionResult> SaveBenchmarkSessionAsync([FromBody] BenchmarkSessionSaveRequest request, CancellationToken cancellationToken)
        => Ok(await _benchmarkSessionService.SaveAsync(request, cancellationToken));

    [HttpGet("benchmark-sessions")]
    public async Task<IActionResult> GetBenchmarkSessionsAsync([FromQuery] int take = 20, CancellationToken cancellationToken = default)
        => Ok(await _benchmarkSessionService.ListAsync(take, cancellationToken));

    [HttpGet("benchmark-sessions/{sessionId}")]
    public async Task<IActionResult> GetBenchmarkSessionDetailAsync(string sessionId, CancellationToken cancellationToken = default)
        => Ok(await _benchmarkSessionService.GetAsync(sessionId, cancellationToken));

    private async Task<T> ExecuteWithHistoryAsync<T>(
        string functionName,
        string routeKey,
        object request,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        var result = await action();
        await _runHistoryService.SaveAsync(functionName, routeKey, request, result!, cancellationToken);
        return result;
    }
}
