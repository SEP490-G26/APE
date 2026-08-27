using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ape.AiModule.Domain.Entities.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

internal sealed class AiVersionHistoryStore
{
    private readonly string _historyRoot;

    public AiVersionHistoryStore(IHostEnvironment environment)
    {
        _historyRoot = Path.Combine(environment.ContentRootPath, "App_Data", "ai-config-history");
        Directory.CreateDirectory(_historyRoot);
    }

    public void CapturePromptSnapshot(PromptTemplate template, string? source = null)
    {
        var content = new Dictionary<string, object?>
        {
            ["key"] = template.Key,
            ["version"] = template.Version,
            ["description"] = template.Description,
            ["system_prompt"] = template.SystemPrompt,
            ["user_prompt"] = template.UserPrompt,
            ["is_active"] = template.IsActive,
            ["updated_at"] = template.UpdatedAt
        };

        CaptureSnapshot(new AiConfigSnapshotRequest(
            ArtifactType: "prompts",
            StableId: template.Key,
            Version: template.Version,
            FunctionName: ResolvePromptFunctionName(template.Key),
            SubjectCode: null,
            QuestionType: null,
            Language: "en",
            Description: template.Description,
            IsActive: template.IsActive,
            Source: source ?? "prompt_store",
            Content: content,
            SourceCommit: null,
            CapturedFrom: $"App_Data/ai-prompts.json::{template.Key}",
            ChangeReason: null,
            ChangeSummary: null));
    }

    public void CaptureJsonArtifactSnapshot(
        string artifactType,
        string stableId,
        string version,
        string functionName,
        string? subjectCode,
        string? questionType,
        string? language,
        string? description,
        bool isActive,
        string source,
        IReadOnlyDictionary<string, object> content,
        string? sourceCommit = null,
        string? capturedFrom = null,
        string? changeReason = null,
        string? changeSummary = null)
        => CaptureSnapshot(new AiConfigSnapshotRequest(
            artifactType,
            stableId,
            version,
            functionName,
            subjectCode,
            questionType,
            language,
            description,
            isActive,
            source,
            content.ToDictionary(static pair => pair.Key, static pair => (object?)pair.Value),
            sourceCommit,
            capturedFrom,
            changeReason,
            changeSummary));

    public IReadOnlyList<IReadOnlyDictionary<string, object>> GetPromptHistory(string key)
    {
        var folder = Path.Combine(_historyRoot, "prompts", SanitizePathSegment(key));
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetCreationTimeUtc)
            .Select(ReadSnapshotEnvelope)
            .ToList();
    }

    private void CaptureSnapshot(AiConfigSnapshotRequest request)
    {
        var stableFolder = Path.Combine(_historyRoot, request.ArtifactType, SanitizePathSegment(request.StableId));
        Directory.CreateDirectory(stableFolder);

        var json = JsonSerializer.Serialize(request.Content, JsonOptions);
        var hash = ComputeSha256(json);
        var existing = Directory.GetFiles(stableFolder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(ReadSnapshotEnvelope)
            .FirstOrDefault(item =>
                string.Equals(item["content_hash"]?.ToString(), hash, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item["version"]?.ToString(), request.Version, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return;
        }

        var capturedAt = DateTimeOffset.UtcNow;
        var snapshotId = $"{request.StableId}:{request.Version}:{capturedAt:yyyyMMddHHmmssfff}:{hash[..12]}";
        var fileName = $"{capturedAt:yyyyMMddHHmmssfff}-{SanitizePathSegment(request.Version)}-{hash[..12]}.json";
        var payload = new Dictionary<string, object?>
        {
            ["snapshot_id"] = snapshotId,
            ["artifact_type"] = request.ArtifactType,
            ["stable_id"] = request.StableId,
            ["version"] = request.Version,
            ["function_name"] = request.FunctionName,
            ["subject_code"] = request.SubjectCode,
            ["question_type"] = request.QuestionType,
            ["language"] = request.Language,
            ["description"] = request.Description,
            ["is_active"] = request.IsActive,
            ["source"] = request.Source,
            ["source_commit"] = request.SourceCommit,
            ["captured_from"] = request.CapturedFrom,
            ["captured_at"] = capturedAt,
            ["content_hash"] = hash,
            ["change_reason"] = request.ChangeReason,
            ["change_summary"] = request.ChangeSummary,
            ["content"] = request.Content
        };

        File.WriteAllText(Path.Combine(stableFolder, fileName), JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8);
    }

    private static IReadOnlyDictionary<string, object> ReadSnapshotEnvelope(string path)
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

    private static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

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

    private static string ResolvePromptFunctionName(string key)
        => key.Trim().ToLowerInvariant() switch
        {
            "gatekeeper" => "Gatekeeper",
            "extract_content_vision" => "ExtractedContent",
            "auto_tagging" => "EmbeddingTagging",
            "question_generation" => "QuestionGeneration",
            "question_generation_repair" => "QuestionGenerationRepair",
            "question_review" => "QuestionReview",
            "code_mentor" => "CodeMentor",
            _ => "Unknown"
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private sealed record AiConfigSnapshotRequest(
        string ArtifactType,
        string StableId,
        string Version,
        string FunctionName,
        string? SubjectCode,
        string? QuestionType,
        string? Language,
        string? Description,
        bool IsActive,
        string Source,
        IReadOnlyDictionary<string, object?> Content,
        string? SourceCommit,
        string? CapturedFrom,
        string? ChangeReason,
        string? ChangeSummary);
}
