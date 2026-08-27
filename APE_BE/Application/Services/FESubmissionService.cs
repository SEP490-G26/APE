using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public class FESubmissionService
{
    private readonly IFESubmissionRepository _feRepo;
    private readonly IFEQuestionRepository _feQuestionRepo;
    private readonly IPracticeSessionRepository _sessionRepo;
    private readonly IExamRepository _examRepo;
    private readonly PracticeService _practiceService;

    public FESubmissionService(
        IFESubmissionRepository feRepo,
        IFEQuestionRepository feQuestionRepo,
        IPracticeSessionRepository sessionRepo,
        IExamRepository examRepo,
        PracticeService practiceService)
    {
        _feRepo = feRepo;
        _feQuestionRepo = feQuestionRepo;
        _sessionRepo = sessionRepo;
        _examRepo = examRepo;
        _practiceService = practiceService;
    }

    private static string? NormalizeSingleAnswer(FEQuestion question, string? answer)
    {
        var trimmed = answer?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var options = question.Options ?? new List<string>();

        if (trimmed.Length == 1 && char.IsLetter(trimmed[0]))
        {
            var optionIndex = char.ToUpperInvariant(trimmed[0]) - 'A';
            if (optionIndex >= 0 && optionIndex < options.Count)
            {
                return options[optionIndex].Trim().ToLowerInvariant();
            }
        }

        var matchedIndex = options.FindIndex(option => string.Equals(option?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        if (matchedIndex >= 0)
        {
            return options[matchedIndex].Trim().ToLowerInvariant();
        }

        return trimmed.ToLowerInvariant();
    }

    private static HashSet<string> NormalizeAnswers(FEQuestion question, IEnumerable<string?> answers)
    {
        return answers
            .Select(answer => NormalizeSingleAnswer(question, answer))
            .Where(answer => !string.IsNullOrWhiteSpace(answer))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool EvaluateCorrectness(FEQuestion question, IEnumerable<string?> userAnswers)
    {
        var normalizedUser = NormalizeAnswers(question, userAnswers);
        var normalizedCorrect = NormalizeAnswers(question, question.CorrectAnswer ?? new List<string>());
        return normalizedUser.SetEquals(normalizedCorrect);
    }

    public async Task<ApiResponse<FESubmissionResultDto>> CreateAndEvaluateAsync(string userId, FESubmissionInputDto dto)
    {
        // Validate session exists and is in progress
        var session = await _sessionRepo.GetByIdAsync(dto.SessionId);
        if (session == null) return new ApiResponse<FESubmissionResultDto> { Success = false, Error = "Session not found" };
        if (!string.Equals(session.StudentId, userId, StringComparison.Ordinal))
            return new ApiResponse<FESubmissionResultDto> { Success = false, Error = "You do not have permission to submit to this session" };
        if (session.Status != Domain.Enums.SessionStatus.InProgress)
            return new ApiResponse<FESubmissionResultDto> { Success = false, Error = "Session is not in progress" };

        var exam = await _examRepo.GetByIdAsync(session.ExamId);
        if (exam == null) return new ApiResponse<FESubmissionResultDto> { Success = false, Error = "Exam not found" };
        if (exam.FeExamQuestions.All(id => !string.Equals(id, dto.QuestionId, StringComparison.Ordinal)))
            return new ApiResponse<FESubmissionResultDto> { Success = false, Error = "Question does not belong to this exam" };

        // Validate question
        var question = await _feQuestionRepo.GetByIdAsync(dto.QuestionId);
        if (question == null) return new ApiResponse<FESubmissionResultDto> { Success = false, Error = "Question not found" };

        // Normalize and compare answers (treat as set equality)
        bool isCorrect = EvaluateCorrectness(question, dto.UserAnswer);

        var sub = new FE_Submission
        {
            FeQuestionId = dto.QuestionId,
            SessionId = dto.SessionId,
            CourseId = question.CourseId,
            UserAnswer = dto.UserAnswer ?? [],
            IsCorrect = isCorrect
        };

        await _feRepo.CreateAsync(sub);

        // Link submission to session via PracticeService
        await _practiceService.AddSubmissionAsync(dto.SessionId, sub.Id, Application.Enums.SubmissionType.FE);

        var result = new FESubmissionResultDto
        {
            Id = sub.Id,
            QuestionId = sub.FeQuestionId,
            SessionId = sub.SessionId,
            CourseId = sub.CourseId,
            UserAnswer = sub.UserAnswer ?? new List<string>(),
            CorrectAnswer = question.CorrectAnswer ?? new List<string>(),
            IsCorrect = sub.IsCorrect,
            EarnedPoints = sub.IsCorrect ? 1 : 0
        };

        return ApiResponse.Ok(result);
    }

    public async Task<ApiResponse<FESubmissionResultDto>> GetByIdAsync(string id)
    {
        var s = await _feRepo.GetByIdAsync(id);
        if (s == null) return new ApiResponse<FESubmissionResultDto> { Success = false, Error = "FE Submission not found" };
        var question = await _feQuestionRepo.GetByIdAsync(s.FeQuestionId);
        var isCorrect = question is not null ? EvaluateCorrectness(question, s.UserAnswer) : s.IsCorrect;
        var dto = new FESubmissionResultDto
        {
            Id = s.Id,
            QuestionId = s.FeQuestionId,
            SessionId = s.SessionId,
            CourseId = s.CourseId,
            UserAnswer = s.UserAnswer ?? new List<string>(),
            CorrectAnswer = question?.CorrectAnswer ?? new List<string>(),
            IsCorrect = isCorrect,
            EarnedPoints = isCorrect ? 1 : 0
        };
        return ApiResponse.Ok(dto);
    }

    public async Task<ApiResponse<List<FESubmissionResultDto>>> GetBySessionAsync(string sessionId)
    {
        var subs = await _feRepo.GetBySessionIdAsync(sessionId);
        var questionIds = subs.Select(s => s.FeQuestionId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var questions = questionIds.Count > 0
            ? await _feQuestionRepo.GetByIdsAsync(questionIds)
            : new List<FEQuestion>();
        var questionMap = questions.ToDictionary(
            question => question.Id,
            question => question,
            StringComparer.OrdinalIgnoreCase);

        var list = subs.Select(s =>
        {
            var question = questionMap.TryGetValue(s.FeQuestionId, out var matchedQuestion)
                ? matchedQuestion
                : null;
            var isCorrect = question is not null ? EvaluateCorrectness(question, s.UserAnswer) : s.IsCorrect;

            return new FESubmissionResultDto
            {
                Id = s.Id,
                QuestionId = s.FeQuestionId,
                UserAnswer = s.UserAnswer ?? new List<string>(),
                CorrectAnswer = question is not null
                    ? question.CorrectAnswer ?? new List<string>()
                    : new List<string>(),
                IsCorrect = isCorrect,
                EarnedPoints = isCorrect ? 1 : 0
            };
        }).ToList();
        return ApiResponse.Ok(list);
    }
}
