using System.Collections.Concurrent;
using System.Text.Json;
using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class FileCodeMentorPolicyService : ICodeMentorPolicyService
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, object>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileCodeMentorPolicyService(IHostEnvironment environment)
    {
        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "code-mentor-policy.json");
        LoadDefaultsIfNeeded();
    }

    public async Task<IReadOnlyDictionary<string, object>> GetPolicyAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "code_mentor_policy";
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
  "policy_id": "code_mentor_policy_v1",
  "version": "v1",
  "function_name": "CodeMentor",
  "supported_languages": ["c", "java"],
  "issue_categories": ["syntax", "logic", "edge_case", "runtime_risk", "complexity", "style", "partial_solution", "algorithm_choice"],
  "verdicts": ["correct", "acceptable_with_minor_notes", "needs_fix", "incorrect"],
  "feedback_rules": {
    "max_issue_count": 6,
    "max_suggestion_count": 6,
    "require_actionable_suggestions": true,
    "require_failing_scenarios_when_possible": true
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
