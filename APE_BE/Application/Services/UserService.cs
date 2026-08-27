using Application.Interfaces;
using Application.DTOs;
using Domain.Entities;

namespace Application.Services;

public class UserService
{
    private readonly IUserRepository _userRepo;
    private readonly IPracticeSessionRepository _sessionRepo;
    private readonly IFESubmissionRepository _feSubmissionRepo;
    private readonly IPESubmissionRepository _peSubmissionRepo;

    public UserService(
        IUserRepository userRepo,
        IPracticeSessionRepository sessionRepo,
        IFESubmissionRepository feSubmissionRepo,
        IPESubmissionRepository peSubmissionRepo)
    {
        _userRepo = userRepo;
        _sessionRepo = sessionRepo;
        _feSubmissionRepo = feSubmissionRepo;
        _peSubmissionRepo = peSubmissionRepo;
    }

    public async Task<UserProfileDto?> GetProfileAsync(string userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return null;

        var totalSessions = await _sessionRepo.CountByUserIdAsync(userId);

      
        int totalPE = 0;
        int pePassed = 0;
        double avgPEScore = 0;

        var feSubs = await _feSubmissionRepo.GetByUserIdAsync(userId);
        int totalFE = feSubs.Count;
        int feCorrect = feSubs.Count(s => s.IsCorrect);
        double feRate = totalFE > 0 ? (double)feCorrect / totalFE * 100 : 0;

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            ExpPoints = user.ExpPoints,
            AiWalletBalanceVnd = user.AiWalletBalanceVnd,
            CurrentStreak = user.CurrentStreak,
            HighestStreak = user.HighestStreak,
            Badges = user.Badges,
            LastPracticeDate = user.LastPracticeDate,
            TotalPracticeSessions = totalSessions,
            TotalPESubmissions = totalPE,
            TotalPEPassed = pePassed,
            AveragePEScore = Math.Round(avgPEScore, 2),
            TotalFESubmissions = totalFE,
            TotalFECorrect = feCorrect,
            FECorrectRate = Math.Round(feRate, 2)
        };
    }

    public async Task<UserProfileDto?> UpdateProfileAsync(string userId, UpdateProfileDto dto)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return null;

        if (!string.IsNullOrWhiteSpace(dto.FullName))
            user.FullName = dto.FullName;
        if (dto.AvatarUrl != null)
            user.AvatarUrl = dto.AvatarUrl;

        await _userRepo.UpdateAsync(user);
        return await GetProfileAsync(userId);
    }

}
