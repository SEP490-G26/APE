using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Data;

public static class AIDbReferenceSeeder
{
    public static async Task<DbReferenceSeedSummary> SeedAsync(
        DbContext db,
        string root,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"DB reference seed root was not found: '{root}'.");
        }

        var summary = new DbReferenceSeedSummary();

        summary.RuleArtifacts = await SeedRuleArtifactsFromSnapshotAsync(db, root, cancellationToken);
        summary.Agents = await SeedAgentsAsync(db, root, cancellationToken);

        return summary;
    }

    private static async Task<int> SeedRuleArtifactsFromSnapshotAsync(DbContext db, string root, CancellationToken cancellationToken)
    {
        var taxonomySnapshotLookup = await LoadTaxonomySnapshotLookupAsync(root, cancellationToken);
        var count = 0;
        count += await SeedPromptSnapshotAsync(db, root, cancellationToken);
        count += await SeedTaxonomySnapshotAsync(db, root, cancellationToken);
        count += await SeedHistorySnapshotAsync(db, root, taxonomySnapshotLookup, cancellationToken);
        return count;
    }

    private static async Task<int> SeedPromptSnapshotAsync(DbContext db, string root, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, "APE_DB_Test.AI_Prompt_Versions.json");
        if (!File.Exists(path))
        {
            return 0;
        }

        var items = await LoadArrayAsync(path, cancellationToken);
        var count = 0;
        foreach (var item in items)
        {
            var promptKey = ReadString(item, "PromptKey");
            var version = ReadString(item, "Version");
            if (string.IsNullOrWhiteSpace(promptKey) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            var updatedAt = ReadDateTime(item, "UpdatedAt") ?? DateTime.UtcNow;
            var promptContentJson = JsonSerializer.Serialize(new
            {
                key = promptKey,
                version,
                description = ReadString(item, "Description"),
                systemPrompt = ReadString(item, "SystemPrompt") ?? string.Empty,
                userPrompt = ReadString(item, "UserPrompt") ?? string.Empty,
                variables = ReadStringList(item, "Variables"),
                isActive = ReadBool(item, "IsActive") ?? true
            });

            var artifact = new AIRuleArtifact
            {
                ArtifactType = "Prompt",
                ArtifactKey = promptKey,
                Version = version,
                IsActive = ReadBool(item, "IsActive") ?? true,
                ContentJson = promptContentJson,
                Description = ReadString(item, "Description"),
                Summary = "Prompt template",
                Source = "db-reference",
                SourcePath = Path.GetFileName(path),
                ContentHash = AIRuleArtifactStore.ComputeContentHash(promptContentJson),
                CreatedAt = ReadDateTime(item, "CreatedAt") ?? updatedAt,
                UpdatedAt = updatedAt,
                ActivatedAt = (ReadBool(item, "IsActive") ?? true) ? updatedAt : null
            };

            if (artifact.IsActive)
            {
                await AIRuleArtifactStore.ActivateAsync(db, artifact, cancellationToken);
            }
            else
            {
                await AIRuleArtifactStore.UpsertAsync(db, artifact, cancellationToken);
            }

            count++;
        }

        return count;
    }

    private static async Task<int> SeedTaxonomySnapshotAsync(DbContext db, string root, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, "APE_DB_Test.AI_Tag_Taxonomies.json");
        if (!File.Exists(path))
        {
            return 0;
        }

        var items = await LoadArrayAsync(path, cancellationToken);
        var count = 0;
        foreach (var item in items)
        {
            var taxonomyKey = ReadString(item, "TaxonomyKey");
            var version = ReadString(item, "Version");
            if (string.IsNullOrWhiteSpace(taxonomyKey) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            var contentJson = ReadString(item, "ContentJson") ?? "{}";
            await RemoveTaxonomyReferenceStubArtifactsAsync(db, taxonomyKey, version, isActive: null, cancellationToken);
            var updatedAt = ReadDateTime(item, "UpdatedAt") ?? DateTime.UtcNow;
            var artifact = new AIRuleArtifact
            {
                ArtifactType = "Taxonomy",
                ArtifactKey = taxonomyKey,
                Version = version,
                IsActive = ReadBool(item, "IsActive") ?? true,
                ContentJson = contentJson,
                Description = ReadString(item, "Description"),
                Summary = "Unified tag taxonomy artifact",
                Source = "db-reference",
                SourcePath = ReadString(item, "SourcePath") ?? Path.GetFileName(path),
                ContentHash = AIRuleArtifactStore.ComputeContentHash(contentJson),
                CreatedBy = ReadObjectId(item, "CreatedBy"),
                UpdatedBy = ReadObjectId(item, "UpdatedBy"),
                CreatedAt = ReadDateTime(item, "CreatedAt") ?? updatedAt,
                UpdatedAt = updatedAt,
                ActivatedAt = (ReadBool(item, "IsActive") ?? true) ? updatedAt : null
            };

            if (artifact.IsActive)
            {
                await AIRuleArtifactStore.ActivateAsync(db, artifact, cancellationToken);
            }
            else
            {
                await AIRuleArtifactStore.UpsertAsync(db, artifact, cancellationToken);
            }

            count++;
        }

        return count;
    }

    private static async Task<int> SeedHistorySnapshotAsync(
        DbContext db,
        string root,
        IReadOnlyDictionary<string, string> taxonomySnapshotLookup,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, "APE_DB_Test.AI_Artifact_History.json");
        if (!File.Exists(path))
        {
            return 0;
        }

        var items = await LoadArrayAsync(path, cancellationToken);
        var count = 0;
        foreach (var item in items)
        {
            var artifactKey = NormalizeHistoryArtifactKey(ReadString(item, "ArtifactKey"), ReadString(item, "SourcePath"));
            var version = ReadString(item, "Version");
            var artifactType = NormalizeHistoryArtifactType(ReadString(item, "ArtifactType"), ReadString(item, "ArtifactKey"));
            var contentJson = ExtractActualContentJson(item, artifactType, artifactKey, version, taxonomySnapshotLookup);
            if (string.IsNullOrWhiteSpace(artifactKey) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            var updatedAt = ReadDateTime(item, "SnapshottedAt") ?? DateTime.UtcNow;
            var artifact = new AIRuleArtifact
            {
                ArtifactKey = artifactKey,
                ArtifactType = artifactType,
                Version = version,
                IsActive = false,
                ContentJson = contentJson,
                Description = ReadString(item, "Description"),
                Source = ReadString(item, "Source") ?? "db",
                SourcePath = ReadString(item, "SourcePath"),
                ContentHash = AIRuleArtifactStore.ComputeContentHash(contentJson),
                CreatedAt = updatedAt,
                UpdatedAt = updatedAt
            };

            if (string.Equals(artifact.ArtifactType, "Taxonomy", StringComparison.OrdinalIgnoreCase))
            {
                await RemoveTaxonomyReferenceStubArtifactsAsync(db, artifact.ArtifactKey, artifact.Version, isActive: false, cancellationToken);
            }

            await AIRuleArtifactStore.UpsertAsync(db, artifact, cancellationToken);
            count++;
        }

        return count;
    }

    private static async Task<int> SeedAgentsAsync(DbContext db, string root, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, "APE_DB_Test.AIAgents.json");
        if (!File.Exists(path))
        {
            return 0;
        }

        var items = await LoadArrayAsync(path, cancellationToken);
        var count = 0;
        foreach (var item in items)
        {
            var roleText = ReadString(item, "AgentRole");
            if (!Enum.TryParse<AIAgentRole>(roleText, ignoreCase: true, out var role))
            {
                continue;
            }

            var filter = Builders<AIAgent>.Filter.Eq(x => x.AgentRole, role);
            var existing = await db.AIAgents.Find(filter).FirstOrDefaultAsync(cancellationToken);

            var entity = new AIAgent
            {
                Id = existing?.Id ?? ReadObjectId(item, "_id") ?? ObjectId.GenerateNewId().ToString(),
                AgentRole = role,
                Provider = ParseProvider(ReadString(item, "Provider")),
                ModelName = ReadString(item, "ModelName") ?? string.Empty,
                MaxTokens = ReadInt(item, "MaxTokens"),
                Temperature = ReadDouble(item, "Temperature"),
                CreditCost = ReadDouble(item, "CreditCost") ?? 0,
                IsEnabled = ReadBool(item, "IsEnabled") ?? true,
                FallbackProvider = ParseNullableProvider(ReadString(item, "FallbackProvider")),
                FallbackModelName = ReadString(item, "FallbackModelName"),
                UpdatedBy = ReadObjectId(item, "UpdatedBy"),
                UpdatedAt = ReadDateTime(item, "UpdatedAt") ?? DateTime.UtcNow,
                Notes = ReadString(item, "Notes")
            };

            await db.AIAgents.ReplaceOneAsync(
                Builders<AIAgent>.Filter.Eq(x => x.AgentRole, entity.AgentRole),
                entity,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
            count++;
        }

        return count;
    }
    private static AIProvider ParseProvider(string? value)
    {
        return Enum.TryParse<AIProvider>(value, ignoreCase: true, out var provider)
            ? provider
            : AIProvider.OpenAI;
    }

    private static AIProvider? ParseNullableProvider(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<AIProvider>(value, ignoreCase: true, out var provider)
            ? provider
            : null;
    }

    private static string NormalizeHistoryArtifactType(string? artifactType, string? artifactKey)
    {
        if (string.Equals(artifactType, "GroundTruth", StringComparison.OrdinalIgnoreCase))
        {
            return "GroundTruth";
        }

        if (string.Equals(artifactType, "Rubric", StringComparison.OrdinalIgnoreCase))
        {
            return "Rubric";
        }

        if (string.Equals(artifactType, "Taxonomy", StringComparison.OrdinalIgnoreCase))
        {
            return "Taxonomy";
        }

        if (!string.IsNullOrWhiteSpace(artifactKey) && artifactKey.StartsWith("prompt:", StringComparison.OrdinalIgnoreCase))
        {
            return "Prompt";
        }

        return "Policy";
    }

    private static string NormalizeHistoryArtifactKey(string? artifactKey, string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(artifactKey))
        {
            return sourcePath ?? string.Empty;
        }

        if (artifactKey.StartsWith("prompt:", StringComparison.OrdinalIgnoreCase))
        {
            return artifactKey["prompt:".Length..];
        }

        if (artifactKey.StartsWith("taxonomy:", StringComparison.OrdinalIgnoreCase))
        {
            return "tag-taxonomy";
        }

        if (artifactKey.StartsWith("rubric:", StringComparison.OrdinalIgnoreCase))
        {
            return $"ai-rubrics/{artifactKey["rubric:".Length..]}.json";
        }

        if (artifactKey.StartsWith("groundtruth:", StringComparison.OrdinalIgnoreCase))
        {
            return $"ai-groundtruth/{artifactKey["groundtruth:".Length..]}.json";
        }

        if (artifactKey.StartsWith("policy:", StringComparison.OrdinalIgnoreCase))
        {
            return $"{artifactKey["policy:".Length..]}.json";
        }

        return artifactKey;
    }

    private static string ExtractActualContentJson(
        JsonElement item,
        string artifactType,
        string artifactKey,
        string? version,
        IReadOnlyDictionary<string, string> taxonomySnapshotLookup)
    {
        var contentJson = ReadString(item, "ContentJson") ?? "{}";
        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("content", out var content) &&
            content.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            if (string.Equals(ReadString(root, "artifact_type"), "prompt", StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Serialize(new
                {
                    key = ReadString(root, "stable_id") ?? ReadString(content, "key"),
                    version = ReadString(root, "version") ?? ReadString(content, "version") ?? "v1",
                    description = ReadString(root, "description"),
                    systemPrompt = ReadString(content, "system_prompt") ?? ReadString(content, "systemPrompt") ?? string.Empty,
                    userPrompt = ReadString(content, "user_prompt") ?? ReadString(content, "userPrompt") ?? string.Empty,
                    variables = ReadStringList(content, "variables") ?? ReadStringList(content, "Variables"),
                    isActive = ReadBool(root, "is_active") ?? false
                });
            }

            if (string.Equals(artifactType, "Taxonomy", StringComparison.OrdinalIgnoreCase))
            {
                var resolved = ResolveTaxonomyHistoryContent(content, artifactKey, version, taxonomySnapshotLookup);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }

            return content.GetRawText();
        }

        if (string.Equals(artifactType, "Taxonomy", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ResolveTaxonomyHistoryContent(root, artifactKey, version, taxonomySnapshotLookup);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return contentJson;
    }

    private static async Task<Dictionary<string, string>> LoadTaxonomySnapshotLookupAsync(string root, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, "APE_DB_Test.AI_Tag_Taxonomies.json");
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return lookup;
        }

        var items = await LoadArrayAsync(path, cancellationToken);
        foreach (var item in items)
        {
            var taxonomyKey = ReadString(item, "TaxonomyKey");
            var version = ReadString(item, "Version");
            var contentJson = ReadString(item, "ContentJson");
            if (string.IsNullOrWhiteSpace(taxonomyKey) || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(contentJson))
            {
                continue;
            }

            lookup[$"{taxonomyKey}::{version}"] = contentJson;
        }

        return lookup;
    }

    private static string? ResolveTaxonomyHistoryContent(
        JsonElement candidate,
        string artifactKey,
        string? version,
        IReadOnlyDictionary<string, string> taxonomySnapshotLookup)
    {
        if (!LooksLikeTaxonomyReferenceStub(candidate))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(version) &&
            taxonomySnapshotLookup.TryGetValue($"{artifactKey}::{version}", out var byVersion))
        {
            return byVersion;
        }

        return taxonomySnapshotLookup.TryGetValue($"{artifactKey}::v1", out var fallback)
            ? fallback
            : null;
    }

    private static bool LooksLikeTaxonomyReferenceStub(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return string.Equals(ReadString(element, "artifact_type"), "TagTaxonomy", StringComparison.OrdinalIgnoreCase) &&
               !element.TryGetProperty("subjects", out _);
    }

    private static async Task RemoveTaxonomyReferenceStubArtifactsAsync(
        DbContext db,
        string artifactKey,
        string version,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var builder = Builders<AIRuleArtifact>.Filter;
        var filter = builder.Eq(x => x.ArtifactType, "Taxonomy") &
                     builder.Eq(x => x.ArtifactKey, artifactKey) &
                     builder.Eq(x => x.Version, version);

        if (isActive.HasValue)
        {
            filter &= builder.Eq(x => x.IsActive, isActive.Value);
        }

        var candidates = await db.AIRuleArtifacts.Find(filter).ToListAsync(cancellationToken);
        var invalidIds = candidates
            .Where(item => IsTaxonomyReferenceStubJson(item.ContentJson))
            .Select(item => item.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();
        if (invalidIds.Count == 0)
        {
            return;
        }

        await db.AIRuleArtifacts.DeleteManyAsync(builder.In(x => x.Id, invalidIds), cancellationToken);
    }

    private static bool IsTaxonomyReferenceStubJson(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(contentJson);
            return LooksLikeTaxonomyReferenceStub(document.RootElement);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<List<JsonElement>> LoadArrayAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().Select(item => item.Clone()).ToList()
            : new List<JsonElement>();
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Object when property.TryGetProperty("$oid", out var oid) && oid.ValueKind == JsonValueKind.String => oid.GetString(),
            JsonValueKind.Object when property.TryGetProperty("$date", out var date) && date.ValueKind == JsonValueKind.String => date.GetString(),
            _ => null
        };
    }

    private static string? ReadObjectId(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return property.ValueKind == JsonValueKind.Object &&
               property.TryGetProperty("$oid", out var oid) &&
               oid.ValueKind == JsonValueKind.String
            ? oid.GetString()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static double? ReadDouble(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDouble(out var value) => value,
            JsonValueKind.String when double.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static List<string>? ReadStringList(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return property.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
    }

    private static DateTime? ReadDateTime(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(property.GetString(), out var stringValue))
        {
            return stringValue.ToUniversalTime();
        }

        if (property.ValueKind == JsonValueKind.Object &&
            property.TryGetProperty("$date", out var date))
        {
            if (date.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(date.GetString(), out var objectValue))
            {
                return objectValue.ToUniversalTime();
            }

            if (date.ValueKind == JsonValueKind.Number && date.TryGetInt64(out var epochMs))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime;
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        property = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out property);
    }
}

public sealed class DbReferenceSeedSummary
{
    public int RuleArtifacts { get; set; }
    public int Agents { get; set; }
}
