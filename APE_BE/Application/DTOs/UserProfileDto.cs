using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs
{
    public class UserProfileDto
    {
        // Thông tin cá nhân
        public string Id { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = null!;
        public int ExpPoints { get; set; }
        public long AiWalletBalanceVnd { get; set; }
        public int CurrentStreak { get; set; }
        public int HighestStreak { get; set; }
        public List<string> Badges { get; set; } = new();
        public DateTime? LastPracticeDate { get; set; }

        // Thống kê tổng quan
        public int TotalPracticeSessions { get; set; }

        // Riêng PE
        public int TotalPESubmissions { get; set; }
        public int TotalPEPassed { get; set; }          // số bài PE đạt (passed)
        public double AveragePEScore { get; set; }       // điểm trung bình PE

        // Riêng FE
        public int TotalFESubmissions { get; set; }
        public int TotalFECorrect { get; set; }          // số câu FE đúng
        public double FECorrectRate { get; set; }        // tỷ lệ đúng (%)
    }
}
