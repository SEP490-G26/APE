using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    public UserRepository(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _users = context.Users;
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return await _users
            .Find(user => user.Id == id.Trim())
            .FirstOrDefaultAsync();
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        string normalizedEmail = email.Trim();

        return await _users
            .Find(user => user.Email == normalizedEmail)
            .FirstOrDefaultAsync();
    }

    public async Task<User?> GetByGoogleIdAsync(string googleId)
    {
        if (string.IsNullOrWhiteSpace(googleId))
        {
            return null;
        }

        string normalizedGoogleId = googleId.Trim();

        return await _users
            .Find(user => user.GoogleId == normalizedGoogleId)
            .FirstOrDefaultAsync();
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        string normalizedRefreshToken = refreshToken.Trim();

        return await _users
            .Find(user =>
                user.RefreshTokens.Any(token =>
                    token.Token == normalizedRefreshToken))
            .FirstOrDefaultAsync();
    }

    public Task<bool> ExistsByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult(false);
        }

        return _users
            .Find(user => user.Email == email.Trim())
            .AnyAsync();
    }

    public Task CreateAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return _users.InsertOneAsync(user);
    }

    public async Task UpdateAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var result = await _users.ReplaceOneAsync(
            item => item.Id == user.Id,
            user);

        if (result.MatchedCount == 0)
        {
            throw new InvalidOperationException(
                $"User '{user.Id}' was not found.");
        }
    }

    public async Task<bool> UpdateUserStatusAsync(
        string userId,
        string status,
        bool clearRefreshTokens)
    {
        var updates = new List<UpdateDefinition<User>>
        {
            Builders<User>.Update.Set(
                user => user.Status,
                status)
        };

        if (clearRefreshTokens)
        {
            updates.Add(
                Builders<User>.Update.Set(
                    user => user.RefreshTokens,
                    new List<RefreshToken>()));
        }

        var update =
            Builders<User>.Update.Combine(updates);

        var result = await _users.UpdateOneAsync(
            user => user.Id == userId,
            update);

        return result.MatchedCount > 0;
    }

    public Task AddRefreshTokenAsync(
        string userId,
        RefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var update = Builders<User>.Update.Push(
            user => user.RefreshTokens,
            token);

        return _users.UpdateOneAsync(
            user => user.Id == userId,
            update);
    }

    public Task RevokeRefreshTokenAsync(
        string userId,
        string refreshToken)
    {
        var update = Builders<User>.Update.PullFilter(
            user => user.RefreshTokens,
            token => token.Token == refreshToken);

        return _users.UpdateOneAsync(
            user => user.Id == userId,
            update);
    }

    public Task RemoveExpiredRefreshTokensAsync(
        string userId)
    {
        var update = Builders<User>.Update.PullFilter(
            user => user.RefreshTokens,
            token => token.Expires <= DateTime.UtcNow);

        return _users.UpdateOneAsync(
            user => user.Id == userId,
            update);
    }

    public async Task<(List<User> Items, long Total)> ListAsync(
        string? search,
        string? role,
        string? status,
        int page,
        int limit)
    {
        var filters =
            new List<FilterDefinition<User>>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(
                search.Trim(),
                "i");

            filters.Add(
                Builders<User>.Filter.Or(
                    Builders<User>.Filter.Regex(
                        user => user.Email,
                        regex),
                    Builders<User>.Filter.Regex(
                        user => user.FullName,
                        regex)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            filters.Add(
                Builders<User>.Filter.Eq(
                    user => user.Role,
                    role.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filters.Add(
                Builders<User>.Filter.Eq(
                    user => user.Status,
                    status.Trim()));
        }

        var filter = filters.Count == 0
            ? Builders<User>.Filter.Empty
            : Builders<User>.Filter.And(filters);

        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        var total = await _users.CountDocumentsAsync(filter);

        var items = await _users
            .Find(filter)
            .SortByDescending(user => user.ExpPoints)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync();

        return (items, total);
    }
}