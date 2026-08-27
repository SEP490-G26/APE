using System.Text.Json;
using System.Text.Json.Serialization;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileContextPackStore : IContextPackStore
{
    private readonly string _rootFolder;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileContextPackStore(IHostEnvironment environment)
    {
        _rootFolder = Path.Combine(environment.ContentRootPath, "App_Data", "context-packs");
        Directory.CreateDirectory(_rootFolder);
    }

    public async Task<AIContextPack> SaveAsync(AIContextPack pack, CancellationToken cancellationToken)
    {
        var filePath = GetPath(pack.PackId);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(pack, _jsonOptions), cancellationToken);
        return pack;
    }

    public async Task<AIContextPack?> GetAsync(string packId, CancellationToken cancellationToken)
    {
        var filePath = GetPath(packId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<AIContextPack>(stream, _jsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<ContextPackSummaryResult>> ListAsync(
        string? subject,
        string? questionType,
        string? difficulty,
        string? topic,
        string? status,
        int take,
        CancellationToken cancellationToken)
    {
        var results = new List<ContextPackSummaryResult>();
        foreach (var file in Directory.GetFiles(_rootFolder, "*.json", SearchOption.TopDirectoryOnly))
        {
            await using var stream = File.OpenRead(file);
            var pack = await JsonSerializer.DeserializeAsync<AIContextPack>(stream, _jsonOptions, cancellationToken);
            if (pack is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(subject) && !string.Equals(pack.Subject, subject, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(questionType) && !string.Equals(pack.QuestionType, questionType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(difficulty) && !string.Equals(pack.TargetDifficulty, difficulty, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(pack.PackStatus, status, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(topic) && !pack.TargetTopics.Any(item => string.Equals(item, topic, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            results.Add(new ContextPackSummaryResult(
                pack.PackId,
                pack.Subject,
                pack.QuestionType,
                pack.TargetDifficulty,
                pack.TargetTopics,
                pack.PackStrategy,
                pack.TokenCount,
                pack.RecommendedQuestionCount,
                pack.UsageCount,
                pack.PackStatus,
                pack.UpdatedAt));
        }

        return results
            .OrderByDescending(static item => item.UpdatedAt)
            .Take(Math.Max(1, take))
            .ToList();
    }

    public async Task<AIContextPack> MarkStaleAsync(string packId, CancellationToken cancellationToken)
    {
        var pack = await GetAsync(packId, cancellationToken)
            ?? throw new FileNotFoundException($"Context pack '{packId}' was not found.");

        var updated = pack with
        {
            PackStatus = "stale",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return await SaveAsync(updated, cancellationToken);
    }

    public async Task<AIContextPack> TouchUsageAsync(string packId, CancellationToken cancellationToken)
    {
        var pack = await GetAsync(packId, cancellationToken)
            ?? throw new FileNotFoundException($"Context pack '{packId}' was not found.");

        var updated = pack with
        {
            UsageCount = pack.UsageCount + 1,
            LastUsedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return await SaveAsync(updated, cancellationToken);
    }

    private string GetPath(string packId)
        => Path.Combine(_rootFolder, $"{packId}.json");
}
