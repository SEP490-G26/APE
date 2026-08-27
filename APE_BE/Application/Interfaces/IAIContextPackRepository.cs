using Domain.Entities;

namespace Application.Interfaces;

public interface IAIContextPackRepository
{
    Task<AIContextPack> CreateAsync(AIContextPack pack);
    Task<AIContextPack?> GetByIdAsync(string packId);
    Task<List<AIContextPack>> ListAsync(
        string userId,
        string? subject,
        string? questionType,
        string? difficulty,
        string? topic,
        string? status,
        int take);
    Task UpdateAsync(AIContextPack pack);
}
