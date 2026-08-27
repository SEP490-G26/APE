using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public class ExamService
{
    private readonly IExamRepository _examRepo;
    private readonly IFEQuestionRepository? _feQuestionRepo;
    private readonly IPEQuestionRepository _peQuestionRepo;

    public ExamService(IExamRepository examRepo, IPEQuestionRepository peQuestionRepo)
    {
        _examRepo = examRepo;
        _peQuestionRepo = peQuestionRepo;
    }

    public ExamService(IExamRepository examRepo, IFEQuestionRepository feQuestionRepo, IPEQuestionRepository peQuestionRepo)
    {
        _examRepo = examRepo;
        _feQuestionRepo = feQuestionRepo;
        _peQuestionRepo = peQuestionRepo;
    }

    public async Task<Exam> CreateExamAsync(CreateExamDto dto)
    {
        await ValidateQuestionsAsync(dto);
        var examType = NormalizeExamType(dto.ExamType, dto.FeExamQuestions, dto.PeExamQuestions);

        var exam = new Exam
        {
            Title = dto.Title,
            CourseId = dto.CourseId,
            ExamType = examType,
            PresetScope = NormalizePresetScope(dto.PresetScope),
            Mode = Enum.Parse<ExamMode>(dto.Mode),
            TimeLimit = dto.TimeLimit,
            CreatedBy = dto.CreatedBy,
            Visibility = Enum.Parse<ExamVisibility>(dto.Visibility),
            FeExamQuestions = dto.FeExamQuestions,
            PeExamQuestions = dto.PeExamQuestions.Select(x => new PeExamQuestion
            {
                PeQuestionId = x.PeQuestionId,
                AssignedPoints = x.AssignedPoints
            }).ToList()
        };

        await _examRepo.InsertAsync(exam);
        return exam;
    }

    public async Task<Exam> UpdateExamAsync(string examId, CreateExamDto dto)
    {
        var existing = await _examRepo.GetByIdAsync(examId)
            ?? throw new InvalidOperationException("Exam not found.");

        await ValidateQuestionsAsync(dto);
        var examType = NormalizeExamType(dto.ExamType, dto.FeExamQuestions, dto.PeExamQuestions);

        existing.Title = dto.Title;
        existing.CourseId = dto.CourseId;
        existing.ExamType = examType;
        existing.PresetScope = NormalizePresetScope(dto.PresetScope);
        existing.Mode = Enum.Parse<ExamMode>(dto.Mode);
        existing.TimeLimit = dto.TimeLimit;
        existing.Visibility = Enum.Parse<ExamVisibility>(dto.Visibility);
        existing.FeExamQuestions = dto.FeExamQuestions;
        existing.PeExamQuestions = dto.PeExamQuestions.Select(x => new PeExamQuestion
        {
            PeQuestionId = x.PeQuestionId,
            AssignedPoints = x.AssignedPoints
        }).ToList();
        existing.UpdatedBy = dto.CreatedBy;
        existing.UpdatedAt = DateTime.UtcNow;

        await _examRepo.ReplaceAsync(existing);
        return existing;
    }

    public Task<Exam?> GetExamByIdAsync(string examId) =>
        _examRepo.GetByIdAsync(examId);

    public async Task<bool> SoftDeleteExamAsync(string examId, string actorUserId, bool requireOwner)
    {
        if (string.IsNullOrWhiteSpace(examId))
        {
            throw new InvalidOperationException("Exam id is required.");
        }

        var exam = await _examRepo.GetByIdAsync(examId)
            ?? throw new InvalidOperationException("Exam not found.");

        if (requireOwner && !string.Equals(exam.CreatedBy, actorUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("You do not have permission to delete this exam.");
        }

        if (exam.IsDeleted)
        {
            return true;
        }

        exam.IsDeleted = true;
        exam.DeletedBy = actorUserId;
        exam.DeletedAt = DateTime.UtcNow;
        exam.UpdatedBy = actorUserId;
        exam.UpdatedAt = DateTime.UtcNow;

        await _examRepo.ReplaceAsync(exam);
        return true;
    }

    public async Task<ExamDetailDto?> GetExamDetailByIdAsync(string examId)
    {
        var exam = await _examRepo.GetByIdAsync(examId);

        if (exam is null)
        {
            return null;
        }

        var feQuestions =
            exam.FeExamQuestions.Any() &&
            _feQuestionRepo is not null
                ? await _feQuestionRepo.GetByIdsAsync(exam.FeExamQuestions)
                : new List<FEQuestion>();

        var peQuestionIds = exam.PeExamQuestions
            .Select(item => item.PeQuestionId)
            .ToList();

        var peQuestions = peQuestionIds.Any()
            ? await _peQuestionRepo.GetByIdsAsync(peQuestionIds)
            : new List<PEQuestion>();

        return new ExamDetailDto
        {
            Id = exam.Id,
            Title = exam.Title,
            CourseId = exam.CourseId,
            ExamType = NormalizeExamType(exam.ExamType, exam.FeExamQuestions, exam.PeExamQuestions),
            PresetScope = NormalizePresetScope(exam.PresetScope),
            Mode = exam.Mode.ToString(),
            Visibility = exam.Visibility.ToString(),
            TimeLimit = exam.TimeLimit,
            CreatedAt = exam.CreatedAt,
            UpdatedAt = exam.UpdatedAt,
            IsDeleted = exam.IsDeleted,
            DeletedAt = exam.DeletedAt,
            FEQuestionCount = feQuestions.Count,
            PEQuestionCount = peQuestions.Count,
            TotalQuestionCount = feQuestions.Count + peQuestions.Count,
            FEQuestions = feQuestions
                .Select(question => new FEQuestionDto
                {
                    Id = question.Id,
                    Title = question.Title ?? string.Empty,
                    Description = question.Description ?? string.Empty,
                    Options = question.Options ?? new List<string>(),
                    CorrectAnswer = question.CorrectAnswer ?? new List<string>(),
                    Explanation = question.Explanation,
                    Difficulty = question.Difficulty,
                    TopicTags = question.TopicTags ?? new List<string>()
                })
                .ToList(),
            PEQuestions = peQuestions
                .Select(question => new PEQuestionDto
                {
                    Id = question.Id,
                    Title = question.Title ?? string.Empty,
                    Description = question.Description ?? string.Empty,
                    SkeletonCode = (question.SkeletonCode ?? new List<CodeFile>())
                        .Select(file => new SubmittedCodeFileDto
                        {
                            Filename = file.Filename,
                            Content = file.Content
                        })
                        .ToList(),
                    TestCases = (question.TestCases ?? new List<TestCase>())
                        .Where(testCase => !testCase.IsHidden)
                        .Select(testCase => new TestCaseDto
                        {
                            Input = testCase.Input,
                            ExpectedOutput = testCase.ExpectedOutput,
                            IsHidden = testCase.IsHidden
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    public async Task<(List<ExamDto> Items, long Total)> ListAsync(string courseId, int page = 1, int limit = 10)
    {
        var (exams, total) = await _examRepo.ListByCourseAsync(courseId, page, limit);
        return (MapToDtos(exams), total);
    }

    public async Task<(List<ExamDto> Items, long Total)> ListAllAsync(int page = 1, int limit = 10)
    {
        var (exams, total) = await _examRepo.ListAsync(page, limit);
        return (MapToDtos(exams), total);
    }

    public async Task<(List<ExamDto> Items, long Total)> ListPublicAsync(
        string? courseId,
        string? examType,
        string? mode,
        int page = 1,
        int limit = 10)
    {
        var (exams, total) = await _examRepo.ListPublicAsync(courseId, examType, mode, page, limit);
        return (MapToDtos(exams), total);
    }

    public async Task<(List<ExamDto> Items, long Total)> ListOwnedAsync(
        string creatorUserId,
        string? courseId,
        string? examType,
        string? mode,
        int page = 1,
        int limit = 10)
    {
        var (exams, total) = await _examRepo.ListByCreatorAsync(creatorUserId, courseId, examType, mode, page, limit);
        return (MapToDtos(exams), total);
    }

    private List<ExamDto> MapToDtos(List<Exam> exams)
    {
        return exams.Select(e => new ExamDto
        {
            Id = e.Id,
            Title = e.Title,
            CourseId = e.CourseId,
            ExamType = NormalizeExamType(e.ExamType, e.FeExamQuestions, e.PeExamQuestions),
            PresetScope = NormalizePresetScope(e.PresetScope),
            Mode = e.Mode.ToString(),
            TimeLimit = e.TimeLimit,
            CreatedBy = e.CreatedBy,
            Visibility = e.Visibility.ToString(),
            FEQuestionCount = e.FeExamQuestions.Count,
            PEQuestionCount = e.PeExamQuestions.Count,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            IsDeleted = e.IsDeleted,
            DeletedAt = e.DeletedAt
        }).ToList();
    }

    private async Task ValidateQuestionsAsync(CreateExamDto dto)
    {
        if (dto.FeExamQuestions.Any())
        {
            if (_feQuestionRepo == null) throw new InvalidOperationException("FE question repository is not configured.");
            var feQuestions = await _feQuestionRepo.GetByIdsAsync(dto.FeExamQuestions);
            var feById = feQuestions.ToDictionary(q => q.Id, q => q, StringComparer.OrdinalIgnoreCase);

            foreach (var feId in dto.FeExamQuestions)
            {
                if (!feById.ContainsKey(feId))
                    throw new InvalidOperationException($"Invalid question ID: {feId}");
            }

            var invalidFe = feQuestions.FirstOrDefault(q => !string.Equals(q.Status, "Active", StringComparison.OrdinalIgnoreCase));
            if (invalidFe != null)
                throw new InvalidOperationException($"Question {invalidFe.Id} is {invalidFe.Status}. Only Active questions allowed.");
        }

        if (dto.PeExamQuestions.Any())
        {
            var ids = dto.PeExamQuestions.Select(x => x.PeQuestionId).ToList();
            var peQuestions = await _peQuestionRepo.GetByIdsAsync(ids);
            var peById = peQuestions.ToDictionary(q => q.Id, q => q, StringComparer.OrdinalIgnoreCase);

            foreach (var peId in ids)
            {
                if (!peById.ContainsKey(peId))
                    throw new InvalidOperationException($"Invalid question ID: {peId}");
            }

            var invalidPe = peQuestions.FirstOrDefault(q => !string.Equals(q.Status, "Active", StringComparison.OrdinalIgnoreCase));
            if (invalidPe != null)
                throw new InvalidOperationException($"Question {invalidPe.Id} is {invalidPe.Status}. Only Active questions allowed.");
        }
    }

    private static string NormalizeExamType(string? examType, List<string>? feQuestionIds, List<PeExamQuestionDto>? peQuestions)
    {
        var hasFe = feQuestionIds?.Any() == true;
        var hasPe = peQuestions?.Any() == true;

        if (hasFe && !hasPe) return "FE";
        if (!hasFe && hasPe) return "PE";
        if (hasFe && hasPe) return "Mixed";

        var normalized = (examType ?? string.Empty).Trim();
        if (string.Equals(normalized, "FE", StringComparison.OrdinalIgnoreCase)) return "FE";
        if (string.Equals(normalized, "PE", StringComparison.OrdinalIgnoreCase)) return "PE";
        if (string.Equals(normalized, "Mixed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "FE/PE", StringComparison.OrdinalIgnoreCase)) return "Mixed";

        return "Mixed";
    }

    private static string NormalizeExamType(string? examType, List<string>? feQuestionIds, List<PeExamQuestion>? peQuestions)
    {
        var hasFe = feQuestionIds?.Any() == true;
        var hasPe = peQuestions?.Any() == true;

        if (hasFe && !hasPe) return "FE";
        if (!hasFe && hasPe) return "PE";
        if (hasFe && hasPe) return "Mixed";

        var normalized = (examType ?? string.Empty).Trim();
        if (string.Equals(normalized, "FE", StringComparison.OrdinalIgnoreCase)) return "FE";
        if (string.Equals(normalized, "PE", StringComparison.OrdinalIgnoreCase)) return "PE";
        if (string.Equals(normalized, "Mixed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "FE/PE", StringComparison.OrdinalIgnoreCase)) return "Mixed";

        return "Mixed";
    }

    private static string NormalizePresetScope(string? presetScope)
    {
        return "Course";
    }
}
