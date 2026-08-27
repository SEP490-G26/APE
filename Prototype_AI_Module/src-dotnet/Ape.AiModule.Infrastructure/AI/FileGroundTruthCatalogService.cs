using System.Collections.Concurrent;
using System.Text.Json;
using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileGroundTruthCatalogService : IGroundTruthCatalogService
{
    private readonly string _datasetsFolder;
    private readonly AiVersionHistoryStore _historyStore;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileGroundTruthCatalogService(IHostEnvironment environment)
    {
        _datasetsFolder = Path.Combine(environment.ContentRootPath, "App_Data", "ai-groundtruth");
        _historyStore = new AiVersionHistoryStore(environment);
    }

    public async Task<IReadOnlyDictionary<string, object>> GetQuestionGenerationGroundTruthAsync(
        string subject,
        string questionType,
        CancellationToken cancellationToken)
    {
        var datasetId = ResolveDatasetId(subject, questionType);
        if (_cache.TryGetValue(datasetId, out var cached))
        {
            return cached;
        }

        var filePath = Path.Combine(_datasetsFolder, $"{datasetId}.json");
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"Ground truth dataset '{datasetId}.json' was not found.");
        }

        var payload = await File.ReadAllTextAsync(filePath, cancellationToken);
        using var document = JsonDocument.Parse(payload);
        var result = ConvertObject(document.RootElement);
        CaptureGroundTruthSnapshot(result, filePath);
        _cache[datasetId] = result;
        return result;
    }

    public async Task<IReadOnlyDictionary<string, object>> GetGatekeeperGroundTruthAsync(CancellationToken cancellationToken)
        => await GetStaticGroundTruthAsync("GATEKEEPER_CORE", cancellationToken);

    public async Task<IReadOnlyDictionary<string, object>> GetExtractedContentGroundTruthAsync(CancellationToken cancellationToken)
        => await GetStaticGroundTruthAsync("EXTRACTED_CONTENT_CORE", cancellationToken);

    public async Task<IReadOnlyDictionary<string, object>> GetEmbeddingTaggingGroundTruthAsync(CancellationToken cancellationToken)
        => await GetStaticGroundTruthAsync("EMBEDDING_TAGGING_CORE", cancellationToken);

    public async Task<IReadOnlyDictionary<string, object>> GetCodeMentorGroundTruthAsync(CancellationToken cancellationToken)
        => await GetStaticGroundTruthAsync("CODE_MENTOR_CORE", cancellationToken);

    private async Task<IReadOnlyDictionary<string, object>> GetStaticGroundTruthAsync(string datasetId, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(datasetId, out var cached))
        {
            return cached;
        }

        var filePath = Path.Combine(_datasetsFolder, $"{datasetId}.json");
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"Ground truth dataset '{datasetId}.json' was not found.");
        }

        var payload = await File.ReadAllTextAsync(filePath, cancellationToken);
        using var document = JsonDocument.Parse(payload);
        var result = ConvertObject(document.RootElement);
        CaptureGroundTruthSnapshot(result, filePath);
        _cache[datasetId] = result;
        return result;
    }

    private void CaptureGroundTruthSnapshot(IReadOnlyDictionary<string, object> dataset, string filePath)
    {
        var datasetId = dataset.TryGetValue("dataset_id", out var datasetIdValue) ? datasetIdValue?.ToString() ?? Path.GetFileNameWithoutExtension(filePath) : Path.GetFileNameWithoutExtension(filePath);
        var version = dataset.TryGetValue("version", out var versionValue) ? versionValue?.ToString() ?? "v1" : "v1";
        var functionName = dataset.TryGetValue("function_name", out var functionValue) ? functionValue?.ToString() ?? ResolveFunctionName(datasetId) : ResolveFunctionName(datasetId);
        var subjectCode = dataset.TryGetValue("subject_code", out var subjectValue) ? subjectValue?.ToString() : null;
        var questionType = dataset.TryGetValue("question_type", out var typeValue) ? typeValue?.ToString() : null;
        var language = dataset.TryGetValue("language", out var languageValue) ? languageValue?.ToString() : null;

        _historyStore.CaptureJsonArtifactSnapshot(
            artifactType: "groundtruth",
            stableId: datasetId,
            version: version,
            functionName: functionName,
            subjectCode: subjectCode,
            questionType: questionType,
            language: language,
            description: datasetId,
            isActive: true,
            source: filePath,
            content: dataset);
    }

    private static string ResolveFunctionName(string datasetId)
    {
        if (datasetId.Contains("GATEKEEPER", StringComparison.OrdinalIgnoreCase)) return "Gatekeeper";
        if (datasetId.Contains("EXTRACTED_CONTENT", StringComparison.OrdinalIgnoreCase)) return "ExtractedContent";
        if (datasetId.Contains("EMBEDDING_TAGGING", StringComparison.OrdinalIgnoreCase)) return "EmbeddingTagging";
        if (datasetId.Contains("CODE_MENTOR", StringComparison.OrdinalIgnoreCase)) return "CodeMentor";
        return "QuestionGenerationReview";
    }

    private static string ResolveDatasetId(string subject, string questionType)
    {
        var subjectKey = NormalizeSubject(subject);
        var typeKey = string.Equals(questionType?.Trim(), "PE", StringComparison.OrdinalIgnoreCase) ? "PE" : "FE";
        return $"QGEN_{subjectKey}_{typeKey}";
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
