using Application.DTOs;

namespace Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> GoogleLoginAsync(
        string idToken,
        string ipAddress);

    Task<AuthResponse> RefreshTokenAsync(
        string refreshToken,
        string ipAddress);

    Task TouchDailyActivityAsync(
        string userId);

    Task LogoutAsync(
        string userId,
        string refreshToken);
}
