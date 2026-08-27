using System.Security.Claims;
using Application.DTOs;
using Application.Interfaces;
using Application.Common;
using Domain.Entities;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Services;
using Microsoft.AspNetCore.Http;
namespace API.Controllers;

[ApiController]
[Route("api/admin/questions")]
[Authorize(Roles = "Admin")]
public class QuestionController : ControllerBase
{
    private readonly IFEQuestionRepository _feRepo;
    private readonly IPEQuestionRepository _peRepo;
    private readonly QuestionImportService _importService;  

    public QuestionController(
        IFEQuestionRepository feRepo,
        IPEQuestionRepository peRepo,
        QuestionImportService importService)    
    {
        _feRepo = feRepo;
        _peRepo = peRepo;
        _importService = importService;
    }

    private string? AdminId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    private static bool IsAdminOwnedQuestion(FEQuestion question) =>
        string.Equals(question.Source, "Admin", StringComparison.OrdinalIgnoreCase);

    private static bool IsAdminOwnedQuestion(PEQuestion question) =>
        string.Equals(question.Source, "Admin", StringComparison.OrdinalIgnoreCase);

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        try
        {
            var fe = await _feRepo.GetByIdAsync(id);
            if (fe != null && IsAdminOwnedQuestion(fe))
            {
                return Ok(ApiResponse.Ok(MapQuestion(fe)));
            }

            var pe = await _peRepo.GetByIdAsync(id);
            if (pe != null && IsAdminOwnedQuestion(pe))
            {
                return Ok(ApiResponse.Ok(MapQuestion(pe)));
            }

            return NotFound(ApiResponse.Fail("Question not found"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? courseId, [FromQuery] string? type, [FromQuery] string? difficulty, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        try
        {
            limit = limit <= 0 ? 20 : limit; // BR-91 default
            page = page <= 0 ? 1 : page;

            if (string.Equals(type, "FE", StringComparison.OrdinalIgnoreCase))
            {
                var (items, totalCount) = await _feRepo.ListAdminOwnedAsync(status, courseId, difficulty, page, limit);
                var dtos = items
                    .Select(i => new QuestionSummaryDto
                {
                    Id = i.Id,
                    Type = "FE",
                    Title = i.Title,
                    Difficulty = i.Difficulty,
                    Status = i.Status,
                    CourseId = i.CourseId,
                    TopicTags = i.TopicTags,
                    Source = i.Source,
                    SourceScope = i.SourceScope,
                    OwnerUserId = i.OwnerUserId,
                    QuestionFingerprint = i.QuestionFingerprint,
                    SourceDocumentCount = i.SourceDocumentIds?.Count ?? 0,
                    CreatedBy = i.CreatedBy,
                    LastModifiedAt = i.LastModifiedAt
                }).ToList();

                var resp = new { items = dtos, total = totalCount, page, limit, totalPages = (totalCount + limit - 1) / limit };
                return Ok(ApiResponse.Ok(resp));
            }
            else if (string.Equals(type, "PE", StringComparison.OrdinalIgnoreCase))
            {
                var (items, totalCount) = await _peRepo.ListAdminOwnedAsync(status, courseId, difficulty, page, limit);
                var dtos = items
                    .Select(i => new QuestionSummaryDto
                {
                    Id = i.Id,
                    Type = "PE",
                    Title = i.Title,
                    Difficulty = i.Difficulty,
                    Status = i.Status,
                    CourseId = i.CourseId,
                    TopicTags = i.TopicTags,
                    Source = i.Source,
                    SourceScope = i.SourceScope,
                    OwnerUserId = i.OwnerUserId,
                    QuestionFingerprint = i.QuestionFingerprint,
                    SourceDocumentCount = i.SourceDocumentIds?.Count ?? 0,
                    CreatedBy = i.CreatedBy,
                    LastModifiedAt = i.LastModifiedAt
                }).ToList();

                var resp = new { items = dtos, total = totalCount, page, limit, totalPages = (totalCount + limit - 1) / limit };
                return Ok(ApiResponse.Ok(resp));
            }
            else
            {
                var expandedLimit = Math.Max(page * limit, limit);
                var (feItems, feTotal) = await _feRepo.ListAdminOwnedAsync(status, courseId, difficulty, 1, expandedLimit);
                var (peItems, peTotal) = await _peRepo.ListAdminOwnedAsync(status, courseId, difficulty, 1, expandedLimit);
                var feDtos = feItems
                    .Select(i => new QuestionSummaryDto
                {
                    Id = i.Id,
                    Type = "FE",
                    Title = i.Title,
                    Difficulty = i.Difficulty,
                    Status = i.Status,
                    CourseId = i.CourseId,
                    TopicTags = i.TopicTags,
                    Source = i.Source,
                    SourceScope = i.SourceScope,
                    OwnerUserId = i.OwnerUserId,
                    QuestionFingerprint = i.QuestionFingerprint,
                    SourceDocumentCount = i.SourceDocumentIds?.Count ?? 0,
                    CreatedBy = i.CreatedBy,
                    LastModifiedAt = i.LastModifiedAt
                }).ToList();
                var peDtos = peItems
                    .Select(i => new QuestionSummaryDto
                {
                    Id = i.Id,
                    Type = "PE",
                    Title = i.Title,
                    Difficulty = i.Difficulty,
                    Status = i.Status,
                    CourseId = i.CourseId,
                    TopicTags = i.TopicTags,
                    Source = i.Source,
                    SourceScope = i.SourceScope,
                    OwnerUserId = i.OwnerUserId,
                    QuestionFingerprint = i.QuestionFingerprint,
                    SourceDocumentCount = i.SourceDocumentIds?.Count ?? 0,
                    CreatedBy = i.CreatedBy,
                    LastModifiedAt = i.LastModifiedAt
                }).ToList();

                var combined = feDtos
                    .Concat(peDtos)
                    .OrderByDescending(item => item.LastModifiedAt ?? DateTime.MinValue)
                    .ThenBy(item => item.Title ?? string.Empty)
                    .ToList();

                var paged = combined
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .ToList();

                var resp = new
                {
                    items = paged,
                    total = combined.Count,
                    page,
                    limit,
                    totalPages = (combined.Count + limit - 1) / limit
                };
                return Ok(ApiResponse.Ok(resp));
            }
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<ImportResultDto>), 200)]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Please choose an Excel file."));

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (ext != ".xlsx")
            return BadRequest(ApiResponse.Fail("Only .xlsx files are supported."));

        using var stream = file.OpenReadStream();
        var adminId = AdminId ?? "system";
        var result = await _importService.ImportFromExcelAsync(stream, adminId);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("import-template")]
    [Authorize(Roles = "Admin")]
    public IActionResult DownloadImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Questions");

