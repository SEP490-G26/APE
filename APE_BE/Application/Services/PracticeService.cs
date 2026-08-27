using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public class PracticeService
{
    private readonly IPracticeSessionRepository _sessionRepo;
    private readonly IExamRepository _examRepo;
    private readonly ICourseRepository _courseRepo;
    private readonly IUserRepository _userRepo;
    private readonly IFEQuestionRepository _feQuestionRepo;
    private readonly IPEQuestionRepository _peQuestionRepo;
    private readonly IFESubmissionRepository _feRepo;
    private readonly IPESubmissionRepository _peRepo;

    public PracticeService(
        IPracticeSessionRepository sessionRepo,
        IExamRepository examRepo,
        ICourseRepository courseRepo,
        IUserRepository userRepo,
        IFEQuestionRepository feQuestionRepo,
        IPEQuestionRepository peQuestionRepo,
        IFESubmissionRepository feRepo,
        IPESubmissionRepository peRepo)
    {
        _sessionRepo = sessionRepo;
        _examRepo = examRepo;
        _courseRepo = courseRepo;
        _userRepo = userRepo;
        _feQuestionRepo = feQuestionRepo;
        _peQuestionRepo = peQuestionRepo;
        _feRepo = feRepo;
        _peRepo = peRepo;
    }

    public async Task<ApiResponse<StartSessionResultDto>> StartSessionAsync(string userId, string examId, bool forceNew = false)
    {
        // Validate user and exam
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return new ApiResponse<StartSessionResultDto> { Success = false, Error = "User not found" };
        var exam = await _examRepo.GetByIdAsync(examId);
        if (exam == null) return new ApiResponse<StartSessionResultDto> { Success = false, Error = "Exam not found" };
        var examAvailabilityError = await ValidateExamAvailabilityAsync(exam);
        if (!string.IsNullOrWhiteSpace(examAvailabilityError))
        {
            return new ApiResponse<StartSessionResultDto> { Success = false, Error = examAvailabilityError };
        }

        var existing = await _sessionRepo.GetActiveSessionAsync(userId, examId);

        // Reuse active session only when the caller explicitly allows it.
        if (!forceNew)
        {
            if (existing != null)
            {
                return ApiResponse.Ok(new StartSessionResultDto { SessionId = existing.Id, StartTime = existing.StartTime });
            }
        }
        else if (existing != null)
        {
            var archivedAt = DateTime.UtcNow;
            if (!existing.IsPaused && existing.LastResumedAt.HasValue)
            {
                existing.ActiveDurationSeconds += CalculateElapsedDeltaSeconds(existing.LastResumedAt.Value, archivedAt);
            }

            existing.EndTime = archivedAt;
            existing.LastPausedAt = archivedAt;
            existing.LastResumedAt = null;
            existing.IsPaused = true;
            existing.Status = Domain.Enums.SessionStatus.Timeout;
            await _sessionRepo.UpdateAsync(existing);
        }

        // Create new session
        var now = DateTime.UtcNow;
        var session = new PracticeSession
        {
            StudentId = userId,
            ExamId = examId,
            StartTime = now,
            LastResumedAt = now,
            IsPaused = false,
            Status = Domain.Enums.SessionStatus.InProgress
        };

        await _sessionRepo.CreateAsync(session);
        return ApiResponse.Ok(new StartSessionResultDto { SessionId = session.Id, StartTime = session.StartTime });
    }

    public async Task<ApiResponse<PracticeSessionDto>> GetSessionAsync(string userId, string sessionId)
    {
        var s = await _sessionRepo.GetByIdAsync(sessionId);
        if (s == null) return new ApiResponse<PracticeSessionDto> { Success = false, Error = "Session not found" };
        if (!string.Equals(s.StudentId, userId, StringComparison.Ordinal))
            return new ApiResponse<PracticeSessionDto> { Success = false, Error = "You do not have permission to view this session" };
        var exam = await _examRepo.GetByIdAsync(s.ExamId);
        var feQuestionCount = exam?.FeExamQuestions?.Count ?? 0;
        var peQuestionCount = exam?.PeExamQuestions?.Count ?? 0;
        var dto = new PracticeSessionDto
        {
            Id = s.Id,
            StudentId = s.StudentId,
            ExamId = s.ExamId,
            ExamTitle = exam?.Title ?? string.Empty,
            CourseId = exam?.CourseId ?? string.Empty,
            ExamMode = exam?.Mode.ToString() ?? string.Empty,
            FEQuestionCount = feQuestionCount,
            PEQuestionCount = peQuestionCount,
            TotalQuestionCount = feQuestionCount + peQuestionCount,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            ActiveDurationSeconds = s.ActiveDurationSeconds,
            LastResumedAt = s.LastResumedAt,
            LastPausedAt = s.LastPausedAt,
            IsPaused = s.IsPaused,
            Status = s.Status,
            TotalScore = s.TotalScore,
            SelectedQuestionId = s.SelectedQuestionId,
            DraftCodes = (s.DraftCodes ?? new List<DraftCodeItem>()).Select(d => new DraftCodeDto
            {
                QuestionId = d.QuestionId,
                Files = d.Files.Select(f => new SubmittedCodeFileDto
                {
                    Filename = f.Filename,
                    Content = f.Content
                }).ToList()
            }).ToList()
        };
        return ApiResponse.Ok(dto);
    }

    public async Task<ApiResponse<bool>> EndSessionAsync(string userId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new ApiResponse<bool> { Success = false, Error = "Authenticated user is required." };
        }

        var s = await _sessionRepo.GetByIdAsync(sessionId);
        if (s == null) return new ApiResponse<bool> { Success = false, Error = "Session not found" };
        if (!string.Equals(s.StudentId, userId, StringComparison.Ordinal))
            return new ApiResponse<bool> { Success = false, Error = "You do not have permission to access this session." };
        if (s.Status != Domain.Enums.SessionStatus.InProgress) return new ApiResponse<bool> { Success = false, Error = "Session is not in progress" };

        // Aggregate FE submissions: latest attempt per question only
        var feSubs = await _feRepo.GetBySessionIdAsync(sessionId);
        var latestFeByQuestion = feSubs
            .GroupBy(sub => sub.FeQuestionId)
            .Select(g => g.OrderByDescending(x => x.Id).First());
        double feScore = latestFeByQuestion.Count(sub => sub.IsCorrect);

        // Aggregate PE submissions: latest attempt per question only
        var peSubs = await _peRepo.GetBySessionIdAsync(sessionId);
        var latestPeByQuestion = peSubs
     .GroupBy(sub => sub.QuestionId)
     .Select(group =>
         group
             .OrderByDescending(item => item.SubmittedAt)
             .First());
        double peScore = latestPeByQuestion
    .Where(sub =>
        sub.Status ==
        SubmissionProcessingStatus.Completed)
    .Sum(ResolvePeQuestionScoreForHistory);

        var total = feScore + peScore;
        var endTime = DateTime.UtcNow;
        if (!s.IsPaused && s.LastResumedAt.HasValue)
        {
            s.ActiveDurationSeconds += CalculateElapsedDeltaSeconds(s.LastResumedAt.Value, endTime);
        }

        s.EndTime = endTime;
        s.IsPaused = true;
        s.LastPausedAt = endTime;
        s.LastResumedAt = null;
        s.TotalScore = total;
        s.Status = Domain.Enums.SessionStatus.Submitted;
        await _sessionRepo.UpdateAsync(s);
        return ApiResponse.Ok(true);
    }

    public virtual async Task<ApiResponse<bool>> AddSubmissionAsync(string sessionId, string submissionId, Application.Enums.SubmissionType type)
    {
        var s = await _sessionRepo.GetByIdAsync(sessionId);
        if (s == null) return new ApiResponse<bool> { Success = false, Error = "Session not found" };
        if (s.Status != Domain.Enums.SessionStatus.InProgress) return new ApiResponse<bool> { Success = false, Error = "Session is not in progress" };

        // Session no longer stores FE/PE submission arrays in ERD.
        return ApiResponse.Ok(true);
    }

    public async Task<ApiResponse<List<PracticeHistoryItemDto>>> GetPracticeHistoryAsync(string userId, int page = 1, int limit = 20)
    {
        var sessions = await _sessionRepo.GetByUserIdAsync(userId, page, limit);
        var list = new List<PracticeHistoryItemDto>();
        foreach (var s in sessions)
        {
            var exam = await _examRepo.GetByIdAsync(s.ExamId);
            if (exam?.Mode == ExamMode.Flashcard)
            {
                continue;
            }

            list.Add(new PracticeHistoryItemDto
            {
                SessionId = s.Id,
                ExamTitle = exam?.Title ?? "Unknown",
                CourseName = "",
                Mode = s.Status.ToString(),
                TotalScore = s.TotalScore,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Status = s.Status.ToString()
            });
        }
        return ApiResponse.Ok(list);
    }

    public async Task<ApiResponse<PaginatedResult<PracticeHistoryItemDto>>> GetPracticeHistoryAsync(
        string userId,
        int page,
        int limit,
        string? courseId,
        string? type,
        DateTime? fromDate,
        DateTime? toDate)
    {
        page = page <= 0 ? 1 : page;
        limit = limit <= 0 ? 20 : limit;

        var totalSessions = await _sessionRepo.CountByUserIdAsync(userId);
        var allSessions = new List<PracticeSession>();
        const int batchSize = 100;
        var currentPage = 1;

        while (allSessions.Count < totalSessions)
        {
            var batch = await _sessionRepo.GetByUserIdAsync(userId, currentPage, batchSize);
            if (batch.Count == 0) break;
            allSessions.AddRange(batch);
            if (batch.Count < batchSize) break;
            currentPage++;
        }

        var examIds = allSessions.Select(s => s.ExamId).Distinct().ToList();
        var examMap = new Dictionary<string, Exam?>();
        foreach (var examId in examIds)
        {
            examMap[examId] = await _examRepo.GetByIdAsync(examId);
        }

        var courseIds = examMap.Values
            .Where(exam => exam is not null && !string.IsNullOrWhiteSpace(exam.CourseId))
            .Select(exam => exam!.CourseId)
            .Distinct()
            .ToList();

        var courseMap = new Dictionary<string, Course?>();
        foreach (var itemCourseId in courseIds)
        {
            courseMap[itemCourseId] = await _courseRepo.GetByIdAsync(itemCourseId);
        }

        var historyItems = new List<PracticeHistoryItemDto>();
        foreach (var s in allSessions)
        {
            var exam = examMap.GetValueOrDefault(s.ExamId);
            if (exam?.Mode == ExamMode.Flashcard)
            {
                continue;
            }

            var examType = ResolveExamType(exam);
            var duration = ResolveActiveDurationSeconds(s);

            var courseName = exam is not null && courseMap.TryGetValue(exam.CourseId, out var course)
                ? course?.Name ?? exam.CourseId
                : exam?.CourseId ?? string.Empty;

            var feSubmissions = await _feRepo.GetBySessionIdAsync(s.Id);
            var latestFeByQuestion = feSubmissions
                .GroupBy(item => item.FeQuestionId)
                .Select(group => group.OrderByDescending(item => item.SubmittedAt).First())
                .ToList();

            var peSubmissions = (await _peRepo.GetBySessionIdAsync(s.Id))
                .OrderByDescending(item => item.SubmittedAt)
                .ToList();

            var latestPeByQuestion = peSubmissions
                .GroupBy(item => item.QuestionId)
                .Select(group => group.First())
                .ToList();

            var feQuestionCount = exam?.FeExamQuestions?.Count ?? 0;
            var correctCount = latestFeByQuestion.Count(item => item.IsCorrect);
            var peMaxScore = exam?.PeExamQuestions?.Sum(item => item.AssignedPoints) ?? 0;
            var latestPeSubmission = peSubmissions.FirstOrDefault();
            var latestPeSubmissionId = latestPeSubmission?.Id;
            var latestPeVerdict = latestPeSubmission?.FinalVerdict?.ToString();
            var peEarnedScore = latestPeByQuestion.Sum(ResolvePeQuestionScoreForHistory);
            var score = examType switch
            {
                "PE" => peEarnedScore,
                "FE/PE" => correctCount + peEarnedScore,
                _ => s.TotalScore
            };
            var maxScore = examType switch
            {
                "FE" => feQuestionCount,
                "PE" => peMaxScore,
                _ => Math.Max(feQuestionCount + peMaxScore, 0)
            };

            historyItems.Add(new PracticeHistoryItemDto
            {
                SessionId = s.Id,
                ExamId = s.ExamId,
                Date = s.StartTime,
                CourseId = exam?.CourseId ?? string.Empty,
                ExamType = examType,
                Score = score,
                MaxScore = maxScore,
                CorrectCount = correctCount,
                TotalQuestions = feQuestionCount,
                DurationSeconds = duration,
                Status = ResolveHistoryStatus(s.Status),
                LatestPeSubmissionId = latestPeSubmissionId,
                LatestPeVerdict = latestPeVerdict,
                ExamDeleted = exam?.IsDeleted == true || exam is null,
                ExamTitle = exam?.Title ?? "Unknown",
                CourseName = courseName,
                Mode = examType,
                TotalScore = s.TotalScore,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            });
        }

        var filtered = historyItems
            .Where(i => string.IsNullOrWhiteSpace(courseId) || string.Equals(i.CourseId, courseId, StringComparison.OrdinalIgnoreCase))
            .Where(i => string.IsNullOrWhiteSpace(type) || string.Equals(i.ExamType, type, StringComparison.OrdinalIgnoreCase))
            .Where(i => !fromDate.HasValue || i.Date >= fromDate.Value)
            .Where(i => !toDate.HasValue || i.Date <= toDate.Value)
            .OrderByDescending(i => i.Date)
            .ToList();

        var total = filtered.Count;
        var items = filtered.Skip((page - 1) * limit).Take(limit).ToList();
        var result = new PaginatedResult<PracticeHistoryItemDto>
        {
            Items = items,
            Total = total,
            Page = page,
            Limit = limit,
            TotalPages = (total + limit - 1) / limit
        };

        return ApiResponse.Ok(result);
    }

    private static string ResolveExamType(Exam? exam)
    {
        if (exam == null) return "Unknown";
        if (!string.IsNullOrWhiteSpace(exam.ExamType) &&
            !string.Equals(exam.ExamType, "Mixed", StringComparison.OrdinalIgnoreCase))
        {
            return exam.ExamType.ToUpperInvariant();
        }

        var hasFe = exam.FeExamQuestions?.Any() == true;
        var hasPe = exam.PeExamQuestions?.Any() == true;
        if (hasFe && !hasPe) return "FE";
        if (!hasFe && hasPe) return "PE";
        if (hasFe && hasPe) return "FE/PE";
        return "Unknown";
    }

    private static string ResolveHistoryStatus(Domain.Enums.SessionStatus status)
    {
        return status switch
        {
            Domain.Enums.SessionStatus.Submitted => "Completed",
            Domain.Enums.SessionStatus.Timeout => "Abandoned",
            _ => status.ToString()
        };
    }

    public async Task<ApiResponse<bool>> SaveDraftCodeAsync(string userId, string sessionId, SaveDraftCodeDto request)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new ApiResponse<bool> { Success = false, Error = "Authenticated user is required." };
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return new ApiResponse<bool> { Success = false, Error = "SessionId is required." };
        }

        if (request is null || string.IsNullOrWhiteSpace(request.QuestionId))
        {
            return new ApiResponse<bool> { Success = false, Error = "QuestionId is required." };
        }

        var session = await _sessionRepo.GetByIdAsync(sessionId);
        if (session == null)
        {
            return new ApiResponse<bool> { Success = false, Error = "Session not found." };
        }

        if (!string.Equals(session.StudentId, userId, StringComparison.Ordinal))
        {
            return new ApiResponse<bool> { Success = false, Error = "You do not have permission to update this session." };
        }

        if (session.Status != Domain.Enums.SessionStatus.InProgress)
        {
            return new ApiResponse<bool> { Success = false, Error = "This session can no longer be edited." };
        }

        var nextFiles = (request.Files ?? new List<SubmittedCodeFileDto>())
            .Where(file => file != null && !string.IsNullOrWhiteSpace(file.Filename))
            .Select(file => new CodeFile
            {
                Filename = file.Filename.Trim(),
                Content = file.Content ?? string.Empty
            })
            .ToList();

        session.DraftCodes ??= new List<DraftCodeItem>();
        session.DraftCodes.RemoveAll(item => string.Equals(item.QuestionId, request.QuestionId, StringComparison.Ordinal));
        session.DraftCodes.Add(new DraftCodeItem
        {
            QuestionId = request.QuestionId.Trim(),
            Files = nextFiles
        });

        if (!string.IsNullOrWhiteSpace(request.SelectedQuestionId))
        {
            session.SelectedQuestionId = request.SelectedQuestionId.Trim();
        }

        await _sessionRepo.UpdateAsync(session);
        return ApiResponse.Ok(true);
    }

    public async Task<ApiResponse<bool>> PauseSessionAsync(string userId, string sessionId)
    {
        var validation = await GetEditableSessionAsync(userId, sessionId);
        if (!validation.Success || validation.Data == null)
        {
            return new ApiResponse<bool> { Success = false, Error = validation.Error };
        }

        var session = validation.Data;
        if (!session.IsPaused && session.LastResumedAt.HasValue)
        {
            var now = DateTime.UtcNow;
            session.ActiveDurationSeconds += CalculateElapsedDeltaSeconds(session.LastResumedAt.Value, now);
            session.LastPausedAt = now;
            session.LastResumedAt = null;
            session.IsPaused = true;
            await _sessionRepo.UpdateAsync(session);
        }

        return ApiResponse.Ok(true);
    }

    public async Task<ApiResponse<PracticeSessionDto>> ResumeSessionAsync(string userId, string sessionId)
    {
        var validation = await GetEditableSessionAsync(userId, sessionId);
        if (!validation.Success || validation.Data == null)
        {
            return new ApiResponse<PracticeSessionDto> { Success = false, Error = validation.Error };
        }

        var session = validation.Data;
        if (session.IsPaused)
        {
            session.IsPaused = false;
            session.LastPausedAt = null;
            session.LastResumedAt = DateTime.UtcNow;
            await _sessionRepo.UpdateAsync(session);
        }

        return await GetSessionAsync(userId, session.Id);
    }

    public async Task<ApiResponse<ActivePracticeSessionSummaryDto?>> GetActiveSessionByExamAsync(string userId, string examId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(examId))
        {
            return ApiResponse.Ok<ActivePracticeSessionSummaryDto?>(null);
        }

        var session = await _sessionRepo.GetActiveSessionAsync(userId.Trim(), examId.Trim());
        if (session == null)
        {
            return ApiResponse.Ok<ActivePracticeSessionSummaryDto?>(null);
        }

        return ApiResponse.Ok<ActivePracticeSessionSummaryDto?>(new ActivePracticeSessionSummaryDto
        {
            SessionId = session.Id,
            ExamId = session.ExamId,
            IsPaused = session.IsPaused,
            ActiveDurationSeconds = ResolveActiveDurationSeconds(session),
            StartTime = session.StartTime,
            LastPausedAt = session.LastPausedAt,
            SelectedQuestionId = session.SelectedQuestionId,
            DraftCodeCount = session.DraftCodes?.Count ?? 0
        });
    }

    private async Task<ApiResponse<PracticeSession?>> GetEditableSessionAsync(string userId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new ApiResponse<PracticeSession?> { Success = false, Error = "Authenticated user is required." };
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return new ApiResponse<PracticeSession?> { Success = false, Error = "SessionId is required." };
        }

        var session = await _sessionRepo.GetByIdAsync(sessionId);
        if (session == null)
        {
            return new ApiResponse<PracticeSession?> { Success = false, Error = "Session not found." };
        }

        if (!string.Equals(session.StudentId, userId, StringComparison.Ordinal))
        {
            return new ApiResponse<PracticeSession?> { Success = false, Error = "You do not have permission to access this session." };
        }

        if (session.Status != Domain.Enums.SessionStatus.InProgress)
        {
            return new ApiResponse<PracticeSession?> { Success = false, Error = "Practice session is no longer in progress." };
        }

        return ApiResponse.Ok<PracticeSession?>(session);
    }

    private static int ResolveActiveDurationSeconds(PracticeSession session)
    {
        if (session.EndTime.HasValue)
        {
            if (session.ActiveDurationSeconds > 0)
            {
                return Math.Max(0, session.ActiveDurationSeconds);
            }

            return CalculateElapsedDeltaSeconds(session.StartTime, session.EndTime.Value);
        }

        if (!session.IsPaused && session.LastResumedAt.HasValue)
        {
            return session.ActiveDurationSeconds + CalculateElapsedDeltaSeconds(session.LastResumedAt.Value, DateTime.UtcNow);
        }

        return Math.Max(0, session.ActiveDurationSeconds);
    }

    private static int CalculateElapsedDeltaSeconds(DateTime fromUtc, DateTime toUtc)
    {
        return (int)Math.Max(0, (toUtc - fromUtc).TotalSeconds);
    }

    private static double ResolvePeQuestionScoreForHistory(PE_Submission submission)
    {
        if (submission.Status != SubmissionProcessingStatus.Completed)
        {
            return 0;
        }

        return submission.FinalVerdict == SubmissionVerdict.Accepted
            ? submission.MaxScore
            : 0;
    }

    private async Task<string?> ValidateExamAvailabilityAsync(Exam exam)
    {
        if (exam.IsDeleted)
        {
            return "This exam was removed, so you cannot start a new practice session for it.";
        }

        if (exam.FeExamQuestions?.Any() == true)
        {
            var feQuestions = await _feQuestionRepo.GetByIdsAsync(exam.FeExamQuestions);
            var deletedFeQuestion = feQuestions.FirstOrDefault(question =>
                !string.Equals(question.Status, "Active", StringComparison.OrdinalIgnoreCase));

            if (deletedFeQuestion != null)
            {
                return "This exam contains questions that were deleted or are no longer active, so you cannot start a new practice session for it.";
            }
        }

        if (exam.PeExamQuestions?.Any() == true)
        {
            var peQuestionIds = exam.PeExamQuestions
                .Where(item => !string.IsNullOrWhiteSpace(item.PeQuestionId))
                .Select(item => item.PeQuestionId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var peQuestions = await _peQuestionRepo.GetByIdsAsync(peQuestionIds);
            var deletedPeQuestion = peQuestions.FirstOrDefault(question =>
                !string.Equals(question.Status, "Active", StringComparison.OrdinalIgnoreCase));

            if (deletedPeQuestion != null)
            {
                return "This exam contains questions that were deleted or are no longer active, so you cannot start a new practice session for it.";
            }
        }

        return null;
    }
}
