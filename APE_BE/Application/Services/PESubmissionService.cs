using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;

namespace Application.Services;

public sealed class PESubmissionService : IPESubmissionService
{
    private const int AttemptAllocationRetryLimit = 3;

    private readonly IPESubmissionRepository _submissionRepository;
    private readonly IPracticeSessionRepository _sessionRepository;
    private readonly IExamRepository _examRepository;
    private readonly IPEQuestionRepository _questionRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IExecutionFeedbackDiagnosticParser _diagnosticParser;

    public PESubmissionService(
        IPESubmissionRepository submissionRepository,
        IPracticeSessionRepository sessionRepository,
        IExamRepository examRepository,
        IPEQuestionRepository questionRepository,
        IIdGenerator idGenerator,
        IExecutionFeedbackDiagnosticParser diagnosticParser)
    {
        _submissionRepository = submissionRepository;
        _sessionRepository = sessionRepository;
        _examRepository = examRepository;
        _questionRepository = questionRepository;
        _idGenerator = idGenerator;
        _diagnosticParser = diagnosticParser;
    }

    public async Task<ApiResponse<SubmissionAcceptedDto>> SubmitAsync(
        string studentId,
        PESubmissionInputDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "Authenticated student is required.");
        }

        if (request is null)
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "Submission request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "SessionId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.QuestionId))
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "QuestionId is required.");
        }

        if (request.Files is null || request.Files.Count == 0)
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "At least one source-code file is required.");
        }

        var session = await _sessionRepository.GetByIdAsync(
            request.SessionId,
            cancellationToken);

        if (session is null)
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "Practice session not found.");
        }

        if (!string.Equals(
                session.StudentId,
                studentId,
                StringComparison.Ordinal))
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "You do not have permission to submit to this session.");
        }

        if (session.Status != SessionStatus.InProgress)
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "Practice session is not in progress.");
        }

        var exam = await _examRepository.GetByIdAsync(
            session.ExamId,
            cancellationToken);

        if (exam is null)
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "Exam not found.");
        }

        var examQuestion = exam.PeExamQuestions.FirstOrDefault(
            item => string.Equals(
                item.PeQuestionId,
                request.QuestionId,
                StringComparison.Ordinal));

        if (examQuestion is null)
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "Question does not belong to this exam.");
        }

        if (examQuestion.AssignedPoints <= 0)
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "Question score configuration is invalid.");
        }

        var question = await _questionRepository.GetByIdAsync(
            request.QuestionId,
            cancellationToken);

        if (question is null)
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "Programming question not found.");
        }

        if (!string.Equals(
                question.Status,
                "Active",
                StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "Programming question is not active.");
        }

        if (!string.IsNullOrWhiteSpace(exam.CourseId) &&
            !string.Equals(
                question.CourseId,
                exam.CourseId,
                StringComparison.Ordinal))
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "Question does not belong to the exam course.");
        }

        if (question.TestCases.Count == 0)
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                "Programming question has no test cases.");
        }

        int languageId;

        try
        {
            languageId = question.ResolveLanguageId(
                request.RequestedLanguageId);
        }
        catch (DomainRuleException exception)
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                exception.Message);
        }

        List<CodeFile> submittedFiles;

        try
        {
            submittedFiles = request.Files
                .Select(file =>
                {
                    if (file is null)
                    {
                        throw new ArgumentException(
                            "Submitted files cannot contain null items.");
                    }

                    return CodeFile.Create(
                        file.Filename,
                        file.Content);
                })
                .ToList();
        }
        catch (Exception exception)
            when (exception is ArgumentException or DomainRuleException)
        {
            return ApiResponse.Fail<SubmissionAcceptedDto>(
                exception.Message);
        }

        for (var attempt = 0;
             attempt < AttemptAllocationRetryLimit;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var latestAttempt =
                await _submissionRepository
                    .GetLatestAttemptNumberAsync(
                        session.Id,
                        question.Id,
                        cancellationToken);

            if (latestAttempt == int.MaxValue)
            {
                return ApiResponse.Fail<SubmissionAcceptedDto>(
                    "Submission attempt limit was exceeded.");
            }

            var candidateAttempt = latestAttempt + 1;
            var submittedAt = DateTime.UtcNow;

            PE_Submission submission;

            try
            {
                submission = PE_Submission.Create(
                    id: _idGenerator.NewId(),
                    questionId: question.Id,
                    sessionId: session.Id,
                    courseId: exam.CourseId,
                    mode: ResolveSubmissionMode(exam.Mode),
                    topicTags: question.TopicTags,
                    submittedCode: submittedFiles,
                    languageId: languageId,
                    maxScore: examQuestion.AssignedPoints,
                    attemptCount: candidateAttempt,
                    submittedAt: submittedAt);
            }
            catch (DomainRuleException exception)
            {
                return ApiResponse.Fail<SubmissionAcceptedDto>(
                    exception.Message);
            }

            var inserted =
                await _submissionRepository.TryCreateAsync(
                    submission,
                    cancellationToken);

            if (!inserted)
            {
                continue;
            }

            return ApiResponse.Ok(new SubmissionAcceptedDto
            {
                SubmissionId = submission.Id,
                Status = submission.Status,
                AttemptCount = submission.AttemptCount,
                SubmittedAt = submission.SubmittedAt
            });
        }

        return ApiResponse.Fail<SubmissionAcceptedDto>(
            "Could not allocate a unique submission attempt. Please retry.");
    }

    public async Task<ApiResponse<PESubmissionDetailDto>> GetByIdAsync(
        string studentId,
        string submissionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return ApiResponse.Fail<PESubmissionDetailDto>(
                "Authenticated student is required.");
        }

        if (string.IsNullOrWhiteSpace(submissionId))
        {
            return ApiResponse.Fail<PESubmissionDetailDto>(
                "SubmissionId is required.");
        }

        var submission = await _submissionRepository.GetByIdAsync(
            submissionId,
            cancellationToken);

        if (submission is null)
        {
            return ApiResponse.Fail<PESubmissionDetailDto>(
                "Submission not found.");
        }

        var session = await _sessionRepository.GetByIdAsync(
            submission.SessionId,
            cancellationToken);

        if (session is null)
        {
            return ApiResponse.Fail<PESubmissionDetailDto>(
                "Practice session not found.");
        }

        if (!string.Equals(
                session.StudentId,
                studentId,
                StringComparison.Ordinal))
        {
            return ApiResponse.Fail<PESubmissionDetailDto>(
                "You do not have permission to view this submission.");
        }

        var question = await _questionRepository.GetByIdAsync(
            submission.QuestionId,
            cancellationToken);

        return ApiResponse.Ok(MapDetail(submission, question));
    }

    public async Task<
        ApiResponse<IReadOnlyCollection<PESubmissionSummaryDto>>>
        GetBySessionAsync(
            string studentId,
            string sessionId,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return ApiResponse.Fail<
                IReadOnlyCollection<PESubmissionSummaryDto>>(
                "Authenticated student is required.");
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return ApiResponse.Fail<
                IReadOnlyCollection<PESubmissionSummaryDto>>(
                "SessionId is required.");
        }

        var session = await _sessionRepository.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session is null)
        {
            return ApiResponse.Fail<
                IReadOnlyCollection<PESubmissionSummaryDto>>(
                "Practice session not found.");
        }

        if (!string.Equals(
                session.StudentId,
                studentId,
                StringComparison.Ordinal))
        {
            return ApiResponse.Fail<
                IReadOnlyCollection<PESubmissionSummaryDto>>(
                "You do not have permission to view this session.");
        }

        var submissions =
            await _submissionRepository.GetBySessionIdAsync(
                sessionId,
                cancellationToken);

        var result = submissions
            .OrderByDescending(item => item.SubmittedAt)
            .Select(MapSummary)
            .ToList();

        return ApiResponse.Ok<
            IReadOnlyCollection<PESubmissionSummaryDto>>(result);
    }

    private static PESubmissionSummaryDto MapSummary(
        PE_Submission submission)
    {
        return new PESubmissionSummaryDto
        {
            Id = submission.Id,
            QuestionId = submission.QuestionId,
            Status = submission.Status,
            FinalVerdict = submission.FinalVerdict,
            QuestionScore = submission.QuestionScore,
            MaxScore = submission.MaxScore,
            AttemptCount = submission.AttemptCount,
            SubmittedAt = submission.SubmittedAt,
            CompletedAt = submission.CompletedAt
        };
    }

    private PESubmissionDetailDto MapDetail(
        PE_Submission submission,
        PEQuestion? question)
    {
        return new PESubmissionDetailDto
        {
            Id = submission.Id,
            QuestionId = submission.QuestionId,
            SessionId = submission.SessionId,
            CourseId = submission.CourseId,
            Mode = submission.Mode,
            LanguageId = submission.LanguageId,
            MaxScore = submission.MaxScore,
            QuestionScore = submission.QuestionScore,
            Status = submission.Status,
            FinalVerdict = submission.FinalVerdict,
            TestCasesPassed = submission.TestCasesPassed,
            TotalTestCases = submission.TotalTestCases,
            RuntimeMs = submission.RuntimeMs,
            MemoryKb = submission.MemoryKb,
            AttemptCount = submission.AttemptCount,
            SubmittedAt = submission.SubmittedAt,
            ProcessingStartedAt = submission.ProcessingStartedAt,
            CompletedAt = submission.CompletedAt,
            ProcessingError = submission.ProcessingError,
            Diagnostics = BuildSubmissionDiagnostics(
                submission),

            SubmittedCode = submission.SubmittedCode
                .Select(file => new CodeFileDto
                {
                    Filename = file.Filename,
                    Content = file.Content
                })
                .ToList(),

            TestResults = submission.TestResultItems
                .OrderBy(item => item.TestCaseIndex)
                .Select(item => MapTestResult(
                    submission,
                    item,
                    question))
                .ToList()
        };
    }

    private TestResultDto MapTestResult(
        PE_Submission submission,
        TestResultItem result,
        PEQuestion? question)
    {
        TestCase? sourceTestCase = null;

        if (question is not null &&
            result.TestCaseIndex >= 0 &&
            result.TestCaseIndex < question.TestCases.Count)
        {
            sourceTestCase = question.TestCases[result.TestCaseIndex];
        }

        var executionStatus =
            ExecutionFeedbackMapper.MapOfficialExecutionStatus(
                result.Verdict);

        return new TestResultDto
        {
            TestCaseIndex = result.TestCaseIndex,
            Number = result.TestCaseIndex + 1,
            Label = result.IsHidden
                ? $"Hidden test case {result.TestCaseIndex + 1}"
                : $"Test case {result.TestCaseIndex + 1}",
            Verdict = result.Verdict,
            ExecutionStatus = executionStatus,
            IsJudged = true,

            Input = string.IsNullOrWhiteSpace(result.Input)
                ? sourceTestCase?.Input
                : result.Input,
            ActualOutput = result.ActualOutput,
            ExpectedOutput = result.IsHidden
                ? null
                : string.IsNullOrWhiteSpace(result.ExpectedOutput)
                    ? sourceTestCase?.ExpectedOutput
                    : result.ExpectedOutput,

            StandardError = result.IsHidden
                ? null
                : ExecutionFeedbackSanitizer.SanitizeTechnicalText(
                    result.StandardError,
                    submission.SubmittedCode.ToList()),

            CompileOutput = result.IsHidden
                ? null
                : ExecutionFeedbackSanitizer.SanitizeTechnicalText(
                    result.CompileOutput,
                    submission.SubmittedCode.ToList()),
            RuntimeMs = result.RuntimeMs,
            MemoryKb = result.MemoryKb,
            IsHidden = result.IsHidden,
            Diagnostics = BuildTestResultDiagnostics(
                submission,
                result)
        };
    }

    private IReadOnlyCollection<ExecutionFeedbackDiagnosticDto>
        BuildSubmissionDiagnostics(
            PE_Submission submission)
    {
        if (submission.FinalVerdict == SubmissionVerdict.CompilationError)
        {
            var compileOutput = submission.TestResultItems
                .Select(item => item.CompileOutput)
                .FirstOrDefault(item =>
                    !string.IsNullOrWhiteSpace(item));

            return _diagnosticParser.ParseCompilationDiagnostics(
                compileOutput,
                submission.SubmittedCode.ToList(),
                includeRawDetails: true);
        }

        if (submission.FinalVerdict == SubmissionVerdict.SystemError)
        {
            return
            [
                new ExecutionFeedbackDiagnosticDto
                {
                    Category = ExecutionFeedbackCategory.System,
                    Code = "SYSTEM_ERROR",
                    Severity = ExecutionFeedbackSeverity.Error,
                    Title = "The execution platform reported a system error",
                    Message = "The submission could not be fully processed because of a platform error."
                }
            ];
        }

        return Array.Empty<ExecutionFeedbackDiagnosticDto>();
    }

    private IReadOnlyCollection<ExecutionFeedbackDiagnosticDto>
        BuildTestResultDiagnostics(
            PE_Submission submission,
            TestResultItem result)
    {
        if (result.Verdict == SubmissionVerdict.CompilationError)
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

        if (result.Verdict == SubmissionVerdict.SystemError)
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

        if (result.Verdict is SubmissionVerdict.RuntimeError or
            SubmissionVerdict.TimeLimitExceeded or
            SubmissionVerdict.MemoryLimitExceeded or
            SubmissionVerdict.OutputLimitExceeded)
        {
            var executionState = result.Verdict switch
            {
                SubmissionVerdict.TimeLimitExceeded =>
                    CodeExecutionState.TimedOut,
                SubmissionVerdict.MemoryLimitExceeded =>
                    CodeExecutionState.MemoryLimitExceeded,
                SubmissionVerdict.OutputLimitExceeded =>
                    CodeExecutionState.OutputLimitExceeded,
                _ => CodeExecutionState.RuntimeFailed
            };

            return _diagnosticParser.ParseExecutionDiagnostics(
                new CodeExecutionResult(
                    result.TestCaseIndex,
                    string.Empty,
                    executionState,
                    StandardOutput: result.ActualOutput,
                    StandardError: result.StandardError,
                    CompileOutput: result.CompileOutput,
                    Message: result.StandardError,
                    TimeSeconds: result.RuntimeMs / 1000d,
                    MemoryKb: result.MemoryKb,
                    ProviderStatusCode: null,
                    ProviderStatusDescription: null),
                submission.SubmittedCode.ToList(),
                includeRawDetails: !result.IsHidden);
        }

        return Array.Empty<ExecutionFeedbackDiagnosticDto>();
    }

    private static SubmissionMode ResolveSubmissionMode(
        ExamMode examMode)
    {
        return examMode switch
        {
            ExamMode.MockTest =>
                SubmissionMode.MockTest,

            ExamMode.PracticePlaylist =>
                SubmissionMode.PracticePlaylist,

            ExamMode.Practice =>
                SubmissionMode.Practice,

            _ =>
                SubmissionMode.Practice
        };
    }
}
