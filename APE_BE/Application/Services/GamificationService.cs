using Application.DTOs;
using Application.Interfaces;

using Domain.Enums;
using Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Services;

public class GamificationService
{
    private static readonly TimeSpan UtcPlus7Offset = TimeSpan.FromHours(7);

    private readonly IUserRepository _userRepo;
    private readonly IPracticeSessionRepository _sessionRepo;
    private readonly IFESubmissionRepository _feSubmissionRepo;
    private readonly IPESubmissionRepository _peSubmissionRepo;
    private readonly IExamRepository _examRepo;
    private readonly IMemoryCache _memoryCache;

    public GamificationService(
        IUserRepository userRepo,
        IPracticeSessionRepository sessionRepo,
        IFESubmissionRepository feSubmissionRepo,
        IPESubmissionRepository peSubmissionRepo,
        IExamRepository examRepo,
        IMemoryCache memoryCache)
    {
        _userRepo = userRepo;
        _sessionRepo = sessionRepo;
        _feSubmissionRepo = feSubmissionRepo;
        _peSubmissionRepo = peSubmissionRepo;
        _examRepo = examRepo;
        _memoryCache = memoryCache;
    }

    public async Task<StudentSummaryDto?> GetSummaryAsync(string userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return null;

        var sessions = await _sessionRepo.GetByUserIdAsync(userId);
        var completedSessions = sessions
            .Where(s => s.Status == SessionStatus.Submitted || string.Equals(s.Status.ToString(), "Completed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var averageScore = completedSessions.Any() ? completedSessions.Average(s => s.TotalScore) : 0;
        var totalMinutes = completedSessions.Sum(s =>
            s.EndTime.HasValue
                ? (int)Math.Max(0, (s.EndTime.Value - s.StartTime).TotalMinutes)
                : 0);

        var totalProblemsSolved = 0;
        foreach (var session in completedSessions)
        {
            var feSubs = await _feSubmissionRepo.GetBySessionIdAsync(session.Id);
            var peSubs = await _peSubmissionRepo.GetBySessionIdAsync(session.Id);
            totalProblemsSolved += feSubs.Count + peSubs.Count;
        }

        var streak = await GetStreakAsync(userId);

        return new StudentSummaryDto
        {
            TotalProblemsSolved = totalProblemsSolved,
            AverageScore = Math.Round(averageScore, 2),
            TotalTimeSpentMinutes = totalMinutes,
            CurrentStreak = streak?.CurrentStreak ?? 0
        };
    }

    public async Task<StreakDto?> GetStreakAsync(string userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return null;

        var sessions = await _sessionRepo.GetByUserIdAsync(userId);
        var completedDays = sessions
            .Where(s => s.Status == SessionStatus.Submitted)
            .Select(s => (s.EndTime ?? s.StartTime) + UtcPlus7Offset)
            .Select(dt => dt.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        var todayLocal = (DateTime.UtcNow + UtcPlus7Offset).Date;
        var computedHighest = ComputeHighestStreak(completedDays);
        var currentStreak = ResolveCurrentLoginStreak(user, todayLocal);
        var streakHistory = completedDays.ToHashSet();

        if (user.LastLoginDate.HasValue)
        {
            var lastLoginLocalDate = DateTime.SpecifyKind(user.LastLoginDate.Value, DateTimeKind.Utc)
                .Add(UtcPlus7Offset)
                .Date;

            if (lastLoginLocalDate >= todayLocal.AddDays(-29) && lastLoginLocalDate <= todayLocal)
            {
                streakHistory.Add(lastLoginLocalDate);
            }
        }

        return new StreakDto
        {
            CurrentStreak = currentStreak,
            HighestStreak = Math.Max(user.HighestStreak, computedHighest),
            StreakHistory = streakHistory
                .Where(d => d >= todayLocal.AddDays(-29) && d <= todayLocal)
                .OrderBy(d => d)
                .Select(d => d)
                .ToList()
        };
    }

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(string? courseId, string scope, string currentUserId)
    {
        var normalizedScope = string.Equals(scope, "Course", StringComparison.OrdinalIgnoreCase)
            ? "Course"
            : "Global";

        var cacheKey = $"leaderboard:{normalizedScope}:{courseId ?? "all"}";
        if (!_memoryCache.TryGetValue(cacheKey, out List<LeaderboardEntryDto>? ranked) || ranked == null)
        {
            ranked = await BuildLeaderboardAsync(courseId, normalizedScope);
            _memoryCache.Set(cacheKey, ranked, TimeSpan.FromMinutes(15));
        }

        var top100 = ranked.Take(100).ToList();
        if (!top100.Any(x => x.UserId == currentUserId))
        {
            var current = ranked.FirstOrDefault(x => x.UserId == currentUserId);
            if (current != null) top100.Add(current);
        }

        return top100;
    }

    private async Task<List<LeaderboardEntryDto>> BuildLeaderboardAsync(string? courseId, string scope)
    {
        var users = await GetAllUsersAsync();
        var examCache = new Dictionary<string, Exam?>();
        var entries = new List<LeaderboardEntryDto>();

        foreach (var user in users)
        {
            var sessions = await GetAllSessionsByUserAsync(user.Id);
            var completed = sessions.Where(s => s.Status == SessionStatus.Submitted).ToList();

            if (string.Equals(scope, "Course", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(courseId))
            {
                completed = (await FilterSessionsByCourseAsync(completed, courseId, examCache)).ToList();
            }

            if (!completed.Any()) continue;

            entries.Add(new LeaderboardEntryDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                AvatarUrl = user.AvatarUrl,
                TotalScore = completed.Sum(s => s.TotalScore),
                ProblemsSolved = completed.Count
            });
        }

        var ranked = entries
            .OrderByDescending(e => e.TotalScore)
            .ThenByDescending(e => e.ProblemsSolved)
            .ThenBy(e => e.FullName)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
            ranked[i].Rank = i + 1;

        return ranked;
    }

    private async Task<List<User>> GetAllUsersAsync()
    {
        const int pageSize = 200;
        var page = 1;
        var users = new List<User>();

        while (true)
        {
            
            var (items, total) = await _userRepo.ListAsync(null, null, null, page, pageSize);
            if (!items.Any()) break;

            users.AddRange(items);
            if (users.Count >= total) break;
            page++;
        }

        return users;
    }

    private async Task<List<PracticeSession>> GetAllSessionsByUserAsync(string userId)
    {
        var total = await _sessionRepo.CountByUserIdAsync(userId);
        if (total <= 0) return new List<PracticeSession>();

        const int pageSize = 100;
        var page = 1;
        var sessions = new List<PracticeSession>();

        while (sessions.Count < total)
        {
            var batch = await _sessionRepo.GetByUserIdAsync(userId, page, pageSize);
            if (!batch.Any()) break;
            sessions.AddRange(batch);
            if (batch.Count < pageSize) break;
            page++;
        }

        return sessions;
    }

    private async Task<IEnumerable<PracticeSession>> FilterSessionsByCourseAsync(
        IEnumerable<PracticeSession> sessions,
        string courseId,
        Dictionary<string, Exam?> examCache)
    {
        var result = new List<PracticeSession>();
        foreach (var session in sessions)
        {
            if (!examCache.TryGetValue(session.ExamId, out var exam))
            {
                exam = await _examRepo.GetByIdAsync(session.ExamId);
                examCache[session.ExamId] = exam;
            }

            if (exam != null && string.Equals(exam.CourseId, courseId, StringComparison.OrdinalIgnoreCase))
                result.Add(session);
        }

        return result;
    }

    private static int ComputeHighestStreak(List<DateTime> daysDescending)
    {
        if (daysDescending.Count == 0) return 0;

        var days = daysDescending.OrderBy(d => d).ToList();
        var best = 1;
        var run = 1;

        for (var i = 1; i < days.Count; i++)
        {
            if (days[i] == days[i - 1].AddDays(1))
            {
                run++;
                if (run > best) best = run;
            }
            else
            {
                run = 1;
            }
        }

        return best;
    }

    private static int ResolveCurrentLoginStreak(User user, DateTime todayLocal)
    {
        if (!user.LastLoginDate.HasValue)
        {
            return Math.Max(0, user.CurrentStreak);
        }

        var lastLoginLocalDate = DateTime.SpecifyKind(user.LastLoginDate.Value, DateTimeKind.Utc)
            .Add(UtcPlus7Offset)
            .Date;
        var dayGap = (todayLocal - lastLoginLocalDate).Days;

        if (dayGap <= 1)
        {
            return Math.Max(0, user.CurrentStreak);
        }

        return 0;
    }
}
