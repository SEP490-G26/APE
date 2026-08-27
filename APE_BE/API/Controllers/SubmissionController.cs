using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/student/submissions")]
[Authorize]
public sealed class SubmissionController : ControllerBase
{
    private readonly FESubmissionService _feSubmissionService;
    private readonly IPESubmissionService _peSubmissionService;
    private readonly IPECodeRunService _peCodeRunService;
    private readonly CodeMentorService _codeMentorService;
    private readonly IPracticeSessionRepository _practiceSessionRepository;
    private readonly IExamRepository _examRepository;

    public SubmissionController(
        FESubmissionService feSubmissionService,
        IPESubmissionService peSubmissionService,
        IPECodeRunService peCodeRunService,
        CodeMentorService codeMentorService,
        IPracticeSessionRepository practiceSessionRepository,
        IExamRepository examRepository)
    {
        _feSubmissionService = feSubmissionService;
        _peSubmissionService = peSubmissionService;
        _peCodeRunService = peCodeRunService;
        _codeMentorService = codeMentorService;
        _practiceSessionRepository = practiceSessionRepository;
        _examRepository = examRepository;
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpPost("fe")]
    public async Task<IActionResult> SubmitFE(
        [FromBody] FESubmissionInputDto request)
    {
        var studentId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(
                ApiResponse.Fail("Unauthorized."));
        }

