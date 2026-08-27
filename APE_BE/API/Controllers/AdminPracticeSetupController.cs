using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/admin/practice-setup")]
[Authorize(Roles = "Admin")]
public class AdminPracticeSetupController : ControllerBase
{
    private const int MaxFeCount = 50;
    private const int MaxPeCount = 5;

    private readonly IFEQuestionRepository _feQuestionRepository;
    private readonly IPEQuestionRepository _peQuestionRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly ExamService _examService;

    public AdminPracticeSetupController(
        IFEQuestionRepository feQuestionRepository,
        IPEQuestionRepository peQuestionRepository,
        ICourseRepository courseRepository,
        ExamService examService)
    {
        _feQuestionRepository = feQuestionRepository;
        _peQuestionRepository = peQuestionRepository;
        _courseRepository = courseRepository;
        _examService = examService;
    }

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    [HttpGet("question-pool")]
    public async Task<IActionResult> GetQuestionPool([FromQuery] string courseId, [FromQuery] string examType)
    {
        try
        {
            var pool = await BuildQuestionPoolAsync(courseId, examType);
            return Ok(ApiResponse.Ok(pool));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? courseId = null,
        [FromQuery] string? examType = null,
        [FromQuery] string? mode = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10)
    {
        try
        {
            if (page < 1 || limit < 1)
            {
                return BadRequest(ApiResponse.Fail("page and limit must be >= 1"));
            }

            var (items, total) = await _examService.ListPublicAsync(
                courseId,
                NormalizeExamType(examType, allowEmpty: true),
                NormalizeMode(mode, allowEmpty: true),
                page,
                limit);

            var response = new
            {
                items,
                total,
                page,
                limit,
                totalPages = (total + limit - 1) / limit
            };

            return Ok(ApiResponse.Ok(response));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        try
        {
            var exam = await _examService.GetExamDetailByIdAsync(id);
            if (exam == null)
            {
                return NotFound(ApiResponse.Fail("Exam not found"));
            }

            return Ok(ApiResponse.Ok(exam));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveAdminPracticeSetupRequestDto request)
    {
        try
        {
            var createExam = await BuildCreateExamDtoAsync(request);
            var exam = await _examService.CreateExamAsync(createExam);
            var detail = await _examService.GetExamDetailByIdAsync(exam.Id);
            return Ok(ApiResponse.Ok(detail));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("{id}")]
    public IActionResult Update(string id, [FromBody] SaveAdminPracticeSetupRequestDto request)
    {
        return StatusCode(
            StatusCodes.Status405MethodNotAllowed,
            ApiResponse.Fail("Updating an existing system practice setup is disabled."));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(ApiResponse.Fail("Exam id is required."));
            }

            await _examService.SoftDeleteExamAsync(id, UserId, requireOwner: false);
            return Ok(ApiResponse.Ok(true));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    private async Task<CreateExamDto> BuildCreateExamDtoAsync(SaveAdminPracticeSetupRequestDto request)
    {
        var pool = await BuildQuestionPoolAsync(request.CourseId, request.ExamType);
        if (pool.AvailableCount == 0)
        {
            throw new InvalidOperationException("No active public system questions are available for the selected course.");
        }

        var course = await _courseRepository.GetByIdAsync(request.CourseId);
        if (course == null || !string.Equals(course.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected course is invalid or no longer active.");
        }

        var normalizedExamType = NormalizeExamType(request.ExamType);
        var normalizedMode = NormalizeMode(request.Mode);
        var filteredItems = FilterPoolItems(pool.Items, request.TopicTags, request.Difficulties);

        if (filteredItems.Count == 0)
        {
            throw new InvalidOperationException("No questions remain after applying the topic tag and difficulty filters.");
        }

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? BuildDefaultTitle(course.Code, normalizedExamType, normalizedMode, NormalizePresetScope(request.PresetScope))
            : request.Title.Trim();

        var createExam = new CreateExamDto
        {
            Title = title,
            CourseId = request.CourseId.Trim(),
            ExamType = normalizedExamType,
            PresetScope = NormalizePresetScope(request.PresetScope),
            Mode = normalizedMode,
            TimeLimit = null,
            CreatedBy = UserId,
            Visibility = "Public"
        };

        if (normalizedExamType == "FE")
        {
            var requestedBuckets = NormalizeDifficultyCounts(request.FeDifficultyCounts);
            var totalRequested = requestedBuckets.Values.Sum();

            if (totalRequested <= 0)
            {
                throw new InvalidOperationException("An FE setup must contain at least one question.");
            }

            if (totalRequested > MaxFeCount)
            {
                throw new InvalidOperationException($"The FE question count cannot exceed {MaxFeCount}.");
            }

            var selectedFeQuestionIds = new List<string>();
            foreach (var bucket in requestedBuckets)
            {
                var bucketItems = filteredItems
                    .Where(item => string.Equals(NormalizeDifficulty(item.Difficulty), bucket.Key, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(item => item.Title)
                    .ThenBy(item => item.Id)
                    .Take(bucket.Value)
                    .Select(item => item.Id)
                    .ToList();

                if (bucketItems.Count < bucket.Value)
                {
                    throw new InvalidOperationException($"There are not enough {bucket.Key} questions. Only {bucketItems.Count} questions are available after filtering.");
                }

                selectedFeQuestionIds.AddRange(bucketItems);
            }

            if (selectedFeQuestionIds.Count == 0)
            {
                throw new InvalidOperationException("No FE questions were selected.");
            }

            createExam.FeExamQuestions = selectedFeQuestionIds;
        }
        else
        {
            var allowedIds = filteredItems.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selectedPeQuestionIds = (request.PeQuestionIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (selectedPeQuestionIds.Count == 0)
            {
                throw new InvalidOperationException("A PE setup must contain at least one question.");
            }

            if (selectedPeQuestionIds.Count > MaxPeCount)
            {
                throw new InvalidOperationException($"The PE question count cannot exceed {MaxPeCount}.");
            }

            if (selectedPeQuestionIds.Any(id => !allowedIds.Contains(id)))
            {
                throw new InvalidOperationException("The selected PE question list is not valid for the current filters.");
            }

            createExam.PeExamQuestions = selectedPeQuestionIds
                .Select(id => new PeExamQuestionDto
                {
                    PeQuestionId = id,
                    AssignedPoints = 1
                })
                .ToList();
        }

        return createExam;
    }

    private async Task<ExamConfigQuestionPoolDto> BuildQuestionPoolAsync(string courseId, string examType)
    {
        if (string.IsNullOrWhiteSpace(courseId))
        {
            throw new InvalidOperationException("Please choose a course.");
        }

        var normalizedExamType = NormalizeExamType(examType);
        var course = await _courseRepository.GetByIdAsync(courseId);
        if (course == null || !string.Equals(course.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Course khong hop le hoac khong con active.");
        }

        if (normalizedExamType == "FE")
        {
            var (questions, _) = await _feQuestionRepository.ListAsync(null, courseId, null, 1, 1000);
            var items = questions
                .Where(question =>
                    string.Equals(question.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                    question.IsPublic &&
                    string.Equals(question.Source, "Admin", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(question.SourceScope, "SYSTEM", StringComparison.OrdinalIgnoreCase))
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
                CourseId = courseId.Trim(),
                Source = "System",
                ExamType = normalizedExamType,
                AvailableCount = items.Count,
                Items = items
            };
        }

        var (peQuestions, _) = await _peQuestionRepository.ListAsync(null, courseId, null, 1, 1000);
        var peItems = peQuestions
            .Where(question =>
                string.Equals(question.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                question.IsPublic &&
                string.Equals(question.Source, "Admin", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(question.SourceScope, "SYSTEM", StringComparison.OrdinalIgnoreCase))
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
            CourseId = courseId.Trim(),
            Source = "System",
            ExamType = normalizedExamType,
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

    private static string NormalizeMode(string? mode, bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return allowEmpty ? string.Empty : "Practice";
        }

        return string.Equals(mode, "Flashcard", StringComparison.OrdinalIgnoreCase)
            ? "Flashcard"
            : "Practice";
    }

    private static string NormalizeExamType(string? examType, bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(examType))
        {
            return allowEmpty ? string.Empty : "FE";
        }

        return string.Equals(examType, "PE", StringComparison.OrdinalIgnoreCase)
            ? "PE"
            : "FE";
    }

    private static string NormalizePresetScope(string? presetScope)
    {
        return "Course";
    }

    private static string BuildDefaultTitle(string courseCode, string examType, string mode, string presetScope)
    {
        return $"{courseCode} {examType} {mode} Course".Trim();
    }
}
