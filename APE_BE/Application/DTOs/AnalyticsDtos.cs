namespace Application.DTOs;

public class StudentSummaryDto
{
    public int TotalProblemsSolved { get; set; }
    public double AverageScore { get; set; }
    public int TotalTimeSpentMinutes { get; set; }
    public int CurrentStreak { get; set; }
}
