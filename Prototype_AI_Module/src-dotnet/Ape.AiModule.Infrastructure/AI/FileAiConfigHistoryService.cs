using System.Text.Json;
using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileAiConfigHistoryService : IAiConfigHistoryService
{
    private readonly string _historyRoot;

    public FileAiConfigHistoryService(IHostEnvironment environment)
    {
        _historyRoot = Path.Combine(environment.ContentRootPath, "App_Data", "ai-config-history");
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object>>> GetHistoryAsync(
        string artifactType,
        string stableId,
        CancellationToken cancellationToken)
    {
        var folder = Path.Combine(_historyRoot, NormalizeArtifactType(artifactType), SanitizePathSegment(stableId));
        if (!Directory.Exists(folder))
        {
            return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object>>>([]);
        }

        var results = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetCreationTimeUtc)
            .Select(ReadSnapshot)
            .ToList();

        return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object>>>(results);
    }

    private static IReadOnlyDictionary<string, object> ReadSnapshot(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return ConvertObject(document.RootElement);
    }

    private static IReadOnlyDictionary<string, object> ConvertObject(JsonElement element)
    {
        var dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = ConvertValue(property.Value) ?? string.Empty;
        }

        return dictionary;
    }

    private static object? ConvertValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertValue).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };

    private static string NormalizeArtifactType(string artifactType)
        => artifactType.Trim().ToLowerInvariant() switch
        {
            "prompt" or "prompts" => "prompts",
            "rubric" or "rubrics" => "rubrics",
            "groundtruth" or "ground-truth" or "ground_truth" => "groundtruth",
            "policy" or "policies" => "policies",
            _ => artifactType.Trim().ToLowerInvariant()
        };

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Replace(' ', '_');
    }
}
