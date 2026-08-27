using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class AIContextPackRepository : IAIContextPackRepository
{
    private readonly IMongoCollection<AIContextPack> _collection;

    public AIContextPackRepository(DbContext dbContext)
    {
        _collection = dbContext.AIContextPacks;
    }

    public async Task<AIContextPack> CreateAsync(AIContextPack pack)
    {
        await _collection.InsertOneAsync(pack);
        return pack;
    }

    public async Task<AIContextPack?> GetByIdAsync(string packId)
        => await _collection.Find(item => item.Id == packId).FirstOrDefaultAsync();

    public async Task<List<AIContextPack>> ListAsync(
        string userId,
        string? subject,
        string? questionType,
        string? difficulty,
        string? topic,
        string? status,
        int take)
    {
        var builder = Builders<AIContextPack>.Filter;
        var filter = builder.Eq(item => item.UserId, userId);

        if (!string.IsNullOrWhiteSpace(subject))
        {
            filter &= builder.Eq(item => item.Subject, subject);
        }

        if (!string.IsNullOrWhiteSpace(questionType))
        {
            filter &= builder.Eq(item => item.QuestionType, questionType);
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            filter &= builder.Eq(item => item.TargetDifficulty, difficulty);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Eq(item => item.PackStatus, status);
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            filter &= builder.AnyEq(item => item.TargetTopics, topic);
        }

        return await _collection.Find(filter)
            .SortByDescending(item => item.UpdatedAt)
            .Limit(Math.Max(1, take))
            .ToListAsync();
    }

    public Task UpdateAsync(AIContextPack pack)
        => _collection.ReplaceOneAsync(item => item.Id == pack.Id, pack);
}
