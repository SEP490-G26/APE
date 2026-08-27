using Domain.Entities;

namespace Application.Interfaces;

public interface IUserRepository
{
    #region Query

    Task<User?> GetByIdAsync(string id);

    Task<User?> GetByEmailAsync(string email);

    Task<User?> GetByGoogleIdAsync(string googleId);

    Task<User?> GetByRefreshTokenAsync(string refreshToken);

    Task<bool> ExistsByEmailAsync(string email);

    Task<(List<User> Items, long Total)> ListAsync(
        string? search,
        string? role,
        string? status,
        int page,
        int limit);

    #endregion

    #region Command

    Task CreateAsync(User user);

    Task UpdateAsync(User user);

    Task<bool> UpdateUserStatusAsync(string userId, string status, bool clearRefreshTokens);

    #endregion

    #region Refresh Token

    Task AddRefreshTokenAsync(
        string userId,
        RefreshToken refreshToken);

    Task RevokeRefreshTokenAsync(
        string userId,
        string refreshToken);

    Task RemoveExpiredRefreshTokensAsync(
        string userId);

    #endregion
}