namespace Application.DTOs;

public class AdminDashboardAnalyticsDto
{
    public string Period { get; set; } = "month";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public AdminDashboardAiFinanceDto AiFinance { get; set; } = new();
    public AdminDashboardPracticeDto Practice { get; set; } = new();
    public List<AdminDashboardTrendPointDto> Timeline { get; set; } = new();
    public List<AdminDashboardFeatureStatDto> AiFeatures { get; set; } = new();
    public List<AdminDashboardUserHighlightDto> Highlights { get; set; } = new();
}

public class AdminDashboardAiFinanceDto
{
    public long TotalTopupVnd { get; set; }
    public long TotalAiRevenueVnd { get; set; }
    public long TotalAiActualCostVnd { get; set; }
    public long TotalAiChargedVnd { get; set; }
    public long TotalAiAbsorbedVnd { get; set; }
    public long PricingMarginVnd { get; set; }
    public long RealizedNetVnd { get; set; }
    public int ActiveAiUsers { get; set; }
    public int AiTransactionCount { get; set; }
    public int AiCallCount { get; set; }
}

public class AdminDashboardPracticeDto
{
    public int TotalSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int ActiveStudents { get; set; }
    public double AverageScore { get; set; }
    public double AverageDurationMinutes { get; set; }
}

public class AdminDashboardTrendPointDto
{
    public string Label { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public long TopupVnd { get; set; }
    public long AiRevenueVnd { get; set; }
    public long AiActualCostVnd { get; set; }
    public long AiAbsorbedVnd { get; set; }
    public int PracticeSessions { get; set; }
    public int ActiveStudents { get; set; }
    public double AverageScore { get; set; }
}

public class AdminDashboardFeatureStatDto
{
    public string FeatureKey { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public int UserCount { get; set; }
    public long AiRevenueVnd { get; set; }
    public long AiActualCostVnd { get; set; }
    public long AiAbsorbedVnd { get; set; }
    public long PricingMarginVnd { get; set; }
}

public class AdminDashboardUserHighlightDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public long TopupVnd { get; set; }
    public long AiRevenueVnd { get; set; }
    public long AiActualCostVnd { get; set; }
    public long AiAbsorbedVnd { get; set; }
    public int AiCalls { get; set; }
    public int PracticeSessions { get; set; }
    public double AverageScore { get; set; }
    public long CurrentWalletBalanceVnd { get; set; }
}
