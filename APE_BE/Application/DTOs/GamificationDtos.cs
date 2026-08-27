namespace Application.DTOs;

public class StreakDto
{
    public int CurrentStreak { get; set; }
    public int HighestStreak { get; set; }
    public List<DateTime> StreakHistory { get; set; } = new();
}

public class BadgeDto
{
    public string BadgeName { get; set; } = null!;
    public string Description { get; set; } = null!;
    public bool IsEarned { get; set; }
    public int ProgressValue { get; set; }
    public int ProgressTarget { get; set; }
    public DateTime? EarnedDate { get; set; }
}

public class LeaderboardEntryDto
{
    public int Rank { get; set; }
    public string UserId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public double TotalScore { get; set; }
    public int ProblemsSolved { get; set; }
}
