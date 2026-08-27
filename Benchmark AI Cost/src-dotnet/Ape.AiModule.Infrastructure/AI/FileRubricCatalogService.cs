using System.Collections.Concurrent;
using System.Text.Json;
using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileRubricCatalogService : IRubricCatalogService
{
    private readonly string _rubricsFolder;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileRubricCatalogService(IHostEnvironment environment)
    {
        _rubricsFolder = Path.Combine(environment.ContentRootPath, "App_Data", "ai-rubrics");
    }

    public async Task<IReadOnlyDictionary<string, object>> GetQuestionReviewRubricAsync(
        string subject,
        string questionType,
        CancellationToken cancellationToken)
    {
        var rubricId = ResolveRubricId(subject, questionType);
        if (_cache.TryGetValue(rubricId, out var cached))
        {
            return cached;
        }

        var filePath = Path.Combine(_rubricsFolder, $"{rubricId}.json");
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"Rubric file '{rubricId}.json' was not found.");
        }

        var payload = await File.ReadAllTextAsync(filePath, cancellationToken);
        using var document = JsonDocument.Parse(payload);
        var result = ConvertObject(document.RootElement);
        _cache[rubricId] = result;
        return result;
    }

    public async Task<IReadOnlyDictionary<string, object>> GetGatekeeperRubricAsync(CancellationToken cancellationToken)
        => await GetStaticRubricAsync("GATEKEEPER", cancellationToken);

    public async Task<IReadOnlyDictionary<string, object>> GetExtractedContentRubricAsync(CancellationToken cancellationToken)
        => await GetStaticRubricAsync("EXTRACTED_CONTENT", cancellationToken);

    public async Task<IReadOnlyDictionary<string, object>> GetEmbeddingTaggingRubricAsync(CancellationToken cancellationToken)
        => await GetStaticRubricAsync("EMBEDDING_TAGGING", cancellationToken);

    public async Task<IReadOnlyDictionary<string, object>> GetCodeMentorRubricAsync(CancellationToken cancellationToken)
        => await GetStaticRubricAsync("CODE_MENTOR", cancellationToken);

    private async Task<IReadOnlyDictionary<string, object>> GetStaticRubricAsync(string rubricId, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(rubricId, out var cached))
        {
            return cached;
        }

        var filePath = Path.Combine(_rubricsFolder, $"{rubricId}.json");
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"Rubric file '{rubricId}.json' was not found.");
        }

        var payload = await File.ReadAllTextAsync(filePath, cancellationToken);
        using var document = JsonDocument.Parse(payload);
        var result = ConvertObject(document.RootElement);
        _cache[rubricId] = result;
        return result;
    }

    private static string ResolveRubricId(string subject, string questionType)
    {
        var subjectKey = NormalizeSubject(subject);
        var typeKey = string.Equals(questionType?.Trim(), "PE", StringComparison.OrdinalIgnoreCase) ? "PE" : "FE";
        return $"{subjectKey}_{typeKey}";
    }

    private static string NormalizeSubject(string subject)
    {
        var lowered = subject.Trim().ToLowerInvariant();
        if (lowered.Contains("java") && lowered.Contains("oop"))
        {
            return "JAVA_OOP";
        }

        if (lowered.Contains("dsa") || lowered.Contains("data structure") || lowered.Contains("algorithm"))
        {
            return "DSA_JAVA";
        }

        return "C";
    }

    private static IReadOnlyDictionary<string, object> ConvertObject(JsonElement element)
    {
        var dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = ConvertValue(property.Value);
        }

        return dictionary;
    }

    private static object ConvertValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertValue).ToList(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => string.Empty,
            _ => element.ToString()
        };
    }
}
