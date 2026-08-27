using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/student/exam-config")]
[Authorize]
public class StudentExamConfigController : ControllerBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IKnowledgeChunkRepository _knowledgeChunkRepository;
    private readonly IFEQuestionRepository _feQuestionRepository;
    private readonly IPEQuestionRepository _peQuestionRepository;
    private readonly ExamService _examService;
    private readonly PracticeService _practiceService;
    private readonly ICourseRepository _courseRepository;

    public StudentExamConfigController(
        IDocumentRepository documentRepository,
        IKnowledgeChunkRepository knowledgeChunkRepository,
        IFEQuestionRepository feQuestionRepository,
        IPEQuestionRepository peQuestionRepository,
        ExamService examService,
        PracticeService practiceService,
        ICourseRepository courseRepository)
    {
        _documentRepository = documentRepository;
        _knowledgeChunkRepository = knowledgeChunkRepository;
        _feQuestionRepository = feQuestionRepository;
        _peQuestionRepository = peQuestionRepository;
        _examService = examService;
        _practiceService = practiceService;
        _courseRepository = courseRepository;
    }

    private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("courses/{courseId}/documents")]
    public async Task<IActionResult> GetDocuments(string courseId, [FromQuery] string? source = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId))
            {
                return Unauthorized(ApiResponse.Fail("Unauthorized."));
            }

            if (string.IsNullOrWhiteSpace(courseId))
            {
                return BadRequest(ApiResponse.Fail("Please choose a course."));
            }

            if (!string.Equals(NormalizeSource(source), "BYOS", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ApiResponse.Ok(new List<ExamConfigDocumentDto>()));
            }

            var (documents, _) = await _documentRepository.ListByCourseAsync(courseId, 1, 200);
            var items = documents
                .Where(document =>
                    document.IsActive &&
                    MatchesRequestedSource(document.Source, source) &&
                    (
                        !string.Equals(document.Source, "BYOS", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(document.UserId, UserId, StringComparison.OrdinalIgnoreCase)
                    ))
                .Select(document => new ExamConfigDocumentDto
                {
                    Id = document.Id,
                    FileName = document.FileName,
                    Source = string.Equals(document.Source, "BYOS", StringComparison.OrdinalIgnoreCase) ? "BYOS" : "System",
                    ChunkCount = 0
                })
                .ToList();

            foreach (var item in items)
            {
                item.ChunkCount = (await _knowledgeChunkRepository.GetByDocumentIdAsync(item.Id)).Count;
            }

            return Ok(ApiResponse.Ok(items));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("question-pool")]
    public async Task<IActionResult> GetQuestionPool([FromQuery] string courseId, [FromQuery] string examType, [FromQuery] string[] documentIds, [FromQuery] string? source = null)
    {
        try
        {
            var pool = await BuildQuestionPoolAsync(courseId, examType, documentIds, UserId, source);
            return Ok(ApiResponse.Ok(pool));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartConfiguredExam([FromBody] StartConfiguredExamRequestDto request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UserId))
            {
                return Unauthorized(ApiResponse.Fail("Unauthorized."));
            }

            var normalizedExamType = NormalizeExamType(request.ExamType);
            var pool = await BuildQuestionPoolAsync(
                request.CourseId,
                normalizedExamType,
                (request.DocumentIds ?? new List<string>()).ToArray(),
                UserId,
                request.Source);

            if (pool.AvailableCount == 0)
            {
                return BadRequest(ApiResponse.Fail(string.Equals(NormalizeSource(request.Source), "BYOS", StringComparison.OrdinalIgnoreCase)
                    ? "No active questions are available for the selected course and BYOS documents."
                    : "No active public questions are available for the selected course."));
            }

            var course = await _courseRepository.GetByIdAsync(request.CourseId);
            var createExam = new CreateExamDto
            {
                Title = $"{course?.Code ?? request.CourseId} {normalizedExamType} Exam",
                CourseId = request.CourseId,
                ExamType = normalizedExamType,
                Mode = normalizedExamType == "FE" && string.Equals(request.Mode, "Flashcard", StringComparison.OrdinalIgnoreCase)
                    ? "Flashcard"
                    : "Practice",
                TimeLimit = null,
                CreatedBy = UserId,
                Visibility = "Private"
            };

            if (normalizedExamType == "FE")
            {
                var filteredItems = FilterPoolItems(pool.Items, request.TopicTags, request.Difficulties);
                if (filteredItems.Count == 0)
                {
                    return BadRequest(ApiResponse.Fail("No FE questions remain after applying the topic tag and difficulty filters."));
                }

                var requestedBuckets = NormalizeDifficultyCounts(request.FeDifficultyCounts);
                if (requestedBuckets.Count == 0)
                {
                    return BadRequest(ApiResponse.Fail("Please enter the FE question count for at least one difficulty level."));
                }

                var selectedFeQuestionIds = new List<string>();
                foreach (var bucket in requestedBuckets)
                {
                    var bucketItems = filteredItems
                        .Where(item => string.Equals(NormalizeDifficulty(item.Difficulty), bucket.Key, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(_ => Guid.NewGuid())
                        .Take(bucket.Value)
                        .Select(item => item.Id)
                        .ToList();

                    if (bucketItems.Count < bucket.Value)
                    {
                        return BadRequest(ApiResponse.Fail($"There are not enough {bucket.Key} questions. Only {bucketItems.Count} questions are available after filtering."));
                    }

                    selectedFeQuestionIds.AddRange(bucketItems);
                }

                if (selectedFeQuestionIds.Count == 0)
                {
                    return BadRequest(ApiResponse.Fail("No FE questions were selected to create the exam."));
                }

                createExam.FeExamQuestions = selectedFeQuestionIds
                    .OrderBy(_ => Guid.NewGuid())
                    .ToList();
            }
            else
            {
                var filteredItems = FilterPoolItems(pool.Items, request.TopicTags, request.Difficulties);
                var allowedIds = filteredItems.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var selectedPeQuestionIds = (request.PeQuestionIds ?? new List<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (selectedPeQuestionIds.Count == 0)
                {
                    return BadRequest(ApiResponse.Fail("Please select at least one PE question."));
                }

                if (selectedPeQuestionIds.Any(id => !allowedIds.Contains(id)))
                {
                    return BadRequest(ApiResponse.Fail("The selected PE question list is not valid for the current filters."));
                }

                createExam.PeExamQuestions = selectedPeQuestionIds
                    .Select(id => new PeExamQuestionDto
                    {
                        PeQuestionId = id,
                        AssignedPoints = 1
                    })
                    .ToList();
            }

            var exam = await _examService.CreateExamAsync(createExam);
            var session = await _practiceService.StartSessionAsync(UserId, exam.Id, forceNew: true);
            if (!session.Success || session.Data is null)
            {
                return BadRequest(session);
            }

            var detail = await _examService.GetExamDetailByIdAsync(exam.Id);
            var result = new StartConfiguredExamResultDto
            {
                ExamId = exam.Id,
                SessionId = session.Data.SessionId,
                StartTime = session.Data.StartTime,
                ExamType = normalizedExamType,
                QuestionCount = normalizedExamType == "FE"
                    ? createExam.FeExamQuestions.Count
                    : createExam.PeExamQuestions.Count,
                Exam = detail
            };

            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    private async Task<ExamConfigQuestionPoolDto> BuildQuestionPoolAsync(string courseId, string examType, string[] documentIds, string? userId, string? source)
    {
        if (string.IsNullOrWhiteSpace(courseId))
        {
            throw new InvalidOperationException("Please choose a course.");
        }

        var normalizedExamType = NormalizeExamType(examType);
        var normalizedSource = NormalizeSource(source);
        var normalizedDocumentIds = documentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.Equals(normalizedSource, "BYOS", StringComparison.OrdinalIgnoreCase) && normalizedDocumentIds.Count == 0)
        {
            throw new InvalidOperationException("Please choose at least one document.");
        }

        var allChunkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedAccessibleIds = new List<string>();

        if (string.Equals(normalizedSource, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            var (documents, _) = await _documentRepository.ListByCourseAsync(courseId, 1, 500);
            var accessibleDocumentIds = documents
                .Where(document =>
                    document.IsActive &&
                    MatchesRequestedSource(document.Source, source) &&
                    string.Equals(document.UserId, userId, StringComparison.OrdinalIgnoreCase))
                .Select(document => document.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            selectedAccessibleIds = normalizedDocumentIds
                .Where(accessibleDocumentIds.Contains)
                .ToList();

            if (selectedAccessibleIds.Count == 0)
            {
                throw new InvalidOperationException("No valid BYOS documents were found for the selected course.");
            }

            foreach (var documentId in selectedAccessibleIds)
            {
                var chunks = await _knowledgeChunkRepository.GetByDocumentIdAsync(documentId);
                foreach (var chunk in chunks)
                {
                    allChunkIds.Add(chunk.Id);
                }
            }
        }

        if (normalizedExamType == "FE")
        {
            var (questions, _) = await _feQuestionRepository.ListAsync(null, courseId, null, 1, 500);
            var items = questions
                .Where(question =>
                    string.Equals(question.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                    (
                        string.Equals(normalizedSource, "System", StringComparison.OrdinalIgnoreCase)
                            ? question.IsPublic
                            : (
                                string.Equals(question.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase) &&
                                (
                                    (question.SourceDocumentIds != null &&
                                     question.SourceDocumentIds.Any(id => selectedAccessibleIds.Contains(id, StringComparer.OrdinalIgnoreCase))) ||
                                    (question.LegacySourceChunkIds != null &&
                                     question.LegacySourceChunkIds.Any(id => allChunkIds.Contains(id)))
                                )
                            )
                    ))
                .Select(question => new ExamConfigQuestionItemDto
                {
                    Id = question.Id,
                    Title = question.Title,
                    Difficulty = question.Difficulty,
                    TopicTags = question.TopicTags
                })
                .ToList();

            return new ExamConfigQuestionPoolDto
            {
                CourseId = courseId,
                Source = normalizedSource,
                ExamType = normalizedExamType,
                DocumentIds = selectedAccessibleIds,
                AvailableCount = items.Count,
                Items = items
            };
        }

        var (peQuestions, _) = await _peQuestionRepository.ListAsync(null, courseId, null, 1, 500);
        var peItems = peQuestions
            .Where(question =>
                string.Equals(question.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                (
                    string.Equals(normalizedSource, "System", StringComparison.OrdinalIgnoreCase)
                        ? question.IsPublic
                        : (
                            string.Equals(question.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase) &&
                            (
                                (question.SourceDocumentIds != null &&
                                 question.SourceDocumentIds.Any(id => selectedAccessibleIds.Contains(id, StringComparer.OrdinalIgnoreCase))) ||
                                (question.LegacyChunkIds != null &&
                                 question.LegacyChunkIds.Any(id => allChunkIds.Contains(id)))
                            )
                        )
                ))
            .Select(question => new ExamConfigQuestionItemDto
            {
                Id = question.Id,
                Title = question.Title,
                Difficulty = question.Difficulty,
                TopicTags = question.TopicTags
            })
            .ToList();

        return new ExamConfigQuestionPoolDto
        {
            CourseId = courseId,
            Source = normalizedSource,
            ExamType = normalizedExamType,
            DocumentIds = selectedAccessibleIds,
            AvailableCount = peItems.Count,
            Items = peItems
        };
    }

    private static List<ExamConfigQuestionItemDto> FilterPoolItems(
        IEnumerable<ExamConfigQuestionItemDto> items,
        IEnumerable<string>? topicTags,
        IEnumerable<string>? difficulties)
    {
        var normalizedTopics = (topicTags ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalizedDifficulties = (difficulties ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(NormalizeDifficulty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return items
            .Where(item =>
                (normalizedTopics.Count == 0 ||
                 (item.TopicTags ?? new List<string>()).Any(tag => normalizedTopics.Contains(tag))) &&
                (normalizedDifficulties.Count == 0 ||
                 normalizedDifficulties.Contains(NormalizeDifficulty(item.Difficulty))))
            .ToList();
    }

    private static Dictionary<string, int> NormalizeDifficultyCounts(IEnumerable<DifficultyCountDto>? items)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? Array.Empty<DifficultyCountDto>())
        {
            var key = NormalizeDifficulty(item.Difficulty);
            if (string.IsNullOrWhiteSpace(key) || item.Count <= 0)
            {
                continue;
            }

            result[key] = item.Count;
        }

        return result;
    }

    private static string NormalizeDifficulty(string? difficulty)
    {
        return (difficulty ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "easy" => "Easy",
            "medium" => "Medium",
            "hard" => "Hard",
            _ => (difficulty ?? string.Empty).Trim()
        };
    }

    private static string NormalizeSource(string? source)
    {
        return string.Equals(source, "BYOS", StringComparison.OrdinalIgnoreCase)
            ? "BYOS"
            : "System";
    }

    private static bool MatchesRequestedSource(string? actualSource, string? requestedSource)
    {
        var normalizedRequested = NormalizeSource(requestedSource);
        var normalizedActual = string.Equals(actualSource, "BYOS", StringComparison.OrdinalIgnoreCase)
            ? "BYOS"
            : "System";
        return string.Equals(normalizedRequested, normalizedActual, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExamType(string? examType)
    {
        if (string.Equals(examType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return "PE";
        }

        return "FE";
    }
}
