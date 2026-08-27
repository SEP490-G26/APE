using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed class AuthService : IAuthService
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    private readonly IUserRepository _userRepository;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IGoogleAuthService googleAuthService,
        IJwtTokenGenerator jwtTokenGenerator,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _googleAuthService = googleAuthService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _logger = logger;
    }

    public async Task<AuthResponse> GoogleLoginAsync(
     string idToken,
     string ipAddress)
    {
        var payload = await _googleAuthService.VerifyGoogleTokenAsync(idToken);


        var user = await _userRepository.GetByGoogleIdAsync(
    payload.Sub);

        if (user is null)
        {
            user = await _userRepository.GetByEmailAsync(
                payload.Email);

            if (user is not null)
            {
                user.GoogleId = payload.Sub;
                user.FullName = payload.Name;
                user.AvatarUrl = payload.Picture;

                await _userRepository.UpdateAsync(user);
            }
            else
            {
                user = await CreateUserAsync(payload);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(user.GoogleId))
            {
                user.GoogleId = payload.Sub;
            }
        }

        EnsureUserCanLogin(user);
        ApplyLoginStreak(user);

        await _userRepository.UpdateAsync(user);
        await _userRepository.RemoveExpiredRefreshTokensAsync(user.Id);

        var accessToken =
            _jwtTokenGenerator.GenerateAccessToken(user);

        var (refreshToken, expires) =
            _jwtTokenGenerator.GenerateRefreshTokenWithExpiry();

        await _userRepository.AddRefreshTokenAsync(
            user.Id,
            new RefreshToken
            {
                Token = refreshToken,
                Expires = expires,
                Created = DateTime.UtcNow,
                CreatedByIp = ipAddress
            });

        _logger.LogInformation(
            "User {Email} login successfully.",
            user.Email);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expires,
            User = MapUser(user)
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(
        string refreshToken,
        string ipAddress)
    {
        var user =
            await _userRepository.GetByRefreshTokenAsync(refreshToken);

        if (user is null)
            throw new UnauthorizedException("Refresh token is invalid.");

        EnsureUserCanLogin(user);
        ApplyLoginStreak(user);
        await _userRepository.UpdateAsync(user);

        var oldToken = user.RefreshTokens
            .FirstOrDefault(x => x.Token == refreshToken);

        if (oldToken is null || !oldToken.IsActive)
            throw new UnauthorizedException("Refresh token is expired.");

        await _userRepository.RevokeRefreshTokenAsync(
            user.Id,
            refreshToken);

        var accessToken =
            _jwtTokenGenerator.GenerateAccessToken(user);

        var (newRefreshToken, expires) =
            _jwtTokenGenerator.GenerateRefreshTokenWithExpiry();

        await _userRepository.AddRefreshTokenAsync(
            user.Id,
            new RefreshToken
            {
                Token = newRefreshToken,
                Expires = expires,
                Created = DateTime.UtcNow,
                CreatedByIp = ipAddress
            });

        _logger.LogInformation(
            "Refresh token renewed for {Email}.",
            user.Email);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = expires,
            User = MapUser(user)
        };
    }

    public async Task TouchDailyActivityAsync(
        string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return;
        }

        EnsureUserCanLogin(user);
        var beforeCurrent = user.CurrentStreak;
        var beforeHighest = user.HighestStreak;
        var beforeLastLogin = user.LastLoginDate;

        ApplyLoginStreak(user);

        if (beforeCurrent != user.CurrentStreak ||
            beforeHighest != user.HighestStreak ||
            beforeLastLogin != user.LastLoginDate)
        {
            await _userRepository.UpdateAsync(user);
        }
    }

    public async Task LogoutAsync(
        string userId,
        string refreshToken)
    {
        await _userRepository.RevokeRefreshTokenAsync(
            userId,
            refreshToken);

        _logger.LogInformation(
            "User {UserId} logged out.",
            userId);
    }

    private async Task<User> CreateUserAsync(
        GoogleUserPayload payload)
    {
        var user = new User
        {
            GoogleId = payload.Sub,
            Email = payload.Email,
            FullName = payload.Name,
            AvatarUrl = payload.Picture,
            Role = "Student",
            Status = "Active",
            ExpPoints = 0,
            AiWalletBalanceVnd = 0,
            CurrentStreak = 1,
            HighestStreak = 1,
            LastLoginDate = DateTime.UtcNow,
            LastPracticeDate = null,
            Badges = new(),
            RefreshTokens = new()
        };

        await _userRepository.CreateAsync(user);

        _logger.LogInformation(
            "New user created: {Email}",
            user.Email);

        return user;
    }

    private static void EnsureUserCanLogin(User user)
    {
        if (!string.Equals(
            user.Status,
            "Active",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException(
                "Your account has been disabled.");
        }
    }

    private static void ApplyLoginStreak(User user)
    {
        var nowUtc = DateTime.UtcNow;
        var today = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, VietnamTimeZone).Date;

        if (!user.LastLoginDate.HasValue)
        {
            user.CurrentStreak = Math.Max(1, user.CurrentStreak);
            user.HighestStreak = Math.Max(user.HighestStreak, user.CurrentStreak);
            user.LastLoginDate = nowUtc;
            return;
        }

        var lastLoginLocalDate = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(user.LastLoginDate.Value, DateTimeKind.Utc),
            VietnamTimeZone).Date;

        var dayGap = (today - lastLoginLocalDate).Days;

        if (dayGap <= 0)
        {
            user.LastLoginDate = nowUtc;
            return;
        }

        user.CurrentStreak = dayGap == 1
            ? Math.Max(1, user.CurrentStreak + 1)
            : 1;

        user.HighestStreak = Math.Max(user.HighestStreak, user.CurrentStreak);
        user.LastLoginDate = nowUtc;
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static UserProfileDto MapUser(User user)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            ExpPoints = user.ExpPoints,
            AiWalletBalanceVnd = user.AiWalletBalanceVnd,
            CurrentStreak = user.CurrentStreak,
            HighestStreak = user.HighestStreak,
            Badges = user.Badges,
            LastPracticeDate = user.LastPracticeDate,

            TotalPracticeSessions = 0,

            TotalPESubmissions = 0,
            TotalPEPassed = 0,
            AveragePEScore = 0,

            TotalFESubmissions = 0,
            TotalFECorrect = 0,
            FECorrectRate = 0
        };
    }
}