        var result =
            await _feSubmissionService.CreateAndEvaluateAsync(
                studentId,
                request);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpPost("pe")]
    public async Task<IActionResult> SubmitPE(
        [FromBody] PESubmissionInputDto request,
        CancellationToken cancellationToken)
    {
        var studentId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(
                ApiResponse.Fail("Unauthorized."));
        }

        var response =
            await _peSubmissionService.SubmitAsync(
                studentId,
                request,
                cancellationToken);

        return response.Success
            ? Accepted(response)
            : BadRequest(response);
    }

    [HttpPost("pe/run")]
    [ProducesResponseType(typeof(ApiResponse<PECodeRunResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunPE(
        [FromBody] PECodeRunInputDto request,
        CancellationToken cancellationToken)
    {
        var studentId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized."));
        }

        var result = await _peCodeRunService.RunAsync(
            studentId,
            request,
            cancellationToken);

        if (result.Success)
        {
            return Ok(ApiResponse.Ok(result.Payload!));
        }

        return result.StatusCode switch
        {
            StatusCodes.Status400BadRequest =>
                BadRequest(ApiResponse.Fail(result.Error ?? "Run failed.")),

            StatusCodes.Status404NotFound =>
                NotFound(ApiResponse.Fail(result.Error ?? "Run failed.")),

            _ => StatusCode(
                result.StatusCode,
                ApiResponse.Fail(result.Error ?? "Run failed."))
        };
    }

    [HttpGet("pe/{id}")]
    [ProducesResponseType(
        typeof(ApiResponse<PESubmissionDetailDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<PESubmissionDetailDto>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPESubmission(
        string id,
        CancellationToken cancellationToken)
    {
        var studentId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(
                ApiResponse.Fail("Unauthorized."));
        }

        var result =
            await _peSubmissionService.GetByIdAsync(
                studentId,
                id,
                cancellationToken);

        return result.Success
            ? Ok(result)
            : NotFound(result);
    }

    [HttpGet("pe/session/{sessionId}")]
    [ProducesResponseType(
        typeof(ApiResponse<
            IReadOnlyCollection<PESubmissionSummaryDto>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPESubmissionsBySession(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var studentId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(
                ApiResponse.Fail("Unauthorized."));
        }

        var result =
            await _peSubmissionService.GetBySessionAsync(
                studentId,
                sessionId,
                cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpGet("session/{sessionId}")]
    [ProducesResponseType(
        typeof(ApiResponse<SubmissionSessionResultDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionSubmissions(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var studentId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized."));
        }

        var session = await _practiceSessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || !string.Equals(session.StudentId, studentId, StringComparison.Ordinal))
        {
            return NotFound(ApiResponse.Fail("Practice session not found."));
        }

        var exam = await _examRepository.GetByIdAsync(session.ExamId, cancellationToken);
        var feResponse = await _feSubmissionService.GetBySessionAsync(sessionId);
        var peResponse = await _peSubmissionService.GetBySessionAsync(studentId, sessionId, cancellationToken);

        if (!peResponse.Success)
        {
            return BadRequest(ApiResponse.Fail(peResponse.Error ?? "Could not load PE submissions."));
        }

        var payload = new SubmissionSessionResultDto
        {
            SessionId = session.Id,
            ExamId = session.ExamId,
            ExamTitle = exam?.Title ?? string.Empty,
            CourseId = exam?.CourseId ?? string.Empty,
            ExamMode = exam?.Mode.ToString() ?? string.Empty,
            TimeLimit = exam?.TimeLimit,
            SessionStatus = session.Status.ToString(),
            FEQuestionCount = exam?.FeExamQuestions?.Count ?? 0,
            PEQuestionCount = exam?.PeExamQuestions?.Count ?? 0,
            TotalQuestionCount = (exam?.FeExamQuestions?.Count ?? 0) + (exam?.PeExamQuestions?.Count ?? 0),
            FE = feResponse.Data ?? new List<FESubmissionResultDto>(),
            PE = (peResponse.Data ?? Array.Empty<PESubmissionSummaryDto>())
                .Select(item => new PESubmissionResultDto
                {
                    Id = item.Id,
                    QuestionId = item.QuestionId,
                    SessionId = session.Id,
                    CourseId = exam?.CourseId ?? string.Empty,
                    SubmitTime = item.SubmittedAt,
                    Status = item.Status.ToString(),
                    EarnedScore = item.QuestionScore,
                    MaxScore = item.MaxScore,
                    AttemptCount = item.AttemptCount
                })
                .ToList()
        };

        payload.FESubmissionCount = payload.FE.Count;
        payload.PESubmissionCount = payload.PE.Count;
        payload.TotalSubmissionCount = payload.FESubmissionCount + payload.PESubmissionCount;

        return Ok(ApiResponse.Ok(payload));
    }

    [HttpPost("{id}/mentor")]
    [ProducesResponseType(
        typeof(ApiResponse<CodeMentorResultDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestCodeMentor(
        string id,
        [FromBody] CodeMentorRequestDto request,
        CancellationToken cancellationToken)
    {
        var requesterUserId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(requesterUserId))
        {
            return Unauthorized(
                ApiResponse.Fail("Unauthorized."));
        }

        try
        {
            var result = await _codeMentorService.RunAsync(
                id,
                requesterUserId,
                request,
                cancellationToken);

            return Ok(ApiResponse.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/mentor-feedback")]
    [ProducesResponseType(
        typeof(ApiResponse<MentorFeedbackDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestMentorFeedback(
        string id,
        CancellationToken cancellationToken)
    {
        var requesterUserId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(requesterUserId))
        {
            return Unauthorized(
                ApiResponse.Fail("Unauthorized."));
        }

        MentorFeedbackDto? result;
        try
        {
            result =
                await _codeMentorService.GetLatestAsync(
                    id,
                    requesterUserId,
                    cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }

        if (result is null)
        {
            return NotFound(
                ApiResponse.Fail(
                    "Mentor feedback not found."));
        }

        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{id}/mentor-feedbacks")]
    [ProducesResponseType(
        typeof(ApiResponse<List<MentorFeedbackDto>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMentorFeedbackHistory(
        string id,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var requesterUserId = CurrentUserId;

        if (string.IsNullOrWhiteSpace(requesterUserId))
        {
            return Unauthorized(
                ApiResponse.Fail("Unauthorized."));
        }

        try
        {
            var result =
                await _codeMentorService.GetHistoryAsync(
                    id,
                    requesterUserId,
                    take,
                    cancellationToken);

            return Ok(ApiResponse.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

}
