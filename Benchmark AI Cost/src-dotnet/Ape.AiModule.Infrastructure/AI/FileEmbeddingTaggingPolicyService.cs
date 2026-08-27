using System.Collections.Concurrent;
using System.Text.Json;
using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileEmbeddingTaggingPolicyService : IEmbeddingTaggingPolicyService
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileEmbeddingTaggingPolicyService(IHostEnvironment environment)
    {
        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "embedding-tagging-policy.json");
        LoadDefaultsIfNeeded();
    }

    public async Task<IReadOnlyDictionary<string, object>> GetPolicyAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "embedding_tagging_policy";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var payload = await File.ReadAllTextAsync(_filePath, cancellationToken);
        using var document = JsonDocument.Parse(payload);
        var result = ConvertObject(document.RootElement);
        _cache[cacheKey] = result;
        return result;
    }

    private void LoadDefaultsIfNeeded()
    {
        if (File.Exists(_filePath))
        {
            return;
        }

        var payload = """
{
  "policy_id": "embedding_tagging_policy_v1",
  "version": "v1",
  "function_name": "EmbeddingTagging",
  "chunk_defaults": {
    "chunking_strategy_text_only": "logical_block",
    "chunking_strategy_multimodal": "page_block",
    "retrieval_enabled": true,
    "default_status": "active"
  },
  "tagging_rules": {
    "max_tags_per_chunk": 5,
    "fallback_chunk_type": "paragraph",
    "fallback_section_title": "Untitled Section"
  },
  "subject_taxonomy": {
    "C_BASIC": ["variables", "conditions", "loops", "arrays", "functions", "pointers", "stdio"],
    "JAVA_OOP": ["class", "object", "constructor", "encapsulation", "inheritance", "polymorphism", "interface", "method"],
    "DSA_OOP": ["array", "linked_list", "stack", "queue", "tree", "graph", "sorting", "searching", "complexity"]
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
