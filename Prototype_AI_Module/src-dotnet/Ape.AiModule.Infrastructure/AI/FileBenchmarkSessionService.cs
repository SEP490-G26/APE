using System.Text.Json;
using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileBenchmarkSessionService : IBenchmarkSessionService
{
    private readonly string _rootFolder;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public FileBenchmarkSessionService(IHostEnvironment environment)
    {
        _rootFolder = Path.Combine(environment.ContentRootPath, "App_Data", "benchmark-sessions");
        Directory.CreateDirectory(_rootFolder);
    }

    public async Task<BenchmarkSessionRecord> SaveAsync(
        BenchmarkSessionSaveRequest request,
        CancellationToken cancellationToken)
    {
        var sessionId = $"bench-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..Math.Min(64, $"bench-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}".Length)];
        var fileName = $"{sessionId}.json";
        var summaryCount = CountItems(request.SummaryResults);
        var batchCount = CountItems(request.BatchResults);
        var matrixCount = CountItems(request.MatrixResults);

        var summary = new BenchmarkSessionSummary(
            sessionId,
            string.IsNullOrWhiteSpace(request.Name) ? sessionId : request.Name.Trim(),
            DateTimeOffset.UtcNow,
            fileName,
            summaryCount,
            batchCount,
            matrixCount,
            request.Notes);

        var record = new BenchmarkSessionRecord(
            summary,
            request.SummaryResults,
            request.BatchResults,
            request.MatrixResults);

        var filePath = Path.Combine(_rootFolder, fileName);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(record, _jsonOptions), cancellationToken);
        return record;
    }

    public async Task<IReadOnlyList<BenchmarkSessionSummary>> ListAsync(
        int take,
        CancellationToken cancellationToken)
    {
        var items = new List<BenchmarkSessionSummary>();
        if (!Directory.Exists(_rootFolder))
        {
            return items;
        }

        foreach (var file in Directory.GetFiles(_rootFolder, "*.json", SearchOption.TopDirectoryOnly))
        {
            await using var stream = File.OpenRead(file);
            var record = await JsonSerializer.DeserializeAsync<BenchmarkSessionRecord>(stream, _jsonOptions, cancellationToken);
            if (record is not null)
            {
                items.Add(record.Summary);
            }
        }

        return items
            .OrderByDescending(static item => item.CreatedAt)
            .Take(Math.Max(1, take))
            .ToList();
    }

    public async Task<BenchmarkSessionRecord> GetAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(_rootFolder, $"{sessionId}.json");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Benchmark session '{sessionId}' was not found.");
        }

        await using var stream = File.OpenRead(filePath);
        var record = await JsonSerializer.DeserializeAsync<BenchmarkSessionRecord>(stream, _jsonOptions, cancellationToken);
        if (record is null)
        {
            throw new InvalidOperationException($"Benchmark session '{sessionId}' could not be deserialized.");
        }

        return record;
    }

    private static int CountItems(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            return jsonElement.GetArrayLength();
        }

        if (value is System.Collections.ICollection collection)
        {
            return collection.Count;
        }

        return 0;
    }
}
