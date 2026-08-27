using System.Collections.Concurrent;
using System.Text.Json;
using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileGatekeeperPolicyService : IGatekeeperPolicyService
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileGatekeeperPolicyService(IHostEnvironment environment)
    {
        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "gatekeeper-policy.json");
        LoadDefaultsIfNeeded();
    }

    public async Task<IReadOnlyDictionary<string, object>> GetPolicyAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "gatekeeper_policy";
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
  "policy_id": "gatekeeper_policy_v1",
  "version": "v1",
  "function_name": "Gatekeeper",
  "supported_subjects": [
    {
      "code": "C",
      "label": "Programming Fundamentals using C",
      "topic_examples": ["variables", "conditions", "loops", "arrays", "functions", "pointers", "scanf", "printf"]
    },
    {
      "code": "JAVA_OOP",
      "label": "Java OOP",
      "topic_examples": ["class", "object", "constructor", "encapsulation", "inheritance", "polymorphism", "method", "interface"]
    },
    {
      "code": "DSA_JAVA",
      "label": "Data Structures and Algorithms in Java",
      "topic_examples": ["array", "linked list", "stack", "queue", "tree", "graph", "sorting", "searching", "complexity"]
    }
  ],
  "verdict_rules": {
    "supported": "The document clearly belongs to exactly one supported subject area.",
    "unsupported": "The document is outside the supported subject whitelist.",
    "ambiguous": "The document is too short, too noisy, or mixes multiple supported subject areas without a clear primary subject."
  },
  "precheck_rules": {
    "min_word_count": 12,
    "empty_content_rejection_code": "empty_content",
    "insufficient_content_rejection_code": "insufficient_content"
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
