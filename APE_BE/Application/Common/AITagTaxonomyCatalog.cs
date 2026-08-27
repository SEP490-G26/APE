using System.Text.Json;

namespace Application.Common;

public sealed class AITagTaxonomyCatalog
{
    public static AITagTaxonomyCatalog Empty { get; } = new(new Dictionary<string, AITagTaxonomySubject>(StringComparer.OrdinalIgnoreCase));

    private readonly Dictionary<string, AITagTaxonomySubject> _subjects;

    private AITagTaxonomyCatalog(Dictionary<string, AITagTaxonomySubject> subjects)
    {
        _subjects = subjects;
    }

    public static AITagTaxonomyCatalog FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        using var document = JsonDocument.Parse(json);
        return FromJsonElement(document.RootElement);
    }

    public static AITagTaxonomyCatalog FromJsonElement(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("subjects", out var subjectsElement) ||
            subjectsElement.ValueKind != JsonValueKind.Array)
        {
            return Empty;
        }

        var subjects = new Dictionary<string, AITagTaxonomySubject>(StringComparer.OrdinalIgnoreCase);
        foreach (var subject in subjectsElement.EnumerateArray())
        {
            var subjectCode = ReadString(subject, "subjectCode");
            if (string.IsNullOrWhiteSpace(subjectCode))
            {
                continue;
            }

            var tags = subject.TryGetProperty("tags", out var tagArray) && tagArray.ValueKind == JsonValueKind.Array
                ? tagArray.EnumerateArray().ToList()
                : new List<JsonElement>();

            var canonicalTags = tags
                .Select(tag => ReadString(tag, "canonical")?.Trim().ToLowerInvariant())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();

            var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in tags)
            {
                var canonical = ReadString(tag, "canonical")?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(canonical))
                {
                    continue;
                }

                aliasMap[canonical] = canonical;
                if (tag.TryGetProperty("aliases", out var aliasesElement) && aliasesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var alias in aliasesElement.EnumerateArray().Select(item => item.GetString()))
                    {
                        if (!string.IsNullOrWhiteSpace(alias))
                        {
                            aliasMap[alias.Trim().ToLowerInvariant()] = canonical;
                        }
                    }
                }
            }

            var normalizedSubject = subjectCode.Trim().ToUpperInvariant();
            var entry = new AITagTaxonomySubject(normalizedSubject, canonicalTags, aliasMap);
            RegisterSubject(subjects, normalizedSubject, entry);

            if (subject.TryGetProperty("aliases", out var subjectAliases) && subjectAliases.ValueKind == JsonValueKind.Array)
            {
                foreach (var alias in subjectAliases.EnumerateArray().Select(item => item.GetString()))
                {
                    if (!string.IsNullOrWhiteSpace(alias))
                    {
                        RegisterSubject(subjects, alias.Trim(), entry);
                    }
                }
            }
        }

        return subjects.Count == 0 ? Empty : new AITagTaxonomyCatalog(subjects);
    }

    public AITagTaxonomySubject? ResolveBySubject(string? subjectCode)
    {
        if (string.IsNullOrWhiteSpace(subjectCode))
        {
            return null;
        }

        return _subjects.TryGetValue(subjectCode.Trim(), out var subject)
            ? subject
            : null;
    }

    public string? FindCanonicalTag(string tag, IReadOnlyList<string> allowedTags)
    {
        foreach (var subject in _subjects.Values.Distinct())
        {
            if (subject.CanonicalTags.Count == 0 || !allowedTags.Any(item => subject.CanonicalTags.Contains(item, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (subject.TryResolveCanonical(tag, out var canonical))
            {
                return canonical;
            }
        }

        return null;
    }

    private static void RegisterSubject(Dictionary<string, AITagTaxonomySubject> subjects, string key, AITagTaxonomySubject entry)
    {
        subjects[key] = entry;
        subjects[key.ToUpperInvariant()] = entry;
        subjects[key.ToLowerInvariant()] = entry;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}

public sealed class AITagTaxonomySubject
{
    public string SubjectCode { get; }
    public List<string> CanonicalTags { get; }
    private readonly Dictionary<string, string> _aliasMap;

    public AITagTaxonomySubject(string subjectCode, List<string> canonicalTags, Dictionary<string, string> aliasMap)
    {
        SubjectCode = subjectCode;
        CanonicalTags = canonicalTags;
        _aliasMap = aliasMap;
    }

    public bool TryResolveCanonical(string tag, out string? canonical)
    {
        canonical = null;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return _aliasMap.TryGetValue(tag.Trim().ToLowerInvariant(), out canonical);
    }
}