        // Column order must match QuestionImportService expectations:
        // 1 Title, 2 Description, 3 Options JSON, 4 CorrectAnswer JSON, 5 Explanation, 6 Difficulty, 7 TopicTags, 8 CourseCode
        var headers = new[]
        {
            "Title",
            "Description",
            "Options",
            "CorrectAnswer",
            "Explanation",
            "Difficulty (Easy|Medium|Hard)",
            "TopicTags (comma separated)",
            "CourseCode"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        sheet.Cell(2, 1).Value = "Sample: Which statement best describes a local variable declared inside a function in C?";
        sheet.Cell(2, 2).Value = "Choose the correct answer.";
        sheet.Cell(2, 3).Value = "[\"A. It can be accessed from any file in the project\",\"B. It is available only inside that function\",\"C. It is stored permanently even after the program ends\",\"D. It must always be declared with the static keyword\"]";
        sheet.Cell(2, 4).Value = "[\"B. It is available only inside that function\"]";
        sheet.Cell(2, 5).Value = "A local variable declared inside a function is visible only within that function block and cannot be accessed directly from outside it.";
        sheet.Cell(2, 6).Value = "Easy";
        sheet.Cell(2, 7).Value = "variables,scope,function";
        sheet.Cell(2, 8).Value = "PRF192";

        sheet.Cell(3, 1).Value = "(Leave this row as an example or replace it)";

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        sheet.Row(1).Style.Font.Bold = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        const string fileName = "fe_questions_import_template.xlsx";
        return File(stream.ToArray(), contentType, fileName);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(string id, [FromBody] EditQuestionDto dto)
    {
        try
        {
            // Try FE first
            var fe = await _feRepo.GetByIdAsync(id);
            if (fe != null)
            {
                if (!IsAdminOwnedQuestion(fe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                if (!string.IsNullOrWhiteSpace(dto.Title)) fe.Title = dto.Title;
                if (!string.IsNullOrWhiteSpace(dto.Description)) fe.Description = dto.Description;
                if (dto.TopicTags != null) fe.TopicTags = dto.TopicTags;
                if (!string.IsNullOrWhiteSpace(dto.Difficulty)) fe.Difficulty = dto.Difficulty;
                if (dto.Options != null) fe.Options = dto.Options;
                if (dto.CorrectAnswer != null) fe.CorrectAnswer = dto.CorrectAnswer;

                fe.LastModifiedBy = AdminId;
                fe.LastModifiedAt = DateTime.UtcNow;

                await _feRepo.UpdateAsync(fe);
                return Ok(ApiResponse.Ok(new QuestionAdminMutationResultDto
                {
                    Action = "edit",
                    Message = "Question updated successfully.",
                    Question = MapQuestion(fe)
                }));
            }

            var pe = await _peRepo.GetByIdAsync(id);
            if (pe != null)
            {
                if (!IsAdminOwnedQuestion(pe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                if (!string.IsNullOrWhiteSpace(dto.Title)) pe.Title = dto.Title;
                if (!string.IsNullOrWhiteSpace(dto.Description)) pe.Description = dto.Description;
                if (dto.TopicTags != null) pe.TopicTags = dto.TopicTags;
                if (!string.IsNullOrWhiteSpace(dto.Difficulty)) pe.Difficulty = dto.Difficulty;
                if (dto.SkeletonCode is not null)
                {
                    pe.SkeletonCode = dto.SkeletonCode
                        .Select(file => CodeFile.Create(
                            file.Filename,
                            file.Content))
                        .ToList();
                }

                if (dto.TestCases is not null)
                {
                    pe.TestCases = dto.TestCases
                        .Select(testCase => new TestCase
                        {
                            Input = testCase.Input,
                            ExpectedOutput = testCase.ExpectedOutput,
                            IsHidden = testCase.IsHidden
                        })
                        .ToList();
                }

                pe.LastModifiedBy = AdminId;
                pe.LastModifiedAt = DateTime.UtcNow;

                await _peRepo.UpdateAsync(pe);
                return Ok(ApiResponse.Ok(new QuestionAdminMutationResultDto
                {
                    Action = "edit",
                    Message = "Question updated successfully.",
                    Question = MapQuestion(pe)
                }));
            }

            return NotFound(ApiResponse.Fail("Question not found"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/disable")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Disable(string id, [FromBody] DisableQuestionDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest(ApiResponse.Fail("Reason is required")); // BR-95

            var fe = await _feRepo.GetByIdAsync(id);
            if (fe != null)
            {
                if (!IsAdminOwnedQuestion(fe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                var now = DateTime.UtcNow;
                if (string.Equals(fe.Status, "Disabled", StringComparison.OrdinalIgnoreCase))
                {
                    // re-enable
                    fe.Status = "Active";
                    fe.DisabledReason = null;
                    fe.DisabledBy = null;
                    fe.DisabledAt = null;
                }
                else
                {
                    fe.Status = "Disabled";
                    fe.DisabledReason = dto.Reason;
                    fe.DisabledBy = AdminId;
                    fe.DisabledAt = now;
                }

                fe.LastModifiedBy = AdminId;
                fe.LastModifiedAt = DateTime.UtcNow;

                await _feRepo.UpdateAsync(fe);
                return Ok(ApiResponse.Ok(new QuestionAdminMutationResultDto
                {
                    Action = string.Equals(fe.Status, "Disabled", StringComparison.OrdinalIgnoreCase) ? "disable" : "enable",
                    Message = string.Equals(fe.Status, "Disabled", StringComparison.OrdinalIgnoreCase)
                        ? "Question disabled successfully."
                        : "Question re-enabled successfully.",
                    Question = MapQuestion(fe)
                }));
            }

            var pe = await _peRepo.GetByIdAsync(id);
            if (pe != null)
            {
                if (!IsAdminOwnedQuestion(pe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                var now = DateTime.UtcNow;
                if (string.Equals(pe.Status, "Disabled", StringComparison.OrdinalIgnoreCase))
                {
                    pe.Status = "Active";
                    pe.DisabledReason = null;
                    pe.DisabledBy = null;
                    pe.DisabledAt = null;
                }
                else
                {
                    pe.Status = "Disabled";
                    pe.DisabledReason = dto.Reason;
                    pe.DisabledBy = AdminId;
                    pe.DisabledAt = now;
                }

                pe.LastModifiedBy = AdminId;
                pe.LastModifiedAt = DateTime.UtcNow;

                await _peRepo.UpdateAsync(pe);
                return Ok(ApiResponse.Ok(new QuestionAdminMutationResultDto
                {
                    Action = string.Equals(pe.Status, "Disabled", StringComparison.OrdinalIgnoreCase) ? "disable" : "enable",
                    Message = string.Equals(pe.Status, "Disabled", StringComparison.OrdinalIgnoreCase)
                        ? "Question disabled successfully."
                        : "Question re-enabled successfully.",
                    Question = MapQuestion(pe)
                }));
            }

            return NotFound(ApiResponse.Fail("Question not found"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Publish(string id)
    {
        try
        {
            var fe = await _feRepo.GetByIdAsync(id);
            if (fe != null)
            {
                if (!IsAdminOwnedQuestion(fe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                fe.Status = "Active";
                fe.IsPublic = string.Equals(fe.Source, "Admin", StringComparison.OrdinalIgnoreCase);
                fe.DisabledReason = null;
                fe.DisabledBy = null;
                fe.DisabledAt = null;
                fe.LastModifiedBy = AdminId;
                fe.LastModifiedAt = DateTime.UtcNow;
                await _feRepo.UpdateAsync(fe);
                return Ok(ApiResponse.Ok(new QuestionAdminMutationResultDto
                {
                    Action = "publish",
                    Message = "Question published successfully.",
                    Question = MapQuestion(fe)
                }));
            }

            var pe = await _peRepo.GetByIdAsync(id);
            if (pe != null)
            {
                if (!IsAdminOwnedQuestion(pe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                pe.Status = "Active";
                pe.IsPublic = string.Equals(pe.Source, "Admin", StringComparison.OrdinalIgnoreCase);
                pe.DisabledReason = null;
                pe.DisabledBy = null;
                pe.DisabledAt = null;
                pe.LastModifiedBy = AdminId;
                pe.LastModifiedAt = DateTime.UtcNow;
                await _peRepo.UpdateAsync(pe);
                return Ok(ApiResponse.Ok(new QuestionAdminMutationResultDto
                {
                    Action = "publish",
                    Message = "Question published successfully.",
                    Question = MapQuestion(pe)
                }));
            }

            return NotFound(ApiResponse.Fail("Question not found"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("publish-all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PublishAll(
        [FromQuery] string? courseId,
        [FromQuery] string? type,
        [FromQuery] string? difficulty,
        [FromQuery] string? status,
        CancellationToken cancellationToken = default)
    {
        try
        {
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
                var feItems = await _feRepo.ListAdminOwnedAllAsync(normalizedStatus, courseId, difficulty);
                foreach (var question in feItems)
                {
                    if (!IsAdminOwnedQuestion(question) || !string.Equals(question.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                    {
                        skippedCount += 1;
                        continue;
                    }

                    question.Status = "Active";
                    question.IsPublic = true;
                    question.DisabledReason = null;
                    question.DisabledBy = null;
                    question.DisabledAt = null;
                    question.LastModifiedBy = AdminId;
                    question.LastModifiedAt = DateTime.UtcNow;
                    await _feRepo.UpdateAsync(question);
                    updatedCount += 1;
                }
            }

            if (!string.Equals(type, "FE", StringComparison.OrdinalIgnoreCase))
            {
                var peItems = await _peRepo.ListAdminOwnedAllAsync(normalizedStatus, courseId, difficulty, cancellationToken);
                foreach (var question in peItems)
                {
                    if (!IsAdminOwnedQuestion(question) || !string.Equals(question.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                    {
                        skippedCount += 1;
                        continue;
                    }

                    question.Status = "Active";
                    question.IsPublic = true;
                    question.DisabledReason = null;
                    question.DisabledBy = null;
                    question.DisabledAt = null;
                    question.LastModifiedBy = AdminId;
                    question.LastModifiedAt = DateTime.UtcNow;
                    await _peRepo.UpdateAsync(question, cancellationToken);
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

    [HttpPut("{id}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reject(string id, [FromBody] PublishQuestionDto dto)
    {
        try
        {
            var reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Rejected by admin" : dto.Reason.Trim();

            var fe = await _feRepo.GetByIdAsync(id);
            if (fe != null)
            {
                if (!IsAdminOwnedQuestion(fe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                fe.Status = "Disabled";
                fe.DisabledReason = reason;
                fe.DisabledBy = AdminId;
                fe.DisabledAt = DateTime.UtcNow;
                fe.LastModifiedBy = AdminId;
                fe.LastModifiedAt = DateTime.UtcNow;
                await _feRepo.UpdateAsync(fe);
                return Ok(ApiResponse.Ok(new QuestionAdminMutationResultDto
                {
                    Action = "reject",
                    Message = "Question rejected successfully.",
                    Question = MapQuestion(fe)
                }));
            }

            var pe = await _peRepo.GetByIdAsync(id);
            if (pe != null)
            {
                if (!IsAdminOwnedQuestion(pe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                pe.Status = "Disabled";
                pe.DisabledReason = reason;
                pe.DisabledBy = AdminId;
                pe.DisabledAt = DateTime.UtcNow;
                pe.LastModifiedBy = AdminId;
                pe.LastModifiedAt = DateTime.UtcNow;
                await _peRepo.UpdateAsync(pe);
                return Ok(ApiResponse.Ok(new QuestionAdminMutationResultDto
                {
                    Action = "reject",
                    Message = "Question rejected successfully.",
                    Question = MapQuestion(pe)
                }));
            }

            return NotFound(ApiResponse.Fail("Question not found"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            var fe = await _feRepo.GetByIdAsync(id);
            if (fe != null)
            {
                if (!IsAdminOwnedQuestion(fe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                if (string.Equals(fe.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                {
                    await _feRepo.DeleteAsync(id);
                    return Ok(ApiResponse.Ok(new
                    {
                        id,
                        action = "hard_delete",
                        deleted = true
                    }));
                }

                fe.Status = "Disabled";
                fe.DisabledReason = "Deleted by admin";
                fe.DisabledBy = AdminId;
                fe.DisabledAt = DateTime.UtcNow;
                fe.LastModifiedBy = AdminId;
                fe.LastModifiedAt = DateTime.UtcNow;
                await _feRepo.UpdateAsync(fe);
                return Ok(ApiResponse.Ok(new QuestionAdminMutationResultDto
                {
                    Action = "soft_delete",
                    Message = "Question was moved to Disabled status.",
                    Question = MapQuestion(fe)
                }));
            }

            var pe = await _peRepo.GetByIdAsync(id);
            if (pe != null)
            {
                if (!IsAdminOwnedQuestion(pe))
                {
                    return NotFound(ApiResponse.Fail("Question not found"));
                }

                if (string.Equals(pe.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                {
                    await _peRepo.DeleteAsync(id);
                    return Ok(ApiResponse.Ok(new
                    {
                        id,
                        action = "hard_delete",
                        deleted = true
                    }));
                }

                pe.Status = "Disabled";
                pe.DisabledReason = "Deleted by admin";
                pe.DisabledBy = AdminId;
                pe.DisabledAt = DateTime.UtcNow;
                pe.LastModifiedBy = AdminId;
                pe.LastModifiedAt = DateTime.UtcNow;
                await _peRepo.UpdateAsync(pe);
                return Ok(ApiResponse.Ok(new QuestionAdminMutationResultDto
                {
                    Action = "soft_delete",
                    Message = "Question was moved to Disabled status.",
                    Question = MapQuestion(pe)
                }));
            }

            return NotFound(ApiResponse.Fail("Question not found"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    private static QuestionAdminDetailDto MapQuestion(FEQuestion question)
    {
        return new QuestionAdminDetailDto
        {
            Id = question.Id,
            Type = "FE",
            CourseId = question.CourseId,
            TopicTags = question.TopicTags ?? new List<string>(),
            Difficulty = question.Difficulty,
            Title = question.Title,
            Description = question.Description,
            Status = question.Status,
            IsPublic = question.IsPublic,
            CreatedBy = question.CreatedBy,
            LastModifiedBy = question.LastModifiedBy,
            LastModifiedAt = question.LastModifiedAt,
            DisabledReason = question.DisabledReason,
            DisabledBy = question.DisabledBy,
            DisabledAt = question.DisabledAt,
            Source = question.Source,
            SourceScope = question.SourceScope,
            OwnerUserId = question.OwnerUserId,
            QuestionFingerprint = question.QuestionFingerprint,
            SourceDocumentIds = question.SourceDocumentIds ?? new List<string>(),
            SourceDocumentCount = question.SourceDocumentIds?.Count ?? 0,
            Options = question.Options ?? new List<string>(),
            CorrectAnswer = question.CorrectAnswer ?? new List<string>(),
            Explanation = question.Explanation,
            IsDisabled = string.Equals(question.Status, "Disabled", StringComparison.OrdinalIgnoreCase),
            IsDraft = string.Equals(question.Status, "Draft", StringComparison.OrdinalIgnoreCase),
            CanPublish = !string.Equals(question.Status, "Active", StringComparison.OrdinalIgnoreCase),
            CanReject = !string.Equals(question.Status, "Disabled", StringComparison.OrdinalIgnoreCase),
            CanDisable = true,
            CanEdit = true,
            StatusLabel = BuildStatusLabel(question.Status)
        };
    }

    private static QuestionAdminDetailDto MapQuestion(PEQuestion question)
    {
        return new QuestionAdminDetailDto
        {
            Id = question.Id,
            Type = "PE",
            CourseId = question.CourseId,
            TopicTags = question.TopicTags ?? new List<string>(),
            Difficulty = question.Difficulty,
            Title = question.Title,
            Description = question.Description,
            Status = question.Status,
            IsPublic = question.IsPublic,
            CreatedBy = question.CreatedBy,
            LastModifiedBy = question.LastModifiedBy,
            LastModifiedAt = question.LastModifiedAt,
            DisabledReason = question.DisabledReason,
            DisabledBy = question.DisabledBy,
            DisabledAt = question.DisabledAt,
            Source = question.Source,
            SourceScope = question.SourceScope,
            OwnerUserId = question.OwnerUserId,
            QuestionFingerprint = question.QuestionFingerprint,
            SourceDocumentIds = question.SourceDocumentIds ?? new List<string>(),
            SourceDocumentCount = question.SourceDocumentIds?.Count ?? 0,
            SkeletonCode = question.SkeletonCode,
            SolutionCode = question.SolutionCode,
            TestCases = question.TestCases,
            IsDisabled = string.Equals(question.Status, "Disabled", StringComparison.OrdinalIgnoreCase),
            IsDraft = string.Equals(question.Status, "Draft", StringComparison.OrdinalIgnoreCase),
            CanPublish = !string.Equals(question.Status, "Active", StringComparison.OrdinalIgnoreCase),
            CanReject = !string.Equals(question.Status, "Disabled", StringComparison.OrdinalIgnoreCase),
            CanDisable = true,
            CanEdit = true,
            StatusLabel = BuildStatusLabel(question.Status)
        };
    }

    private static string BuildStatusLabel(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Unknown";
        }

        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return "Published";
        }

        if (string.Equals(status, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return "Rejected or Disabled";
        }

        return status;
    }
}
