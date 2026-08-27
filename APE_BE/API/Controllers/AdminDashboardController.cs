using Application.Common;
using Application.DTOs;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace API.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController : ControllerBase
{
    private static readonly TimeZoneInfo DashboardTimeZone = ResolveDashboardTimeZone();
    private readonly DbContext _dbContext;

    public AdminDashboardController(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("analytics")]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardAnalyticsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] string period = "month",
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPeriod = NormalizePeriod(period);
        var range = ResolveRange(normalizedPeriod, fromDate, toDate);

        var usersTask = _dbContext.Users
            .Find(Builders<User>.Filter.Eq(item => item.Role, "Student"))
            .ToListAsync(cancellationToken);

        var paymentsTask = _dbContext.Payments
            .Find(Builders<Payment>.Filter.Where(item =>
                item.IsWalletCredited &&
                item.Status == PaymentStatus.Completed))
            .ToListAsync(cancellationToken);

        var billingTask = _dbContext.AIVndBillingTransactions
            .Find(Builders<AIVndBillingTransaction>.Filter.Where(item =>
                item.CreatedAt >= range.FromDate &&
                item.CreatedAt <= range.ToDate))
            .ToListAsync(cancellationToken);

        var practiceTask = _dbContext.PracticeSessions
            .Find(Builders<PracticeSession>.Filter.Where(item =>
                item.StartTime >= range.FromDate &&
                item.StartTime <= range.ToDate))
            .ToListAsync(cancellationToken);

        var usageTask = _dbContext.APIUsageLogs
            .Find(Builders<AIUsageLog>.Filter.Where(item =>
                item.CreatedAt >= range.FromDate &&
                item.CreatedAt <= range.ToDate))
            .ToListAsync(cancellationToken);

        await Task.WhenAll(usersTask, paymentsTask, billingTask, practiceTask, usageTask);

        var users = usersTask.Result;
        var payments = paymentsTask.Result
            .Where(item =>
            {
                var paymentDate = ResolvePaymentDate(item);
                return paymentDate >= range.FromDate && paymentDate <= range.ToDate;
            })
            .ToList();
        var billings = billingTask.Result;
        var practiceSessions = practiceTask.Result;
        var usageLogs = usageTask.Result;

        var userMap = users.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        var aiFinance = BuildAiFinance(payments, billings, usageLogs);
        var practice = BuildPractice(practiceSessions);
        var timeline = BuildTimeline(normalizedPeriod, range.FromDate, range.ToDate, payments, billings, practiceSessions);
        var aiFeatures = BuildFeatureStats(billings);
        var highlights = BuildHighlights(userMap, payments, billings, practiceSessions, usageLogs);

        var result = new AdminDashboardAnalyticsDto
        {
            Period = normalizedPeriod,
            FromDate = range.FromDate,
            ToDate = range.ToDate,
            AiFinance = aiFinance,
            Practice = practice,
            Timeline = timeline,
            AiFeatures = aiFeatures,
            Highlights = highlights
        };

        return Ok(ApiResponse.Ok(result));
    }

    private static string NormalizePeriod(string? period)
    {
        if (string.Equals(period, "day", StringComparison.OrdinalIgnoreCase))
        {
            return "day";
        }

        if (string.Equals(period, "week", StringComparison.OrdinalIgnoreCase))
        {
            return "week";
        }

        return "month";
    }

    private static (DateTime FromDate, DateTime ToDate) ResolveRange(string period, DateTime? fromDate, DateTime? toDate)
    {
        if (fromDate.HasValue || toDate.HasValue)
        {
            var currentLocalTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, DashboardTimeZone).DateTime;
            var requestedLocalFrom = DateTime.SpecifyKind(fromDate ?? currentLocalTime.Date, DateTimeKind.Unspecified);
            var requestedLocalTo = DateTime.SpecifyKind(toDate ?? currentLocalTime, DateTimeKind.Unspecified);
            var resolvedFrom = TimeZoneInfo.ConvertTimeToUtc(requestedLocalFrom, DashboardTimeZone);
            var resolvedTo = TimeZoneInfo.ConvertTimeToUtc(requestedLocalTo, DashboardTimeZone);
            return (resolvedFrom, resolvedTo);
        }

        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, DashboardTimeZone).DateTime;
        var today = localNow.Date;

        DateTime localFrom;
        DateTime localTo;

        switch (period)
        {
            case "day":
                localFrom = today;
                localTo = today.AddDays(1).AddTicks(-1);
                break;
            case "week":
                localFrom = today.AddDays(-6);
                localTo = today.AddDays(1).AddTicks(-1);
                break;
            default:
                localFrom = new DateTime(today.Year, 1, 1);
                localTo = new DateTime(today.Year, 12, 31, 23, 59, 59, 999).AddTicks(9999);
                break;
        }

        return (
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localFrom, DateTimeKind.Unspecified), DashboardTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localTo, DateTimeKind.Unspecified), DashboardTimeZone));
    }

    private static DateTime ResolvePaymentDate(Payment payment)
        => payment.ProcessedAt ?? payment.PaidAt ?? payment.CreatedAt;

    private static AdminDashboardAiFinanceDto BuildAiFinance(
        List<Payment> payments,
        List<AIVndBillingTransaction> billings,
        List<AIUsageLog> usageLogs)
    {
        var activeAiUsers = new HashSet<string>(
            usageLogs
                .Select(item => item.TriggeredBy?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))!,
            StringComparer.OrdinalIgnoreCase);

        var totalTopup = payments.Sum(item => item.AmountVnd);
        var totalRevenue = billings.Sum(item => item.ActualDeductedVnd);
        var totalActualCost = billings.Sum(item => item.ActualCostVnd);
        var totalCharged = billings.Sum(item => item.ChargedVnd);
        var totalAbsorbed = billings.Sum(item => item.AbsorbedVnd);

        return new AdminDashboardAiFinanceDto
        {
            TotalTopupVnd = totalTopup,
            TotalAiRevenueVnd = totalRevenue,
            TotalAiActualCostVnd = totalActualCost,
            TotalAiChargedVnd = totalCharged,
            TotalAiAbsorbedVnd = totalAbsorbed,
            PricingMarginVnd = totalCharged - totalActualCost,
            RealizedNetVnd = totalRevenue - totalActualCost,
            ActiveAiUsers = activeAiUsers.Count,
            AiTransactionCount = billings.Count,
            AiCallCount = usageLogs.Count
        };
    }

    private static AdminDashboardPracticeDto BuildPractice(List<PracticeSession> practiceSessions)
    {
        var completedSessions = practiceSessions
            .Where(item => item.Status == SessionStatus.Submitted || item.Status == SessionStatus.Timeout)
            .ToList();

        return new AdminDashboardPracticeDto
        {
            TotalSessions = practiceSessions.Count,
            CompletedSessions = completedSessions.Count,
            ActiveStudents = practiceSessions
                .Select(item => item.StudentId)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            AverageScore = completedSessions.Count == 0 ? 0 : Math.Round(completedSessions.Average(item => item.TotalScore), 2),
            AverageDurationMinutes = practiceSessions.Count == 0 ? 0 : Math.Round(practiceSessions.Average(item => item.ActiveDurationSeconds) / 60d, 1)
        };
    }

    private static List<AdminDashboardTrendPointDto> BuildTimeline(
        string period,
        DateTime fromDate,
        DateTime toDate,
        List<Payment> payments,
        List<AIVndBillingTransaction> billings,
        List<PracticeSession> practiceSessions)
    {
        var localFrom = TimeZoneInfo.ConvertTimeFromUtc(NormalizeUtc(fromDate), DashboardTimeZone);
        var localTo = TimeZoneInfo.ConvertTimeFromUtc(NormalizeUtc(toDate), DashboardTimeZone);

        if (period == "day")
        {
            return Enumerable.Range(0, 24)
                .Select(hour => BuildHourlyPoint(hour, localFrom, payments, billings, practiceSessions))
                .ToList();
        }

        if (period == "month")
        {
            var startMonth = new DateTime(localFrom.Year, localFrom.Month, 1);
            var endMonth = new DateTime(localTo.Year, localTo.Month, 1);
            var monthCount = ((endMonth.Year - startMonth.Year) * 12) + endMonth.Month - startMonth.Month + 1;

            return Enumerable.Range(0, monthCount)
                .Select(offset => BuildMonthlyPoint(startMonth.AddMonths(offset), payments, billings, practiceSessions))
                .ToList();
        }

        var dayCount = Math.Max(1, (localTo.Date - localFrom.Date).Days + 1);
        return Enumerable.Range(0, dayCount)
            .Select(offset => BuildDailyPoint(localFrom.Date.AddDays(offset), payments, billings, practiceSessions))
            .ToList();
    }

    private static AdminDashboardTrendPointDto BuildHourlyPoint(
        int hour,
        DateTime localDayStart,
        List<Payment> payments,
        List<AIVndBillingTransaction> billings,
        List<PracticeSession> practiceSessions)
    {
        var bucketStartLocal = localDayStart.Date.AddHours(hour);
        var bucketEndLocal = bucketStartLocal.AddHours(1);
        var bucketPayments = payments.Where(item => IsBetweenLocal(ResolvePaymentDate(item), bucketStartLocal, bucketEndLocal)).ToList();
        var bucketBillings = billings.Where(item => IsBetweenLocal(item.CreatedAt, bucketStartLocal, bucketEndLocal)).ToList();
        var bucketPractice = practiceSessions.Where(item => IsBetweenLocal(item.StartTime, bucketStartLocal, bucketEndLocal)).ToList();

        return new AdminDashboardTrendPointDto
        {
            Label = $"{hour:00}:00",
            PeriodStart = bucketStartLocal,
            TopupVnd = bucketPayments.Sum(item => item.AmountVnd),
            AiRevenueVnd = bucketBillings.Sum(item => item.ActualDeductedVnd),
            AiActualCostVnd = bucketBillings.Sum(item => item.ActualCostVnd),
            AiAbsorbedVnd = bucketBillings.Sum(item => item.AbsorbedVnd),
            PracticeSessions = bucketPractice.Count,
            ActiveStudents = bucketPractice
                .Select(item => item.StudentId)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            AverageScore = CalculateAverageScore(bucketPractice)
        };
    }

    private static AdminDashboardTrendPointDto BuildDailyPoint(
        DateTime localDayStart,
        List<Payment> payments,
        List<AIVndBillingTransaction> billings,
        List<PracticeSession> practiceSessions)
    {
        var localDayEnd = localDayStart.AddDays(1);
        var bucketPayments = payments.Where(item => IsBetweenLocal(ResolvePaymentDate(item), localDayStart, localDayEnd)).ToList();
        var bucketBillings = billings.Where(item => IsBetweenLocal(item.CreatedAt, localDayStart, localDayEnd)).ToList();
        var bucketPractice = practiceSessions.Where(item => IsBetweenLocal(item.StartTime, localDayStart, localDayEnd)).ToList();

        return new AdminDashboardTrendPointDto
        {
            Label = localDayStart.ToString("dd/MM"),
            PeriodStart = localDayStart,
            TopupVnd = bucketPayments.Sum(item => item.AmountVnd),
            AiRevenueVnd = bucketBillings.Sum(item => item.ActualDeductedVnd),
            AiActualCostVnd = bucketBillings.Sum(item => item.ActualCostVnd),
            AiAbsorbedVnd = bucketBillings.Sum(item => item.AbsorbedVnd),
            PracticeSessions = bucketPractice.Count,
            ActiveStudents = bucketPractice
                .Select(item => item.StudentId)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            AverageScore = CalculateAverageScore(bucketPractice)
        };
    }

    private static AdminDashboardTrendPointDto BuildMonthlyPoint(
        DateTime localMonthStart,
        List<Payment> payments,
        List<AIVndBillingTransaction> billings,
        List<PracticeSession> practiceSessions)
    {
        var localMonthEnd = localMonthStart.AddMonths(1);
        var bucketPayments = payments.Where(item => IsBetweenLocal(ResolvePaymentDate(item), localMonthStart, localMonthEnd)).ToList();
        var bucketBillings = billings.Where(item => IsBetweenLocal(item.CreatedAt, localMonthStart, localMonthEnd)).ToList();
        var bucketPractice = practiceSessions.Where(item => IsBetweenLocal(item.StartTime, localMonthStart, localMonthEnd)).ToList();

        return new AdminDashboardTrendPointDto
        {
            Label = localMonthStart.ToString("MM/yyyy"),
            PeriodStart = localMonthStart,
            TopupVnd = bucketPayments.Sum(item => item.AmountVnd),
            AiRevenueVnd = bucketBillings.Sum(item => item.ActualDeductedVnd),
            AiActualCostVnd = bucketBillings.Sum(item => item.ActualCostVnd),
            AiAbsorbedVnd = bucketBillings.Sum(item => item.AbsorbedVnd),
            PracticeSessions = bucketPractice.Count,
            ActiveStudents = bucketPractice
                .Select(item => item.StudentId)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            AverageScore = CalculateAverageScore(bucketPractice)
        };
    }

    private static List<AdminDashboardFeatureStatDto> BuildFeatureStats(List<AIVndBillingTransaction> billings)
    {
        return billings
            .GroupBy(item => string.IsNullOrWhiteSpace(item.FeatureKey) ? "Unknown" : item.FeatureKey)
            .Select(group => new AdminDashboardFeatureStatDto
            {
                FeatureKey = group.Key,
                TransactionCount = group.Count(),
                UserCount = group
                    .Select(item => item.UserId)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                AiRevenueVnd = group.Sum(item => item.ActualDeductedVnd),
                AiActualCostVnd = group.Sum(item => item.ActualCostVnd),
                AiAbsorbedVnd = group.Sum(item => item.AbsorbedVnd),
                PricingMarginVnd = group.Sum(item => item.ChargedVnd - item.ActualCostVnd)
            })
            .OrderByDescending(item => item.AiRevenueVnd)
            .ThenByDescending(item => item.TransactionCount)
            .Take(8)
            .ToList();
    }

    private static List<AdminDashboardUserHighlightDto> BuildHighlights(
        IReadOnlyDictionary<string, User> userMap,
        List<Payment> payments,
        List<AIVndBillingTransaction> billings,
        List<PracticeSession> practiceSessions,
        List<AIUsageLog> usageLogs)
    {
        var topupByUser = payments
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.AmountVnd), StringComparer.OrdinalIgnoreCase);

        var billingByUser = billings
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var practiceByUser = practiceSessions
            .GroupBy(item => item.StudentId)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var aiCallsByUser = usageLogs
            .Where(item => !string.IsNullOrWhiteSpace(item.TriggeredBy))
            .GroupBy(item => item.TriggeredBy!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var userIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var userId in topupByUser.Keys) userIds.Add(userId);
        foreach (var userId in billingByUser.Keys) userIds.Add(userId);
        foreach (var userId in practiceByUser.Keys) userIds.Add(userId);
        foreach (var userId in aiCallsByUser.Keys) userIds.Add(userId);

        return userIds
            .Select(userId =>
            {
                userMap.TryGetValue(userId, out var user);
                billingByUser.TryGetValue(userId, out var userBillings);
                practiceByUser.TryGetValue(userId, out var userPractice);

                userBillings ??= [];
                userPractice ??= [];

                var completedPractice = userPractice
                    .Where(item => item.Status == SessionStatus.Submitted || item.Status == SessionStatus.Timeout)
                    .ToList();

                return new AdminDashboardUserHighlightDto
                {
                    UserId = userId,
                    FullName = user?.FullName ?? userId,
                    Email = user?.Email ?? string.Empty,
                    TopupVnd = topupByUser.GetValueOrDefault(userId, 0),
                    AiRevenueVnd = userBillings.Sum(item => item.ActualDeductedVnd),
                    AiActualCostVnd = userBillings.Sum(item => item.ActualCostVnd),
                    AiAbsorbedVnd = userBillings.Sum(item => item.AbsorbedVnd),
                    AiCalls = aiCallsByUser.GetValueOrDefault(userId, 0),
                    PracticeSessions = userPractice.Count,
                    AverageScore = completedPractice.Count == 0 ? 0 : Math.Round(completedPractice.Average(item => item.TotalScore), 2),
                    CurrentWalletBalanceVnd = user?.AiWalletBalanceVnd ?? 0
                };
            })
            .OrderByDescending(item => item.AiRevenueVnd)
            .ThenByDescending(item => item.PracticeSessions)
            .ThenByDescending(item => item.AiCalls)
            .Take(10)
            .ToList();
    }

    private static bool IsBetween(DateTime value, DateTime startInclusive, DateTime endExclusive)
        => value >= startInclusive && value < endExclusive;

    private static bool IsBetweenLocal(DateTime utcValue, DateTime localStartInclusive, DateTime localEndExclusive)
    {
        var localValue = TimeZoneInfo.ConvertTimeFromUtc(NormalizeUtc(utcValue), DashboardTimeZone);
        return localValue >= localStartInclusive && localValue < localEndExclusive;
    }

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static double CalculateAverageScore(List<PracticeSession> practiceSessions)
    {
        var completedSessions = practiceSessions
            .Where(item => item.Status == SessionStatus.Submitted || item.Status == SessionStatus.Timeout)
            .ToList();

        return completedSessions.Count == 0 ? 0 : Math.Round(completedSessions.Average(item => item.TotalScore), 2);
    }

    private static TimeZoneInfo ResolveDashboardTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Saigon");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
