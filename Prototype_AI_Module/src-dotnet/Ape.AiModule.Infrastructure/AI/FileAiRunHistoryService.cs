using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Ape.AiModule.Domain.Entities.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileAiRunHistoryService : IAiRunHistoryService
{
    private readonly string _rootFolder;
    private readonly string _appDataFolder;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileAiRunHistoryService(IHostEnvironment environment)
    {
        _rootFolder = Path.Combine(environment.ContentRootPath, "App_Data", "run-history");
        _appDataFolder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(_rootFolder);
    }

    public async Task<AiRunHistoryRecord> SaveAsync(
        string functionName,
        string routeKey,
        object request,
        object response,
        CancellationToken cancellationToken)
    {
        var normalizedFunction = Normalize(functionName);
        var folder = Path.Combine(_rootFolder, normalizedFunction);
        Directory.CreateDirectory(folder);

        var runId = $"{normalizedFunction}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..Math.Min(60, $"{normalizedFunction}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}".Length)];
        var fileName = $"{runId}.json";
        var summary = new AiRunHistorySummary(
            runId,
            normalizedFunction,
            routeKey,
            DateTimeOffset.UtcNow,
            request.GetType().Name,
            response.GetType().Name,
            ExtractStatus(response),
            ExtractModelSummary(request, response),
            ExtractModelFields(request, response),
            ResolveConfigReferences(normalizedFunction, routeKey, request),
            ExtractUsageSource(response),
            ExtractError(response),
            ExtractTotals(response),
            fileName);

        var record = new AiRunHistoryRecord(summary, request, response);
        var filePath = Path.Combine(folder, fileName);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(record, _jsonOptions), cancellationToken);
        return record;
    }

    public async Task<IReadOnlyList<AiRunHistorySummary>> ListAsync(
        string? functionName,
        int take,
        CancellationToken cancellationToken)
    {
        var folders = string.IsNullOrWhiteSpace(functionName)
            ? Directory.Exists(_rootFolder) ? Directory.GetDirectories(_rootFolder) : []
            : [Path.Combine(_rootFolder, Normalize(functionName))];

        var items = new List<AiRunHistorySummary>();
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
            {
                await using var stream = File.OpenRead(file);
                var record = await JsonSerializer.DeserializeAsync<AiRunHistoryRecord>(stream, _jsonOptions, cancellationToken);
                if (record is not null)
                {
                    items.Add(record.Summary);
                }
            }
        }

        return items
            .OrderByDescending(static item => item.RecordedAt)
            .Take(Math.Max(1, take))
            .ToList();
    }

    public async Task<AiRunHistoryRecord> GetAsync(
        string functionName,
        string runId,
        CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(_rootFolder, Normalize(functionName), $"{runId}.json");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Run history '{runId}' was not found for function '{functionName}'.");
        }

        await using var stream = File.OpenRead(filePath);
        var record = await JsonSerializer.DeserializeAsync<AiRunHistoryRecord>(stream, _jsonOptions, cancellationToken);
        if (record is null)
        {
            throw new InvalidOperationException($"Run history '{runId}' could not be deserialized.");
        }

        return record;
    }

    private static string Normalize(string value)
        => value.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');

    private static string? ExtractStatus(object response)
    {
        if (TryReadStringProperty(response, "Verdict", out var verdict))
        {
            return verdict;
        }

        if (TryReadNestedStringProperty(response, "Verdict", "Verdict", out var nestedVerdict))
        {
            return nestedVerdict;
        }

        if (TryReadStringProperty(response, "ReviewStatus", out var reviewStatus))
        {
            return reviewStatus;
        }

        if (TryReadNestedStringProperty(response, "Review", "ReviewStatus", out var nestedReviewStatus))
        {
            return nestedReviewStatus;
        }

        if (TryReadNestedStringProperty(response, "Feedback", "Verdict", out var mentorVerdict))
        {
            return mentorVerdict;
        }

        return null;
    }

    private static string? ExtractModelSummary(object request, object response)
    {
        var requestModelParts = request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static property => property.PropertyType == typeof(string) && property.Name.Contains("Model", StringComparison.OrdinalIgnoreCase))
            .Select(property => new
            {
                property.Name,
                Value = property.GetValue(request)?.ToString()
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(static item => $"{item.Name}={item.Value}")
            .ToList();

        if (requestModelParts.Count > 0)
        {
            return string.Join("; ", requestModelParts);
        }

        if (TryReadNestedStringProperty(response, "Verdict", "ModelName", out var verdictModel))
        {
            return verdictModel;
        }

        if (TryReadNestedStringProperty(response, "Feedback", "ModelName", out var feedbackModel))
        {
            return feedbackModel;
        }

        return null;
    }

    private static NormalizedModelFields? ExtractModelFields(object request, object response)
    {
        if (TryReadPropertyValue<TokenCostBreakdown>(response, "Totals", out var totals) && totals?.UsageCapture is not null)
        {
            var usageProvider = InferProviderFromResponse(response) ?? InferProviderFromRequest(request);
            return BuildModelFieldsFromRequest(request, usageProvider);
        }

        var provider = InferProviderFromResponse(response) ?? InferProviderFromRequest(request);
        var modelFields = BuildModelFieldsFromRequest(request, provider);
        return modelFields;
    }

    private static string? ExtractUsageSource(object response)
    {
        if (TryReadPropertyValue<TokenCostBreakdown>(response, "Totals", out var totals))
        {
            return totals?.UsageCapture?.UsageSource;
        }

        return null;
    }

    private static NormalizedError? ExtractError(object response)
    {
        if (TryReadPropertyValue<TokenCostBreakdown>(response, "Totals", out var totals) && totals?.Error is not null)
        {
            return totals.Error;
        }

        return null;
    }

    private static TokenCostBreakdown? ExtractTotals(object response)
    {
        var property = response.GetType().GetProperty("Totals", BindingFlags.Public | BindingFlags.Instance);
        return property?.GetValue(response) as TokenCostBreakdown;
    }

    private static bool TryReadPropertyValue<T>(object source, string propertyName, out T? value)
        where T : class
    {
        var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        value = property?.GetValue(source) as T;
        return value is not null;
    }

    private static bool TryReadStringProperty(object source, string propertyName, out string value)
    {
        var property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.PropertyType == typeof(string) && property.GetValue(source) is string text && !string.IsNullOrWhiteSpace(text))
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadNestedStringProperty(object source, string parentPropertyName, string childPropertyName, out string value)
    {
        var parent = source.GetType().GetProperty(parentPropertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(source);
        if (parent is not null && TryReadStringProperty(parent, childPropertyName, out value))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? InferProviderFromRequest(object request)
    {
        var modelProperties = request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static property => property.PropertyType == typeof(string) && property.Name.Contains("Model", StringComparison.OrdinalIgnoreCase))
            .Select(property => property.GetValue(request)?.ToString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return modelProperties.Count > 0 ? InferProvider(modelProperties[0]!) : null;
    }

    private static string? InferProviderFromResponse(object response)
    {
        if (TryReadNestedStringProperty(response, "Verdict", "ModelName", out var verdictModel))
        {
            return InferProvider(verdictModel);
        }

        if (TryReadNestedStringProperty(response, "Feedback", "ModelName", out var feedbackModel))
        {
            return InferProvider(feedbackModel);
        }

        return null;
    }

    private static NormalizedModelFields BuildModelFieldsFromRequest(object request, string? provider)
    {
        string? Read(string propertyName) => request.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(request)?.ToString();

        return new NormalizedModelFields(
            provider,
            Read("Model"),
            Read("GeneratorModel"),
            Read("ReviewerModel"),
            Read("EmbeddingModel"),
            Read("TaggingModel"),
            Read("VisionModel"),
            Read("MentorModel"));
    }

    private static string InferProvider(string model)
    {
        var lowered = model.ToLowerInvariant();
        return lowered switch
        {
            var value when value.Contains("gpt") || value.Contains("text-embedding") => "openai",
            var value when value.Contains("gemini") => "gemini",
            var value when value.Contains("cohere") || value.Contains("embed") || value.Contains("command") => "cohere",
            _ => "custom"
        };
    }

    private IReadOnlyList<AiConfigReference> ResolveConfigReferences(string functionName, string routeKey, object request)
    {
        var refs = new List<AiConfigReference>();
        var normalizedRoute = Normalize(routeKey);

        void AddPromptRef(string promptKey)
        {
            var prompt = ReadCurrentJsonFile(Path.Combine(_appDataFolder, "ai-prompts.json"), promptKey, "Key");
            if (prompt is null) return;
            var version = ReadString(prompt, "Version") ?? "unknown";
            var snapshot = ReadSnapshotReference("prompts", promptKey, version);
            var snapshotHash = snapshot is null ? null : ReadString(snapshot, "content_hash");
            var snapshotId = snapshot is null ? null : ReadString(snapshot, "snapshot_id");
            refs.Add(new AiConfigReference(
                "prompt",
                promptKey,
                version,
                ResolveFunctionNameFromPromptKey(promptKey),
                null,
                null,
                "en",
                snapshotHash ?? ComputeContentHash(prompt),
                snapshotId is { Length: > 0 }
                    ? Path.Combine("App_Data", "ai-config-history", "prompts", promptKey, $"{snapshotId}.json")
                    : Path.Combine("App_Data", "ai-prompts.json")));
        }

        void AddRubricRef(string rubricId)
        {
            var path = Path.Combine(_appDataFolder, "ai-rubrics", $"{rubricId}.json");
            var rubric = ReadCurrentJsonFile(path);
            if (rubric is null) return;
            var version = ReadString(rubric, "version") ?? "unknown";
            var snapshot = ReadSnapshotReference("rubrics", ReadString(rubric, "rubric_id") ?? rubricId, version);
            var snapshotHash = snapshot is null ? null : ReadString(snapshot, "content_hash");
            var snapshotId = snapshot is null ? null : ReadString(snapshot, "snapshot_id");
            refs.Add(new AiConfigReference(
                "rubric",
                ReadString(rubric, "rubric_id") ?? rubricId,
                version,
                ReadString(rubric, "function_name"),
                ReadString(rubric, "subject_code"),
                ReadString(rubric, "question_type"),
                ReadString(rubric, "language"),
                snapshotHash ?? ComputeContentHash(rubric),
                snapshotId is { Length: > 0 }
                    ? Path.Combine("App_Data", "ai-config-history", "rubrics", ReadString(rubric, "rubric_id") ?? rubricId, $"{snapshotId}.json")
                    : path));
        }

        void AddGroundTruthRef(string datasetId)
        {
            var path = Path.Combine(_appDataFolder, "ai-groundtruth", $"{datasetId}.json");
            var dataset = ReadCurrentJsonFile(path);
            if (dataset is null) return;
            var stableId = ReadString(dataset, "dataset_id") ?? datasetId;
            var version = ReadString(dataset, "version") ?? "unknown";
            var snapshot = ReadSnapshotReference("groundtruth", stableId, version);
            var snapshotHash = snapshot is null ? null : ReadString(snapshot, "content_hash");
            var snapshotId = snapshot is null ? null : ReadString(snapshot, "snapshot_id");
            refs.Add(new AiConfigReference(
                "groundtruth",
                stableId,
                version,
                ReadString(dataset, "function_name"),
                ReadString(dataset, "subject_code"),
                ReadString(dataset, "question_type"),
                ReadString(dataset, "language"),
                snapshotHash ?? ComputeContentHash(dataset),
                snapshotId is { Length: > 0 }
                    ? Path.Combine("App_Data", "ai-config-history", "groundtruth", stableId, $"{snapshotId}.json")
                    : path));
        }

        void AddPolicyRef(string fileName)
        {
            var path = Path.Combine(_appDataFolder, fileName);
            var policy = ReadCurrentJsonFile(path);
            if (policy is null) return;
            var stableId = ReadString(policy, "policy_id") ?? Path.GetFileNameWithoutExtension(fileName);
            var version = ReadString(policy, "version") ?? "unknown";
            var snapshot = ReadSnapshotReference("policies", stableId, version);
            var snapshotHash = snapshot is null ? null : ReadString(snapshot, "content_hash");
            var snapshotId = snapshot is null ? null : ReadString(snapshot, "snapshot_id");
            refs.Add(new AiConfigReference(
                "policy",
                stableId,
                version,
                ReadString(policy, "function_name"),
                null,
                null,
                null,
                snapshotHash ?? ComputeContentHash(policy),
                snapshotId is { Length: > 0 }
                    ? Path.Combine("App_Data", "ai-config-history", "policies", stableId, $"{snapshotId}.json")
                    : path));
        }

        if (normalizedRoute.Contains("gatekeeper", StringComparison.Ordinal))
        {
            AddPromptRef("gatekeeper");
            AddPolicyRef("gatekeeper-policy.json");
            AddRubricRef("GATEKEEPER");
            AddGroundTruthRef("GATEKEEPER_CORE");
        }

        if (normalizedRoute.Contains("extracted-content", StringComparison.Ordinal))
        {
            AddPromptRef("extract_content_vision");
            AddPolicyRef("extracted-content-policy.json");
            AddRubricRef("EXTRACTED_CONTENT");
            AddGroundTruthRef("EXTRACTED_CONTENT_CORE");
        }

        if (normalizedRoute.Contains("embedding-tagging", StringComparison.Ordinal))
        {
            AddPromptRef("auto_tagging");
            AddPolicyRef("embedding-tagging-policy.json");
            AddRubricRef("EMBEDDING_TAGGING");
            AddGroundTruthRef("EMBEDDING_TAGGING_CORE");
        }

        if (normalizedRoute.Contains("question-generation", StringComparison.Ordinal)
            || normalizedRoute.Contains("question-review", StringComparison.Ordinal)
            || normalizedRoute.Contains("generation-review", StringComparison.Ordinal))
        {
            var subject = ReadStringFromRequest(request, "Subject") ?? "C";
            var questionType = ReadStringFromRequest(request, "QuestionType") ?? "FE";
            var rubricId = ResolveQuestionRubricId(subject, questionType);
            var datasetId = ResolveQuestionDatasetId(subject, questionType);

            if (normalizedRoute.Contains("question-review", StringComparison.Ordinal)
                || normalizedRoute.Contains("generation-review", StringComparison.Ordinal))
            {
                AddPromptRef("question_review");
            }

            if (normalizedRoute.Contains("question-generation", StringComparison.Ordinal)
                || normalizedRoute.Contains("generation-review", StringComparison.Ordinal))
            {
                AddPromptRef("question_generation");
            }

            AddRubricRef(rubricId);
            AddGroundTruthRef(datasetId);
        }

        if (normalizedRoute.Contains("code-mentor", StringComparison.Ordinal))
        {
            AddPromptRef("code_mentor");
            AddPolicyRef("code-mentor-policy.json");
            AddRubricRef("CODE_MENTOR");
            AddGroundTruthRef("CODE_MENTOR_CORE");
        }

        return refs
            .GroupBy(static item => $"{item.ArtifactType}:{item.StableId}:{item.Version}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();
    }

    private static IReadOnlyDictionary<string, object>? ReadCurrentJsonFile(string path, string? matchValue = null, string? matchField = null)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var candidate = ConvertObject(item);
                if (string.IsNullOrWhiteSpace(matchValue) || string.Equals(ReadString(candidate, matchField ?? "key"), matchValue, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        return ConvertObject(document.RootElement);
    }

    private IReadOnlyDictionary<string, object>? ReadSnapshotReference(string artifactType, string stableId, string version)
    {
        var folder = Path.Combine(_appDataFolder, "ai-config-history", NormalizeArtifactType(artifactType), SanitizePathSegment(stableId));
        if (!Directory.Exists(folder))
        {
            return null;
        }

        return Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => ReadCurrentJsonFile(path))
            .Where(static snapshot => snapshot is not null)
            .Cast<IReadOnlyDictionary<string, object>>()
            .Where(snapshot => string.Equals(ReadString(snapshot, "version"), version, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(snapshot => ReadString(snapshot, "captured_at"))
            .FirstOrDefault();
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

    private static string ComputeContentHash(IReadOnlyDictionary<string, object> source)
    {
        var json = JsonSerializer.Serialize(source.OrderBy(static pair => pair.Key).ToDictionary(static pair => pair.Key, static pair => pair.Value));
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? ReadString(IReadOnlyDictionary<string, object> source, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return source.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static string? ReadStringFromRequest(object request, string propertyName)
        => request.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(request)?.ToString();

    private static string ResolveQuestionRubricId(string subject, string questionType)
    {
        var subjectKey = NormalizeSubject(subject);
        var typeKey = string.Equals(questionType?.Trim(), "PE", StringComparison.OrdinalIgnoreCase) ? "PE" : "FE";
        return $"{subjectKey}_{typeKey}";
    }

    private static string ResolveQuestionDatasetId(string subject, string questionType)
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
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return builder.ToString().Replace(' ', '_');
    }

    private static string ResolveFunctionNameFromPromptKey(string promptKey)
        => promptKey.Trim().ToLowerInvariant() switch
        {
            "gatekeeper" => "Gatekeeper",
            "extract_content_vision" => "ExtractedContent",
            "auto_tagging" => "EmbeddingTagging",
            "question_generation" => "QuestionGeneration",
            "question_review" => "QuestionReview",
            "code_mentor" => "CodeMentor",
            _ => "Unknown"
        };
}
