using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class QuestionImportService
{
    private readonly IFEQuestionRepository _feRepo;
    private readonly ICourseRepository _courseRepo;
    private readonly ILogger<QuestionImportService> _logger;

    // Static JsonSerializerOptions để tái sử dụng
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public QuestionImportService(
        IFEQuestionRepository feRepo,
        ICourseRepository courseRepo,
        ILogger<QuestionImportService> logger)
    {
        _feRepo = feRepo;
        _courseRepo = courseRepo;
        _logger = logger;
    }

    public async Task<ImportResultDto> ImportFromExcelAsync(Stream fileStream, string adminId)
    {
        var result = new ImportResultDto();
        var validQuestions = new List<FEQuestion>();
        var rowErrors = new List<RowError>();

        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheet(1);

        // Kiểm tra sheet có dữ liệu không
        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
        {
            result.RowErrors.Add(new RowError { Row = 0, Error = "The Excel file is empty or contains no data." });
            return result;
        }

        var rows = usedRange.RowsUsed().Skip(1).ToList();
        if (rows.Count == 0)
        {
            result.RowErrors.Add(new RowError { Row = 0, Error = "The Excel file is empty or contains no data." });
            return result;
        }

        // Giới hạn số dòng
        const int MAX_ROWS = 1000;
        if (rows.Count > MAX_ROWS)
        {
            result.RowErrors.Add(new RowError { Row = 0, Error = $"The file has too many rows. The maximum is {MAX_ROWS} rows." });
            return result;
        }

        // ──────── PRELOAD DICTIONARIES ────────
        var allCourses = await GetAllCoursesDictionaryAsync();
        var existingTitles = await GetAllFETitlesHashSetAsync();
        var titlesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Kiểm tra trùng trong file

        // ──────── VALIDATE TỪNG DÒNG ────────
        int rowIndex = 1;
        foreach (var row in rows)
        {
            rowIndex++;
            result.TotalRows++;

            try
            {
                var title = row.Cell(1).GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    rowErrors.Add(new RowError { Row = rowIndex, Error = "Title is required." });
                    continue;
                }

                // Kiểm tra trùng tiêu đề (DB + file)
                if (existingTitles.Contains(title) || !titlesInFile.Add(title))
                {
                    rowErrors.Add(new RowError { Row = rowIndex, Error = $"A question with the title '{title}' already exists." });
                    continue;
                }

                var courseCode = row.Cell(8).GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(courseCode))
                {
                    rowErrors.Add(new RowError { Row = rowIndex, Error = "Course code is required." });
                    continue;
                }

                if (!allCourses.TryGetValue(courseCode, out var course) || course.Status != "Active")
                {
                    rowErrors.Add(new RowError { Row = rowIndex, Error = $"Course code '{courseCode}' does not exist or is not active." });
                    continue;
                }

                // Validate Difficulty
                var difficulty = row.Cell(6).GetValue<string>()?.Trim();
                var validDifficulties = new[] { "Easy", "Medium", "Hard" };
                if (!validDifficulties.Contains(difficulty, StringComparer.OrdinalIgnoreCase))
                {
                    rowErrors.Add(new RowError { Row = rowIndex, Error = $"Difficulty '{difficulty}' is invalid." });
                    continue;
                }
                difficulty = difficulty!.Substring(0, 1).ToUpper() + difficulty.Substring(1).ToLower();

                // Parse Options
                var optionsJson = row.Cell(3).GetValue<string>()?.Trim();
                if (!TryParseOptions(optionsJson, out var options, out var optionsError))
                {
                    rowErrors.Add(new RowError { Row = rowIndex, Error = optionsError });
                    continue;
                }

                // Parse CorrectAnswer
                var correctAnswerJson = row.Cell(4).GetValue<string>()?.Trim();
                if (!TryParseCorrectAnswer(correctAnswerJson, options!, out var correctAnswers, out var answerError))
                {
                    rowErrors.Add(new RowError { Row = rowIndex, Error = answerError });
                    continue;
                }

                // Parse TopicTags
                var topicTagsStr = row.Cell(7).GetValue<string>()?.Trim();
                var topicTags = ParseTopicTags(topicTagsStr);

                var question = new FEQuestion
                {
                    Title = title,
                    Description = row.Cell(2).GetValue<string>()?.Trim() ?? string.Empty,
                    Options = options!,
                    CorrectAnswer = correctAnswers!,
                    Explanation = row.Cell(5).GetValue<string>()?.Trim(),
                    Difficulty = difficulty,
                    TopicTags = topicTags,
                    CourseId = course.Id,
                    Status = "Active",
                    Source = "Admin",
                    IsPublic = true,
                    CreatedBy = adminId,
                    CreatedAt = DateTime.UtcNow,
                    SourceDocumentIds = new List<string>()
                };

                validQuestions.Add(question);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                rowErrors.Add(new RowError { Row = rowIndex, Error = $"Unknown error: {ex.Message}" });
                _logger.LogError(ex, "Error importing row {Row}", rowIndex);
            }
        }

        result.RowErrors = rowErrors;
        result.FailCount = rowErrors.Count;

        // ──────── INSERT ALL VALID QUESTIONS ────────
        if (validQuestions.Any())
        {
            await _feRepo.InsertManyAsync(validQuestions);
            _logger.LogInformation("Successfully imported {Count} FE questions by admin {AdminId}", validQuestions.Count, adminId);
        }

        return result;
    }

    // ==================== HELPER METHODS ====================

    private async Task<Dictionary<string, Course>> GetAllCoursesDictionaryAsync()
    {
        var courses = await _courseRepo.GetAllAsync();
        return courses.ToDictionary(c => c.Code, c => c);
    }

    private async Task<HashSet<string>> GetAllFETitlesHashSetAsync()
    {
        var titles = await _feRepo.GetAllTitlesAsync();
        return new HashSet<string>(titles, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryParseOptions(string? json, out List<string>? options, out string error)
    {
        options = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Options are required.";
            return false;
        }

        try
        {
            options = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions);
            if (options == null || options.Count < 2)
            {
                error = "Options must be a JSON array with at least 2 items.";
                return false;
            }
            return true;
        }
        catch
        {
            error = "Options must be valid JSON.";
            return false;
        }
    }

    private static bool TryParseCorrectAnswer(string? json, List<string> options, out List<string>? answers, out string error)
    {
        answers = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "CorrectAnswer is required.";
            return false;
        }

        try
        {
            answers = JsonSerializer.Deserialize<List<string>>(json);
            if (answers == null || answers.Count == 0)
            {
                error = "CorrectAnswer must be a non-empty JSON array.";
                return false;
            }
            foreach (var ans in answers)
            {
                if (!options.Contains(ans))
                {
                    error = $"Answer '{ans}' is not included in the Options list.";
                    return false;
                }
            }
            return true;
        }
        catch
        {
            error = "CorrectAnswer must be valid JSON.";
            return false;
        }
    }

    private static List<string> ParseTopicTags(string? topicTagsStr)
    {
        if (string.IsNullOrWhiteSpace(topicTagsStr)) return new List<string>();
        return topicTagsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(t => t.Trim())
                           .Where(t => !string.IsNullOrEmpty(t))
                           .ToList();
    }
}
