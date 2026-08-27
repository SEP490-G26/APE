using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/ai/questions")]
[Authorize]
public class AIQuestionGenerationController : ControllerBase
{
    private const int MaxDifficultyBuckets = 3;

    private readonly IQuestionGenerationReviewService _questionGenerationReviewService;
    private readonly IFEQuestionRepository _feQuestionRepository;
    private readonly IPEQuestionRepository _peQuestionRepository;

    public AIQuestionGenerationController(
        IQuestionGenerationReviewService questionGenerationReviewService,
        IFEQuestionRepository feQuestionRepository,
        IPEQuestionRepository peQuestionRepository)
    {
        _questionGenerationReviewService = questionGenerationReviewService;
        _feQuestionRepository = feQuestionRepository;
        _peQuestionRepository = peQuestionRepository;
    }

    private string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpPost("generate-review")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<GenerationReviewResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GenerateReview([FromBody] GenerationReviewRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        dto.UserId = UserId;
        dto.UseActualCostOnly = IsAdmin;
        var result = await _questionGenerationReviewService.RunAsync(dto, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("generate-review/system")]
    [ProducesResponseType(typeof(ApiResponse<GenerationReviewResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GenerateReviewFromSystemPool([FromBody] SystemQuestionGenerationRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var validationError = ValidateSystemProductionRequest(dto, IsAdmin);
        if (validationError is not null)
        {
            return BadRequest(ApiResponse.Fail(validationError));
        }

        var request = new GenerationReviewRequestDto
        {
            UserId = UserId,
            CourseId = dto.CourseId,
            DocumentId = dto.DocumentId,
            Subject = dto.Subject,
            DifficultyProfile = dto.DifficultyProfile,
            DifficultyMode = dto.DifficultyMode,
            Difficulty = dto.Difficulty,
            QuestionType = dto.QuestionType,
            Count = dto.Count,
            Mode = dto.Mode,
            MaxAttempts = dto.MaxAttempts,
            PersistQuestions = dto.PersistQuestions,
            IsPublic = dto.IsPublic,
            UseActualCostOnly = IsAdmin,
            GeneratorOverride = dto.GeneratorOverride,
            ReviewerOverride = dto.ReviewerOverride,
            Retrieval = new RetrievalPlanRequestDto
            {
                UserId = UserId,
                CourseId = dto.CourseId,
                DocumentId = dto.DocumentId,
                ChapterKey = dto.ChapterKey,
                ChapterKeys = ResolveRequestedChapterKeys(dto.ChapterKey, dto.ChapterKeys),
                Subject = dto.Subject,
                QuestionType = dto.QuestionType,
                Difficulty = dto.Difficulty,
                RequestedQuestionCount = ResolveRequestedQuestionCount(dto.DifficultyProfile, dto.Count),
                GenerationMode = dto.Mode,
                TargetTopics = dto.TargetTopics,
                SourceScope = "SYSTEM",
                Language = dto.Language,
                MaxPackedTokens = dto.MaxPackedTokens ?? 2400,
                RetrievalQuery = dto.RetrievalQuery,
                AllowExtendedScope = IsAdmin
            }
        };

        var result = await _questionGenerationReviewService.RunAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("generate-review/byos")]
    [ProducesResponseType(typeof(ApiResponse<GenerationReviewResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GenerateReviewFromByosPool([FromBody] ByosQuestionGenerationRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var validationError = ValidateByosProductionRequest(dto);
        if (validationError is not null)
        {
            return BadRequest(ApiResponse.Fail(validationError));
        }

        var request = new GenerationReviewRequestDto
        {
            UserId = UserId,
            DocumentId = dto.DocumentId,
            Subject = dto.Subject,
            DifficultyProfile = dto.DifficultyProfile,
            DifficultyMode = dto.DifficultyMode,
            Difficulty = dto.Difficulty,
            QuestionType = dto.QuestionType,
            Count = dto.Count,
            Mode = dto.Mode,
            MaxAttempts = dto.MaxAttempts,
            PersistQuestions = dto.PersistQuestions,
            IsPublic = dto.IsPublic,
            UseActualCostOnly = IsAdmin,
            GeneratorOverride = dto.GeneratorOverride,
            ReviewerOverride = dto.ReviewerOverride,
            Retrieval = new RetrievalPlanRequestDto
            {
                UserId = UserId,
                DocumentId = dto.DocumentId,
                ChapterKey = dto.ChapterKey,
                ChapterKeys = ResolveRequestedChapterKeys(dto.ChapterKey, dto.ChapterKeys),
                Subject = dto.Subject,
                QuestionType = dto.QuestionType,
                Difficulty = dto.Difficulty,
                RequestedQuestionCount = ResolveRequestedQuestionCount(dto.DifficultyProfile, dto.Count),
                GenerationMode = dto.Mode,
                TargetTopics = dto.TargetTopics,
                SourceScope = "BYOS",
                Language = dto.Language,
                MaxPackedTokens = dto.MaxPackedTokens ?? 2400,
                RetrievalQuery = dto.RetrievalQuery
            }
        };

        var result = await _questionGenerationReviewService.RunAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("generated/{id}/publish")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishGeneratedQuestion(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var fe = await _feQuestionRepository.GetByIdAsync(id);
        if (fe is not null)
        {
            if (!CanManageQuestion(fe.CreatedBy, fe.OwnerUserId))
            {
                return Forbid();
            }

            fe.Status = "Active";
            fe.IsPublic = IsAdmin && string.Equals(fe.Source, "Admin", StringComparison.OrdinalIgnoreCase);
            fe.DisabledReason = null;
            fe.DisabledBy = null;
            fe.DisabledAt = null;
            fe.LastModifiedBy = UserId;
            fe.LastModifiedAt = DateTime.UtcNow;
            await _feQuestionRepository.UpdateAsync(fe);
            return Ok(ApiResponse.Ok(new
            {
                id = fe.Id,
                status = fe.Status,
                isPublic = fe.IsPublic
            }));
        }

        var pe = await _peQuestionRepository.GetByIdAsync(id, cancellationToken);
        if (pe is not null)
        {
            if (!CanManageQuestion(pe.CreatedBy, pe.OwnerUserId))
            {
                return Forbid();
            }

            pe.Status = "Active";
            pe.IsPublic = IsAdmin && string.Equals(pe.Source, "Admin", StringComparison.OrdinalIgnoreCase);
            pe.DisabledReason = null;
            pe.DisabledBy = null;
            pe.DisabledAt = null;
            pe.LastModifiedBy = UserId;
            pe.LastModifiedAt = DateTime.UtcNow;
            await _peQuestionRepository.UpdateAsync(pe, cancellationToken);
            return Ok(ApiResponse.Ok(new
            {
                id = pe.Id,
                status = pe.Status,
                isPublic = pe.IsPublic
            }));
        }

        return NotFound(ApiResponse.Fail("Generated question not found."));
    }

    [HttpDelete("generated/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGeneratedQuestion(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized"));
        }

        var fe = await _feQuestionRepository.GetByIdAsync(id);
        if (fe is not null)
        {
            if (!CanManageQuestion(fe.CreatedBy, fe.OwnerUserId))
            {
                return Forbid();
            }

            if (string.Equals(fe.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                await _feQuestionRepository.DeleteAsync(id);
                return Ok(ApiResponse.Ok(new { id, deleted = true, action = "hard_delete" }));
            }

            fe.Status = "Disabled";
            fe.DisabledReason = "Removed from generated question review.";
            fe.DisabledBy = UserId;
            fe.DisabledAt = DateTime.UtcNow;
            fe.LastModifiedBy = UserId;
            fe.LastModifiedAt = DateTime.UtcNow;
            await _feQuestionRepository.UpdateAsync(fe);
            return Ok(ApiResponse.Ok(new
            {
                id = fe.Id,
                deleted = false,
                action = "soft_delete",
                status = fe.Status
            }));
        }

        var pe = await _peQuestionRepository.GetByIdAsync(id, cancellationToken);
        if (pe is not null)
        {
            if (!CanManageQuestion(pe.CreatedBy, pe.OwnerUserId))
            {
                return Forbid();
            }

            if (string.Equals(pe.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                await _peQuestionRepository.DeleteAsync(id, cancellationToken);
                return Ok(ApiResponse.Ok(new { id, deleted = true, action = "hard_delete" }));
            }

            pe.Status = "Disabled";
            pe.DisabledReason = "Removed from generated question review.";
            pe.DisabledBy = UserId;
            pe.DisabledAt = DateTime.UtcNow;
            pe.LastModifiedBy = UserId;
            pe.LastModifiedAt = DateTime.UtcNow;
            await _peQuestionRepository.UpdateAsync(pe, cancellationToken);
            return Ok(ApiResponse.Ok(new
            {
                id = pe.Id,
                deleted = false,
                action = "soft_delete",
                status = pe.Status
            }));
        }

        return NotFound(ApiResponse.Fail("Generated question not found."));
    }

    private bool CanManageQuestion(string? createdBy, string? ownerUserId)
    {
        if (IsAdmin)
        {
            return true;
        }

        return string.Equals(createdBy, UserId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ownerUserId, UserId, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ValidateSystemProductionRequest(SystemQuestionGenerationRequestDto dto, bool allowExtendedScope)
    {
        if (string.IsNullOrWhiteSpace(dto.CourseId))
        {
            return "SYSTEM generation requires courseId.";
        }

        if (string.IsNullOrWhiteSpace(dto.DocumentId))
        {
            return "SYSTEM generation requires documentId.";
        }

        if (string.IsNullOrWhiteSpace(dto.Subject))
        {
            return "SYSTEM generation requires subject.";
        }

        var chapterKeys = ResolveRequestedChapterKeys(dto.ChapterKey, dto.ChapterKeys);
        if (chapterKeys.Count == 0)
        {
            return "SYSTEM generation requires at least one chapter.";
        }

        if (!allowExtendedScope && chapterKeys.Count > 3)
        {
            return "SYSTEM generation supports at most 3 chapters per request in this phase.";
        }

        if (dto.TargetTopics is null || dto.TargetTopics.Count == 0)
        {
            return "SYSTEM generation requires at least one target topic.";
        }

        if (!allowExtendedScope && dto.TargetTopics.Count > 5)
        {
            return "SYSTEM generation supports at most 5 target topics.";
        }

        if (!IsSupportedQuestionType(dto.QuestionType))
        {
            return "QuestionType must be FE or PE.";
        }

        var difficultyValidation = ValidateDifficultySelection(dto.Difficulty, dto.DifficultyProfile, dto.Count, dto.QuestionType);
        if (difficultyValidation is not null)
        {
            return difficultyValidation;
        }

        return null;
    }

    private static string? ValidateByosProductionRequest(ByosQuestionGenerationRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DocumentId))
        {
            return "BYOS generation requires documentId.";
        }

        if (string.IsNullOrWhiteSpace(dto.Subject))
        {
            return "BYOS generation requires subject.";
        }

        var chapterKeys = ResolveRequestedChapterKeys(dto.ChapterKey, dto.ChapterKeys);
        if (chapterKeys.Count == 0)
        {
            return "BYOS generation requires at least one chapter.";
        }

        if (chapterKeys.Count > 3)
        {
            return "BYOS generation supports at most 3 chapters per request in this phase.";
        }

        if (dto.TargetTopics is null || dto.TargetTopics.Count == 0)
        {
            return "BYOS generation requires at least one target topic.";
        }

        if (dto.TargetTopics.Count > 5)
        {
            return "BYOS generation supports at most 5 target topics.";
        }

        if (!IsSupportedQuestionType(dto.QuestionType))
        {
            return "QuestionType must be FE or PE.";
        }

        var difficultyValidation = ValidateDifficultySelection(dto.Difficulty, dto.DifficultyProfile, dto.Count, dto.QuestionType);
        if (difficultyValidation is not null)
        {
            return difficultyValidation;
        }

        return null;
    }

    private static string? ValidateDifficultySelection(
        string? difficulty,
        IReadOnlyList<DifficultyDistributionItemDto>? difficultyProfile,
        int declaredCount,
        string? questionType)
    {
        var normalizedProfile = NormalizeDifficultyProfile(difficultyProfile);
        if (normalizedProfile.Count == 0)
        {
            return string.IsNullOrWhiteSpace(difficulty)
                ? "Single-difficulty generation requires difficulty."
                : ValidateRequestedCount(questionType, declaredCount, isMixed: false);
        }

        if (normalizedProfile.Count > MaxDifficultyBuckets)
        {
            return $"Generation supports at most {MaxDifficultyBuckets} difficulty buckets per request.";
        }

        var distinctDifficultyCount = normalizedProfile
            .Select(item => item.Difficulty.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (distinctDifficultyCount != normalizedProfile.Count)
        {
            return "difficultyProfile must not contain duplicate difficulties.";
        }

        var totalRequested = normalizedProfile.Sum(item => item.Count);
        if (declaredCount > 0 && totalRequested != declaredCount)
        {
            return "For mixed-difficulty generation, count must equal the sum of difficultyProfile counts.";
        }

        return ValidateRequestedCount(questionType, totalRequested, isMixed: normalizedProfile.Count > 1, normalizedProfile);
    }

    private static int ResolveRequestedQuestionCount(IReadOnlyList<DifficultyDistributionItemDto>? difficultyProfile, int fallbackCount)
        => NormalizeDifficultyProfile(difficultyProfile).Sum(item => item.Count) is var total && total > 0
            ? total
            : fallbackCount;

    private static List<DifficultyDistributionItemDto> NormalizeDifficultyProfile(IReadOnlyList<DifficultyDistributionItemDto>? difficultyProfile)
        => (difficultyProfile ?? Array.Empty<DifficultyDistributionItemDto>())
            .Where(item => item is not null && item.Count > 0 && !string.IsNullOrWhiteSpace(item.Difficulty))
            .Select(item => new DifficultyDistributionItemDto
            {
                Difficulty = item.Difficulty.Trim(),
                Count = item.Count
            })
            .ToList();

    private static string? ValidateRequestedCount(
        string? questionType,
        int requestedCount,
        bool isMixed,
        IReadOnlyList<DifficultyDistributionItemDto>? difficultyProfile = null)
    {
        if (!isMixed)
        {
            return IsCountInRange(questionType, requestedCount)
                ? null
                : BuildCountValidationError(questionType, isMixed: false);
        }

        if (string.Equals(questionType, "FE", StringComparison.OrdinalIgnoreCase))
        {
            if (requestedCount < 10 || requestedCount > 50)
            {
                return BuildCountValidationError(questionType, isMixed: true);
            }

            return null;
        }

        if (string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            if (requestedCount < 1 || requestedCount > 5)
            {
                return BuildCountValidationError(questionType, isMixed: true);
            }

            return null;
        }

        return "QuestionType must be FE or PE.";
    }

    private static bool IsSupportedQuestionType(string? questionType)
        => string.Equals(questionType, "FE", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase);

    private static bool IsCountInRange(string? questionType, int count)
    {
        if (string.Equals(questionType, "FE", StringComparison.OrdinalIgnoreCase))
        {
            return count >= 10 && count <= 50;
        }

        if (string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return count >= 1 && count <= 5;
        }

        return false;
    }

    private static string BuildCountValidationError(string? questionType, bool isMixed)
    {
        if (string.Equals(questionType, "FE", StringComparison.OrdinalIgnoreCase))
        {
            return isMixed
                ? "FE mixed generation requires total count from 10 to 50."
                : "FE generation requires count from 10 to 50.";
        }

        if (string.Equals(questionType, "PE", StringComparison.OrdinalIgnoreCase))
        {
            return isMixed
                ? "PE mixed generation requires total count from 1 to 5."
                : "PE generation requires count from 1 to 5.";
        }

        return "QuestionType must be FE or PE.";
    }

    private static List<string> ResolveRequestedChapterKeys(string? chapterKey, IReadOnlyList<string>? chapterKeys)
        => new[] { chapterKey }
            .Concat(chapterKeys ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
