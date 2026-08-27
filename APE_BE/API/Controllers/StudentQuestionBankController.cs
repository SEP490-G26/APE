using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/student/question-bank")]
[Authorize]
public class StudentQuestionBankController : ControllerBase
{
    private readonly IFEQuestionRepository _feQuestionRepository;
    private readonly IPEQuestionRepository _peQuestionRepository;
    private readonly ICourseRepository _courseRepository;

    public StudentQuestionBankController(
        IFEQuestionRepository feQuestionRepository,
        IPEQuestionRepository peQuestionRepository,
        ICourseRepository courseRepository)
    {
        _feQuestionRepository = feQuestionRepository;
        _peQuestionRepository = peQuestionRepository;
        _courseRepository = courseRepository;
    }

    private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? courseId,
        [FromQuery] string? type,
        [FromQuery] string? difficulty,
        [FromQuery] string? status,
        [FromQuery] string[]? topicTags,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                return Unauthorized(ApiResponse.Fail("Unauthorized"));
            }

            page = Math.Max(page, 1);
            limit = Math.Clamp(limit, 1, 50);
            var courses = await BuildCourseMapAsync();

            if (string.Equals(type, "FE", StringComparison.OrdinalIgnoreCase))
            {
                var (items, total) = await _feQuestionRepository.ListOwnedByStudentAsync(CurrentUserId, courseId, difficulty, status, topicTags, keyword, page, limit);
                return Ok(ApiResponse.Ok(new
                {
                    items = items.Select(item => MapListItem(item, courses)).ToList(),
                    total,
                    page,
                    limit,
                    totalPages = (total + limit - 1) / limit
                }));
            }

            if (string.Equals(type, "PE", StringComparison.OrdinalIgnoreCase))
            {
                var (items, total) = await _peQuestionRepository.ListOwnedByStudentAsync(CurrentUserId, courseId, difficulty, status, topicTags, keyword, page, limit, cancellationToken);
                return Ok(ApiResponse.Ok(new
                {
                    items = items.Select(item => MapListItem(item, courses)).ToList(),
                    total,
                    page,
                    limit,
                    totalPages = (total + limit - 1) / limit
                }));
            }

            var expandedLimit = Math.Max(page * limit, limit);
            var (feItems, feTotal) = await _feQuestionRepository.ListOwnedByStudentAsync(CurrentUserId, courseId, difficulty, status, topicTags, keyword, 1, expandedLimit);
            var (peItems, peTotal) = await _peQuestionRepository.ListOwnedByStudentAsync(CurrentUserId, courseId, difficulty, status, topicTags, keyword, 1, expandedLimit, cancellationToken);

            var combined = feItems
                .Select(item => MapListItem(item, courses))
                .Concat(peItems.Select(item => MapListItem(item, courses)))
                .OrderByDescending(item => item.LastModifiedAt ?? DateTime.MinValue)
                .ThenBy(item => item.Title)
                .ToList();

            return Ok(ApiResponse.Ok(new
            {
                items = combined.Skip((page - 1) * limit).Take(limit).ToList(),
                total = feTotal + peTotal,
                page,
                limit,
                totalPages = (feTotal + peTotal + limit - 1) / limit
            }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("topic-tags")]
    public async Task<IActionResult> GetTopicTags([FromQuery] string? courseId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                return Unauthorized(ApiResponse.Fail("Unauthorized"));
            }

            var tags = (await _feQuestionRepository.GetOwnedStudentTopicTagsAsync(CurrentUserId, courseId))
                .Concat(await _peQuestionRepository.GetOwnedStudentTopicTagsAsync(CurrentUserId, courseId, cancellationToken))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(ApiResponse.Ok(tags));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                return Unauthorized(ApiResponse.Fail("Unauthorized"));
            }

            var courses = await BuildCourseMapAsync();
            var fe = await _feQuestionRepository.GetByIdAsync(id);
            if (fe is not null && CanView(fe))
            {
                return Ok(ApiResponse.Ok(MapDetail(fe, courses)));
            }

            var pe = await _peQuestionRepository.GetByIdAsync(id, cancellationToken);
            if (pe is not null && CanView(pe))
            {
                return Ok(ApiResponse.Ok(MapDetail(pe, courses)));
            }

            return NotFound(ApiResponse.Fail("Question not found"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                return Unauthorized(ApiResponse.Fail("Unauthorized"));
            }

            var fe = await _feQuestionRepository.GetByIdAsync(id);
            if (fe is not null)
            {
                if (!CanDelete(fe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                fe.Status = "Deleted";
                fe.IsPublic = false;
                fe.DisabledReason = "Deleted by student";
                fe.DisabledBy = CurrentUserId;
                fe.DisabledAt = DateTime.UtcNow;
                fe.LastModifiedBy = CurrentUserId;
                fe.LastModifiedAt = DateTime.UtcNow;
                await _feQuestionRepository.UpdateAsync(fe);
                return Ok(ApiResponse.Ok(new { id = fe.Id, type = "FE", status = fe.Status }));
            }

            var pe = await _peQuestionRepository.GetByIdAsync(id, cancellationToken);
            if (pe is not null)
            {
                if (!CanDelete(pe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                pe.Status = "Deleted";
                pe.IsPublic = false;
                pe.DisabledReason = "Deleted by student";
                pe.DisabledBy = CurrentUserId;
                pe.DisabledAt = DateTime.UtcNow;
                pe.LastModifiedBy = CurrentUserId;
                pe.LastModifiedAt = DateTime.UtcNow;
                await _peQuestionRepository.UpdateAsync(pe, cancellationToken);
                return Ok(ApiResponse.Ok(new { id = pe.Id, type = "PE", status = pe.Status }));
            }

            return NotFound(ApiResponse.Fail("Question not found"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/publish")]
    public async Task<IActionResult> Publish(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                return Unauthorized(ApiResponse.Fail("Unauthorized"));
            }

            var fe = await _feQuestionRepository.GetByIdAsync(id);
            if (fe is not null)
            {
                if (!CanPublish(fe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                fe.Status = "Active";
                fe.IsPublic = false;
                fe.DisabledReason = null;
                fe.DisabledBy = null;
                fe.DisabledAt = null;
                fe.LastModifiedBy = CurrentUserId;
                fe.LastModifiedAt = DateTime.UtcNow;
                await _feQuestionRepository.UpdateAsync(fe);
                return Ok(ApiResponse.Ok(new { id = fe.Id, type = "FE", status = fe.Status, canPublish = false }));
            }

            var pe = await _peQuestionRepository.GetByIdAsync(id, cancellationToken);
            if (pe is not null)
            {
                if (!CanPublish(pe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                pe.Status = "Active";
                pe.IsPublic = false;
                pe.DisabledReason = null;
                pe.DisabledBy = null;
                pe.DisabledAt = null;
                pe.LastModifiedBy = CurrentUserId;
                pe.LastModifiedAt = DateTime.UtcNow;
                await _peQuestionRepository.UpdateAsync(pe, cancellationToken);
                return Ok(ApiResponse.Ok(new { id = pe.Id, type = "PE", status = pe.Status, canPublish = false }));
            }

            return NotFound(ApiResponse.Fail("Question not found"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("publish-all")]
    public async Task<IActionResult> PublishAll(
        [FromQuery] string? courseId,
        [FromQuery] string? type,
        [FromQuery] string? difficulty,
        [FromQuery] string? status,
        [FromQuery] string[]? topicTags,
        [FromQuery] string? keyword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                return Unauthorized(ApiResponse.Fail("Unauthorized"));
            }

            var normalizedStatus = string.IsNullOrWhiteSpace(status) || string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)
                ? "Draft"
                : status.Trim();

            if (!string.Equals(normalizedStatus, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse.Fail("Bulk activation only supports draft questions."));
            }

            var updatedCount = 0;
            var skippedCount = 0;

            if (!string.Equals(type, "PE", StringComparison.OrdinalIgnoreCase))
            {
                var feItems = await _feQuestionRepository.ListOwnedByStudentAllAsync(CurrentUserId, courseId, difficulty, normalizedStatus, topicTags, keyword);
                foreach (var question in feItems)
                {
                    if (!CanPublish(question))
                    {
                        skippedCount += 1;
                        continue;
                    }

                    question.Status = "Active";
                    question.IsPublic = false;
                    question.DisabledReason = null;
                    question.DisabledBy = null;
                    question.DisabledAt = null;
                    question.LastModifiedBy = CurrentUserId;
                    question.LastModifiedAt = DateTime.UtcNow;
                    await _feQuestionRepository.UpdateAsync(question);
                    updatedCount += 1;
                }
            }

            if (!string.Equals(type, "FE", StringComparison.OrdinalIgnoreCase))
            {
                var peItems = await _peQuestionRepository.ListOwnedByStudentAllAsync(CurrentUserId, courseId, difficulty, normalizedStatus, topicTags, keyword, cancellationToken);
                foreach (var question in peItems)
                {
                    if (!CanPublish(question))
                    {
                        skippedCount += 1;
                        continue;
                    }

                    question.Status = "Active";
                    question.IsPublic = false;
                    question.DisabledReason = null;
                    question.DisabledBy = null;
                    question.DisabledAt = null;
                    question.LastModifiedBy = CurrentUserId;
                    question.LastModifiedAt = DateTime.UtcNow;
                    await _peQuestionRepository.UpdateAsync(question, cancellationToken);
                    updatedCount += 1;
                }
            }

            return Ok(ApiResponse.Ok(new
            {
                updatedCount,
                skippedCount
            }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    private async Task<Dictionary<string, Course>> BuildCourseMapAsync()
    {
        var courses = await _courseRepository.GetAllAsync();
        return courses
            .Where(course => string.Equals(course.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .GroupBy(course => course.Id)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private bool CanView(FEQuestion question) =>
        string.Equals(question.OwnerUserId, CurrentUserId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(question.Source, "Student", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(question.Status, "Deleted", StringComparison.OrdinalIgnoreCase);

    private bool CanView(PEQuestion question) =>
        string.Equals(question.OwnerUserId, CurrentUserId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(question.Source, "Student", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(question.Status, "Deleted", StringComparison.OrdinalIgnoreCase);

    private bool CanDelete(FEQuestion question) =>
        string.Equals(question.OwnerUserId, CurrentUserId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(question.Source, "Student", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(question.Status, "Deleted", StringComparison.OrdinalIgnoreCase);

    private bool CanDelete(PEQuestion question) =>
        string.Equals(question.OwnerUserId, CurrentUserId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(question.Source, "Student", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(question.Status, "Deleted", StringComparison.OrdinalIgnoreCase);

    private bool CanPublish(FEQuestion question) =>
        string.Equals(question.OwnerUserId, CurrentUserId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(question.Source, "Student", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(question.Status, "Draft", StringComparison.OrdinalIgnoreCase);

    private bool CanPublish(PEQuestion question) =>
        string.Equals(question.OwnerUserId, CurrentUserId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(question.Source, "Student", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(question.Status, "Draft", StringComparison.OrdinalIgnoreCase);

    private static StudentQuestionBankListItemDto MapListItem(FEQuestion question, IReadOnlyDictionary<string, Course> courses)
    {
        courses.TryGetValue(question.CourseId ?? string.Empty, out var course);
        return new StudentQuestionBankListItemDto
        {
            Id = question.Id,
            Type = "FE",
            Title = question.Title,
            Status = question.Status,
            CanPublish = string.Equals(question.Status, "Draft", StringComparison.OrdinalIgnoreCase),
            Difficulty = question.Difficulty,
            TopicTags = question.TopicTags ?? new List<string>(),
            CourseId = question.CourseId,
            CourseCode = course?.Code ?? string.Empty,
            CourseName = course?.Name ?? string.Empty,
            LastModifiedAt = question.LastModifiedAt ?? question.CreatedAt
        };
    }

    private static StudentQuestionBankListItemDto MapListItem(PEQuestion question, IReadOnlyDictionary<string, Course> courses)
    {
        courses.TryGetValue(question.CourseId ?? string.Empty, out var course);
        return new StudentQuestionBankListItemDto
        {
            Id = question.Id,
            Type = "PE",
            Title = question.Title,
            Status = question.Status,
            CanPublish = string.Equals(question.Status, "Draft", StringComparison.OrdinalIgnoreCase),
            Difficulty = question.Difficulty,
            TopicTags = question.TopicTags ?? new List<string>(),
            CourseId = question.CourseId,
            CourseCode = course?.Code ?? string.Empty,
            CourseName = course?.Name ?? string.Empty,
            LastModifiedAt = question.LastModifiedAt ?? question.CreatedAt
        };
    }

    private static StudentQuestionBankDetailDto MapDetail(FEQuestion question, IReadOnlyDictionary<string, Course> courses)
    {
        courses.TryGetValue(question.CourseId ?? string.Empty, out var course);
        return new StudentQuestionBankDetailDto
        {
            Id = question.Id,
            Type = "FE",
            Title = question.Title,
            Status = question.Status,
            CanPublish = string.Equals(question.Status, "Draft", StringComparison.OrdinalIgnoreCase),
            Description = question.Description,
            Difficulty = question.Difficulty,
            TopicTags = question.TopicTags ?? new List<string>(),
            CourseId = question.CourseId,
            CourseCode = course?.Code ?? string.Empty,
            CourseName = course?.Name ?? string.Empty,
            Options = question.Options ?? new List<string>(),
            CorrectAnswer = question.CorrectAnswer ?? new List<string>(),
            Explanation = question.Explanation
        };
    }

    private static StudentQuestionBankDetailDto MapDetail(PEQuestion question, IReadOnlyDictionary<string, Course> courses)
    {
        courses.TryGetValue(question.CourseId ?? string.Empty, out var course);
        return new StudentQuestionBankDetailDto
        {
            Id = question.Id,
            Type = "PE",
            Title = question.Title,
            Status = question.Status,
            CanPublish = string.Equals(question.Status, "Draft", StringComparison.OrdinalIgnoreCase),
            Description = question.Description,
            Difficulty = question.Difficulty,
            TopicTags = question.TopicTags ?? new List<string>(),
            CourseId = question.CourseId,
            CourseCode = course?.Code ?? string.Empty,
            CourseName = course?.Name ?? string.Empty,
            SkeletonCode = (question.SkeletonCode ?? new List<CodeFile>())
                .Select(file => new CodeFileDto
                {
                    Filename = file.Filename,
                    Content = file.Content
                })
                .ToList(),
            SampleTestCases = (question.TestCases ?? new List<TestCase>())
                .Where(testCase => !testCase.IsHidden)
                .Select(testCase => new TestCaseDto
                {
                    Input = testCase.Input,
                    ExpectedOutput = testCase.ExpectedOutput
                })
                .ToList()
        };
    }
}
