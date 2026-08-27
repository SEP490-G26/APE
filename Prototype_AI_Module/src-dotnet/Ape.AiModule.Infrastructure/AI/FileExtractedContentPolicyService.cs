using System.Collections.Concurrent;
using System.Text.Json;
using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileExtractedContentPolicyService : IExtractedContentPolicyService
{
    private readonly string _filePath;
    private readonly AiVersionHistoryStore _historyStore;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileExtractedContentPolicyService(IHostEnvironment environment)
    {
        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "extracted-content-policy.json");
        _historyStore = new AiVersionHistoryStore(environment);
        LoadDefaultsIfNeeded();
    }

    public async Task<IReadOnlyDictionary<string, object>> GetPolicyAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "extracted_content_policy";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var payload = await File.ReadAllTextAsync(_filePath, cancellationToken);
        using var document = JsonDocument.Parse(payload);
        var result = ConvertObject(document.RootElement);
        CapturePolicySnapshot(result);
        _cache[cacheKey] = result;
        return result;
    }

    private void CapturePolicySnapshot(IReadOnlyDictionary<string, object> policy)
    {
        var policyId = policy.TryGetValue("policy_id", out var idValue) ? idValue?.ToString() ?? "extracted_content_policy" : "extracted_content_policy";
        var version = policy.TryGetValue("version", out var versionValue) ? versionValue?.ToString() ?? "v1" : "v1";
        _historyStore.CaptureJsonArtifactSnapshot(
            artifactType: "policies",
            stableId: policyId,
            version: version,
            functionName: "ExtractedContent",
            subjectCode: null,
            questionType: null,
            language: null,
            description: "Extracted content runtime policy",
            isActive: true,
            source: _filePath,
            content: policy);
    }

    private void LoadDefaultsIfNeeded()
    {
        if (File.Exists(_filePath))
        {
            return;
        }

        var payload = """
{
  "policy_id": "extracted_content_policy_v1",
  "version": "v1",
  "function_name": "ExtractedContent",
  "supported_modes": ["TextOnly", "FullMultimodalPage"],
  "image_markers": ["image", "img", "figure"],
  "normalization_rules": {
    "collapse_spaces": true,
    "collapse_blank_lines": true,
    "prepend_markdown_header": true
  },
  "warning_rules": {
    "warn_if_empty_content": true,
    "warn_if_images_present_without_vision_model": true,
    "warn_if_no_text_after_normalization": true
  }
}
""";

        File.WriteAllText(_filePath, payload);
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
