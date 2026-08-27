using System.Linq;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Models;
using Application.Options;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

public sealed class PECodeRunService
    : IPECodeRunService
{
    private readonly IPEQuestionRepository _peQuestionRepository;
    private readonly ICodeExecutionClient _codeExecutionClient;
    private readonly IExecutionFeedbackDiagnosticParser _diagnosticParser;
    private readonly PECodeRunOptions _options;
    private readonly ILogger<PECodeRunService> _logger;

    public PECodeRunService(
        IPEQuestionRepository peQuestionRepository,
        ICodeExecutionClient codeExecutionClient,
        IExecutionFeedbackDiagnosticParser diagnosticParser,
        IOptions<PECodeRunOptions> options,
        ILogger<PECodeRunService> logger)
    {
        _peQuestionRepository = peQuestionRepository;
        _codeExecutionClient = codeExecutionClient;
        _diagnosticParser = diagnosticParser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PECodeRunServiceResult> RunAsync(
        string studentId,
        PECodeRunInputDto request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.QuestionId))
        {
            return PECodeRunServiceResult.Fail(
                400,
                "QuestionId is required.");
        }

        if (request.Files is null || request.Files.Count == 0)
        {
            return PECodeRunServiceResult.Fail(
                400,
                "At least one source-code file is required.");
        }

        var question = await _peQuestionRepository.GetByIdAsync(
            request.QuestionId,
            cancellationToken);

        if (question is null)
        {
            return PECodeRunServiceResult.Fail(
                404,
                "Programming question not found.");
        }

        if (!string.Equals(
                question.Status,
                "Active",
                StringComparison.OrdinalIgnoreCase))
        {
            return PECodeRunServiceResult.Fail(
                400,
                "Programming question is not active.");
        }

        List<CodeFile> sourceFiles;

        try
        {
            sourceFiles = request.Files
                .Select(file => CodeFile.Create(
                    file.Filename,
                    file.Content))
                .ToList();
        }
        catch (Exception exception)
            when (exception is ArgumentException or DomainRuleException)
        {
            return PECodeRunServiceResult.Fail(
                400,
                exception.Message);
        }

        int languageId;

        try
        {
            languageId = question.ResolveLanguageId(null);
        }
        catch (DomainRuleException exception)
        {
            return PECodeRunServiceResult.Fail(
                400,
                exception.Message);
        }

        var publicTestCases = question.TestCases
            .Where(testCase => !testCase.IsHidden)
            .ToList();

        var baselineTestCase = question.TestCases.FirstOrDefault()
            ?? new TestCase
            {
                TimeLimitMs = 2000,
                MemoryLimitKb = 256000
            };

        var hasCustomInput = !string.IsNullOrWhiteSpace(request.Stdin);
        var executionCases = new List<CodeExecutionCaseRequest>();

        if (hasCustomInput)
        {
            executionCases.Add(new CodeExecutionCaseRequest(
                0,
                request.Stdin ?? string.Empty,
                baselineTestCase.TimeLimitMs,
                baselineTestCase.MemoryLimitKb));
        }
        else
        {
            for (var index = 0; index < publicTestCases.Count; index++)
            {
                var publicCase = publicTestCases[index];
                executionCases.Add(new CodeExecutionCaseRequest(
                    index,
                    publicCase.Input ?? string.Empty,
                    publicCase.TimeLimitMs,
                    publicCase.MemoryLimitKb));
            }

            if (executionCases.Count == 0)
            {
                executionCases.Add(new CodeExecutionCaseRequest(
                    0,
                    string.Empty,
                    baselineTestCase.TimeLimitMs,
                    baselineTestCase.MemoryLimitKb));
            }
        }

        CodeExecutionBatchReceipt receipt;

        try
        {
            var batchRequest = CodeExecutionBatchRequest.Create(
                languageId,
                sourceFiles,
                executionCases);

            receipt = await _codeExecutionClient.CreateBatchAsync(
                batchRequest,
                cancellationToken);
        }
        catch (DomainRuleException exception)
        {
            _logger.LogWarning(
                exception,
                "PE run validation failed. QuestionId={QuestionId}, UserId={UserId}.",
                request.QuestionId,
                studentId);

            return PECodeRunServiceResult.Fail(
                400,
                exception.Message);
        }
        catch (CodeExecutionClientException exception)
        {
            _logger.LogWarning(
                exception,
                "PE run could not reach Judge0. QuestionId={QuestionId}, UserId={UserId}.",
                request.QuestionId,
                studentId);

            return PECodeRunServiceResult.Fail(
                503,
                "Unable to connect to the Judge0 grading service. Please try again later.");
        }

        var executionTokens = receipt.Tokens
            .OrderBy(item => item.CaseIndex)
            .Where(item => !string.IsNullOrWhiteSpace(item.ProviderToken))
            .ToList();

        var tokens = executionTokens
            .Select(item => item.ProviderToken)
            .ToArray();

        if (tokens.Length == 0)
        {
            return PECodeRunServiceResult.Fail(
                400,
                "Judge0 did not return any execution token.");
        }

        IReadOnlyList<CodeExecutionResult>? latestResults = null;

        for (var attempt = 0;
             attempt < _options.MaximumPollingAttempts;
             attempt++)
        {
            try
            {
                latestResults = await _codeExecutionClient.GetBatchResultsAsync(
                    tokens,
                    cancellationToken);
            }
            catch (CodeExecutionClientException exception)
            {
                _logger.LogWarning(
                    exception,
                    "PE run polling failed. QuestionId={QuestionId}, UserId={UserId}, Attempt={Attempt}.",
                    request.QuestionId,
                    studentId,
                    attempt + 1);

                return PECodeRunServiceResult.Fail(
                    503,
                    "Unable to retrieve the code execution result from Judge0. Please try again later.");
            }

            if (latestResults.Count > 0 &&
                latestResults.All(result => result.IsTerminal))
            {
                break;
            }

            if (_options.PollingDelayMilliseconds > 0)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        _options.PollingDelayMilliseconds),
                    cancellationToken);
            }
        }

        if (latestResults is null ||
            latestResults.Count == 0 ||
            latestResults.Any(result => !result.IsTerminal))
        {
            return PECodeRunServiceResult.Fail(
                504,
                "The code was submitted to Judge0, but the result is not ready yet. Please try again.");
        }

        if (latestResults.Any(result =>
                result.State is CodeExecutionState.ProviderInternalError or
                CodeExecutionState.Unknown))
        {
            return PECodeRunServiceResult.Fail(
                503,
                "The Judge0 grading service returned an internal execution error. Please try again later.");
        }

        var caseIndexByToken = executionTokens.ToDictionary(
            item => item.ProviderToken,
            item => item.CaseIndex,
            StringComparer.Ordinal);

        var orderedResults = latestResults
            .Select(result =>
            {
                if (caseIndexByToken.TryGetValue(
                        result.ProviderToken,
                        out var caseIndex))
                {
                    return result with
                    {
                        CaseIndex = caseIndex
                    };
                }

                return result;
            })
            .OrderBy(result => result.CaseIndex)
            .ToList();

        var payload = hasCustomInput
            ? BuildCustomRunPayload(
                orderedResults,
                request,
                sourceFiles)
            : BuildPublicRunPayload(
                orderedResults,
                publicTestCases,
                sourceFiles);

        return PECodeRunServiceResult.Ok(payload);
    }

    private PECodeRunResultDto BuildPublicRunPayload(
        IReadOnlyList<CodeExecutionResult> orderedResults,
        IReadOnlyList<TestCase> publicTestCases,
        IReadOnlyCollection<CodeFile> sourceFiles)
    {
        if (publicTestCases.Count == 0)
        {
            var result = orderedResults.First();
            var customRunResult = BuildCustomCaseResult(
                1,
                "Run result",
                string.Empty,
                result,
                sourceFiles);

            return new PECodeRunResultDto
            {
                Status = customRunResult.Status,
                Stdout = customRunResult.ActualOutput,
                Stderr = customRunResult.Stderr,
                CompileOutput = customRunResult.CompileOutput,
                RuntimeMs = customRunResult.RuntimeMs,
                MemoryKb = customRunResult.MemoryKb,
                PassedSampleCases = 0,
                TotalSampleCases = 0,
                ExecutionStatus = customRunResult.ExecutionStatus,
                IsJudged = false,
                Diagnostics = customRunResult.Diagnostics,
                CustomRunResult = customRunResult
            };
        }

        var sampleResults = new List<PECodeRunCaseResultDto>();
        var passedSampleCases = 0;
        IReadOnlyCollection<ExecutionFeedbackDiagnosticDto> topLevelDiagnostics =
            Array.Empty<ExecutionFeedbackDiagnosticDto>();

        for (var index = 0; index < publicTestCases.Count; index++)
        {
            var executionResult = orderedResults
                .FirstOrDefault(item => item.CaseIndex == index);

            if (executionResult is null)
            {
                continue;
            }

            var publicCase = publicTestCases[index];
            var verdict = ExecutionFeedbackMapper.ResolveVerdict(
                executionResult,
                publicCase.ExpectedOutput);
            var executionStatus =
                ExecutionFeedbackMapper.MapOfficialExecutionStatus(
                    verdict);

            if (executionStatus == ExecutionFeedbackStatus.Passed)
            {
                passedSampleCases++;
            }

            if (verdict == SubmissionVerdict.CompilationError &&
                topLevelDiagnostics.Count == 0)
            {
                topLevelDiagnostics =
                    _diagnosticParser.ParseCompilationDiagnostics(
                        executionResult.CompileOutput,
                        sourceFiles,
                        includeRawDetails: true);
            }

            sampleResults.Add(
                new PECodeRunCaseResultDto
                {
                    Index = index + 1,
                    Number = index + 1,
                    Label = $"Sample {index + 1}",
                    Status = ExecutionFeedbackMapper.MapVerdictStatus(
                        verdict),
                    Passed = verdict == SubmissionVerdict.Accepted,
                    ExecutionStatus = executionStatus,
                    IsJudged = true,
                    IsHidden = false,
                    Input = publicCase.Input ?? string.Empty,
                    ExpectedOutput = publicCase.ExpectedOutput ?? string.Empty,
                    ActualOutput = executionResult.StandardOutput ?? string.Empty,
                    Stderr = ExecutionFeedbackSanitizer.SanitizeTechnicalText(
                        executionResult.StandardError ??
                        executionResult.Message,
                        sourceFiles) ?? string.Empty,
                    CompileOutput =
                        ExecutionFeedbackSanitizer.SanitizeTechnicalText(
                            executionResult.CompileOutput,
                            sourceFiles) ?? string.Empty,
                    RuntimeMs =
                        ExecutionFeedbackMapper.ConvertRuntimeToMilliseconds(
                            executionResult.TimeSeconds),
                    MemoryKb = Math.Max(
                        executionResult.MemoryKb ?? 0,
                        0),
                    Diagnostics =
                        BuildOfficialCaseDiagnostics(
                            verdict,
                            executionResult,
                            sourceFiles,
                            includeRawDetails: true)
                });
        }

        var headlineResult = orderedResults.FirstOrDefault();

        var overallStatus = sampleResults.All(
            item => item.ExecutionStatus ==
                    ExecutionFeedbackStatus.NotExecuted)
            ? "Compilation Error"
            : $"{passedSampleCases}/{sampleResults.Count} sample cases passed";

        return new PECodeRunResultDto
        {
            Status = overallStatus,
            Stdout = headlineResult?.StandardOutput ?? string.Empty,
            Stderr = ExecutionFeedbackSanitizer.SanitizeTechnicalText(
                headlineResult?.StandardError ??
                headlineResult?.Message,
                sourceFiles) ?? string.Empty,
            CompileOutput = ExecutionFeedbackSanitizer.SanitizeTechnicalText(
                headlineResult?.CompileOutput,
                sourceFiles) ?? string.Empty,
            RuntimeMs =
                ExecutionFeedbackMapper.ConvertRuntimeToMilliseconds(
                    headlineResult?.TimeSeconds),
            MemoryKb = Math.Max(
                headlineResult?.MemoryKb ?? 0,
                0),
            PassedSampleCases = passedSampleCases,
            TotalSampleCases = sampleResults.Count,
            ExecutionStatus = sampleResults.All(
                item => item.ExecutionStatus ==
                        ExecutionFeedbackStatus.Passed)
                ? ExecutionFeedbackStatus.Passed
                : sampleResults.Any(
                    item => item.ExecutionStatus ==
                            ExecutionFeedbackStatus.Failed)
                    ? ExecutionFeedbackStatus.Failed
                    : ExecutionFeedbackStatus.NotExecuted,
            IsJudged = true,
            Diagnostics = topLevelDiagnostics,
            SampleResults = sampleResults
        };
    }

    private PECodeRunResultDto BuildCustomRunPayload(
        IReadOnlyList<CodeExecutionResult> orderedResults,
        PECodeRunInputDto request,
        IReadOnlyCollection<CodeFile> sourceFiles)
    {
        var result = orderedResults.First(item => item.CaseIndex == 0);
        var customRunResult = BuildCustomCaseResult(
            1,
            "Custom input",
            request.Stdin ?? string.Empty,
            result,
            sourceFiles);

        return new PECodeRunResultDto
        {
            Status = customRunResult.Status,
            Stdout = customRunResult.ActualOutput,
            Stderr = customRunResult.Stderr,
            CompileOutput = customRunResult.CompileOutput,
            RuntimeMs = customRunResult.RuntimeMs,
            MemoryKb = customRunResult.MemoryKb,
            PassedSampleCases = 0,
            TotalSampleCases = 0,
            ExecutionStatus = customRunResult.ExecutionStatus,
            IsJudged = false,
            Diagnostics = customRunResult.Diagnostics,
            CustomRunResult = customRunResult
        };
    }

    private PECodeRunCaseResultDto BuildCustomCaseResult(
        int number,
        string label,
        string input,
        CodeExecutionResult result,
        IReadOnlyCollection<CodeFile> sourceFiles)
    {
        var diagnostics = result.State == CodeExecutionState.CompilationFailed
            ? _diagnosticParser.ParseCompilationDiagnostics(
                result.CompileOutput,
                sourceFiles,
                includeRawDetails: true)
            : _diagnosticParser.ParseExecutionDiagnostics(
                result,
                sourceFiles,
                includeRawDetails: true);

        return new PECodeRunCaseResultDto
        {
            Index = number,
            Number = number,
            Label = label,
            Status = ExecutionFeedbackMapper.MapCustomStatusText(
                result.State),
            Passed = result.State == CodeExecutionState.ExecutionSucceeded,
            ExecutionStatus =
                ExecutionFeedbackMapper.MapCustomExecutionStatus(
                    result.State),
            IsJudged = false,
            IsHidden = false,
            Input = input,
            ExpectedOutput = string.Empty,
            ActualOutput = result.StandardOutput ?? string.Empty,
            Stderr = ExecutionFeedbackSanitizer.SanitizeTechnicalText(
                result.StandardError ??
                result.Message,
                sourceFiles) ?? string.Empty,
            CompileOutput = ExecutionFeedbackSanitizer.SanitizeTechnicalText(
                result.CompileOutput,
                sourceFiles) ?? string.Empty,
            RuntimeMs = ExecutionFeedbackMapper.ConvertRuntimeToMilliseconds(
                result.TimeSeconds),
            MemoryKb = Math.Max(result.MemoryKb ?? 0, 0),
            Diagnostics = diagnostics
        };
    }

    private IReadOnlyCollection<ExecutionFeedbackDiagnosticDto>
        BuildOfficialCaseDiagnostics(
            SubmissionVerdict verdict,
            CodeExecutionResult result,
            IReadOnlyCollection<CodeFile> sourceFiles,
            bool includeRawDetails)
    {
        if (verdict == SubmissionVerdict.CompilationError)
        {
            return
            [
                new ExecutionFeedbackDiagnosticDto
                {
                    Category = ExecutionFeedbackCategory.Compilation,
                    Code = "COMPILATION_FAILED",
                    Severity = ExecutionFeedbackSeverity.Error,
                    Title = "The code did not compile",
                    Message = "This testcase was not executed because the code did not compile."
                }
            ];
        }

        if (verdict == SubmissionVerdict.SystemError)
        {
            return
            [
                new ExecutionFeedbackDiagnosticDto
                {
                    Category = ExecutionFeedbackCategory.System,
                    Code = "SYSTEM_ERROR",
                    Severity = ExecutionFeedbackSeverity.Error,
                    Title = "The execution platform reported a system error",
                    Message = "This testcase result could not be completed because of a platform error."
                }
            ];
        }

        return _diagnosticParser.ParseExecutionDiagnostics(
            result,
            sourceFiles,
            includeRawDetails);
    }
}
