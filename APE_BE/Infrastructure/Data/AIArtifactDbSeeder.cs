using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace Infrastructure.Data;

public static class AIArtifactDbSeeder
{
    public static async Task SeedAsync(
        DbContext db,
        IConfiguration configuration,
        IHostEnvironment? environment = null,
        CancellationToken cancellationToken = default)
    {
        var root = ResolveArtifactRoot(configuration);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return;
        }

        await SeedPromptRulesAsync(db, root, cancellationToken);
        await SeedActiveRuleArtifactsAsync(db, root, cancellationToken);
        await SeedTaxonomyRulesAsync(db, root, cancellationToken);
        await SeedRuleHistoryAsync(db, root, cancellationToken);

        var dbReferenceRoot = configuration["AI:DbReferenceSeedPath"];
        if (!string.IsNullOrWhiteSpace(dbReferenceRoot) && Directory.Exists(dbReferenceRoot))
        {
            await AIDbReferenceSeeder.SeedAsync(db, dbReferenceRoot, cancellationToken);
        }
    }

    private static async Task SeedPromptRulesAsync(DbContext db, string root, CancellationToken cancellationToken)
    {
        var promptPath = Path.Combine(root, "ai-prompts.json");
        if (!File.Exists(promptPath))
        {
            return;
        }

        await using var stream = File.OpenRead(promptPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var promptKey = ReadString(item, "Key");
            if (string.IsNullOrWhiteSpace(promptKey))
            {
                continue;
            }

            var version = ReadString(item, "Version") ?? "v1";
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
                Source = "app-data",
                SourcePath = NormalizeRelativePath(root, promptPath),
                ContentHash = AIRuleArtifactStore.ComputeContentHash(promptContentJson),
                CreatedAt = updatedAt,
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
        }
    }

    private static async Task SeedActiveRuleArtifactsAsync(DbContext db, string root, CancellationToken cancellationToken)
    {
        var artifactFiles = new[]
        {
            "code-mentor-policy.json",
            "embedding-tagging-policy.json",
            "extracted-content-policy.json",
            "gatekeeper-policy.json",
            "question-generation-review-policy.json"
        }
        .Select(file => Path.Combine(root, file))
        .Where(File.Exists)
        .ToList();

        var rubricDir = Path.Combine(root, "ai-rubrics");
        if (Directory.Exists(rubricDir))
        {
            artifactFiles.AddRange(Directory.GetFiles(rubricDir, "*.json", SearchOption.TopDirectoryOnly));
        }

        var groundtruthDir = Path.Combine(root, "ai-groundtruth");
        if (Directory.Exists(groundtruthDir))
        {
            artifactFiles.AddRange(Directory.GetFiles(groundtruthDir, "*.json", SearchOption.TopDirectoryOnly));
        }

        foreach (var path in artifactFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var relativeKey = NormalizeRelativePath(root, path);
            var contentJson = await File.ReadAllTextAsync(path, cancellationToken);
            using var document = JsonDocument.Parse(contentJson);
            var rootElement = document.RootElement;
            var updatedAt = ReadDateTime(rootElement, "updated_at") ?? ReadDateTime(rootElement, "updatedAt") ?? DateTime.UtcNow;
            var artifact = new AIRuleArtifact
            {
                ArtifactType = InferArtifactType(relativeKey),
                ArtifactKey = relativeKey,
                Version = ReadString(rootElement, "version") ?? "file-current",
                IsActive = ReadBool(rootElement, "is_active") ?? true,
                ContentJson = contentJson,
                Description = ReadString(rootElement, "description") ?? Path.GetFileName(relativeKey),
                Summary = $"{InferArtifactType(relativeKey)} artifact",
                Source = "app-data",
                SourcePath = relativeKey,
                ContentHash = AIRuleArtifactStore.ComputeContentHash(contentJson),
                CreatedAt = updatedAt,
                UpdatedAt = updatedAt,
                ActivatedAt = (ReadBool(rootElement, "is_active") ?? true) ? updatedAt : null
            };

            if (artifact.IsActive)
            {
                await AIRuleArtifactStore.ActivateAsync(db, artifact, cancellationToken);
            }
            else
            {
                await AIRuleArtifactStore.UpsertAsync(db, artifact, cancellationToken);
            }
        }
    }

    private static async Task SeedTaxonomyRulesAsync(DbContext db, string root, CancellationToken cancellationToken)
    {
        var taxonomyPath = Path.Combine(root, "tag-taxonomy.json");
        if (!File.Exists(taxonomyPath))
        {
            return;
        }

        var contentJson = await File.ReadAllTextAsync(taxonomyPath, cancellationToken);
        using var document = JsonDocument.Parse(contentJson);
        var rootElement = document.RootElement;
        var updatedAt = ReadDateTime(rootElement, "updatedAt") ?? ReadDateTime(rootElement, "updated_at") ?? DateTime.UtcNow;
        await RemoveTaxonomyReferenceStubArtifactsAsync(
            db,
            artifactKey: "tag-taxonomy",
            version: ReadString(rootElement, "version") ?? "v1",
            isActive: null,
            cancellationToken);
        var artifact = new AIRuleArtifact
        {
            ArtifactType = "Taxonomy",
            ArtifactKey = "tag-taxonomy",
            Version = ReadString(rootElement, "version") ?? "v1",
            IsActive = ReadBool(rootElement, "is_active") ?? true,
            ContentJson = contentJson,
            Description = "Canonical AI tag taxonomy",
            Summary = "Unified tag taxonomy artifact",
            Source = "app-data",
            SourcePath = NormalizeRelativePath(root, taxonomyPath),
            ContentHash = AIRuleArtifactStore.ComputeContentHash(contentJson),
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
            ActivatedAt = (ReadBool(rootElement, "is_active") ?? true) ? updatedAt : null
        };

        if (artifact.IsActive)
        {
            await AIRuleArtifactStore.ActivateAsync(db, artifact, cancellationToken);
        }
        else
        {
            await AIRuleArtifactStore.UpsertAsync(db, artifact, cancellationToken);
        }
    }

    private static async Task SeedRuleHistoryAsync(DbContext db, string root, CancellationToken cancellationToken)
    {
        var historyRoot = Path.Combine(root, "ai-config-history");
        if (!Directory.Exists(historyRoot))
        {
            return;
        }

        var files = Directory.GetFiles(historyRoot, "*.json", SearchOption.AllDirectories);
        foreach (var path in files)
        {
            var rawJson = await File.ReadAllTextAsync(path, cancellationToken);
            using var document = JsonDocument.Parse(rawJson);
            var artifact = BuildHistoryRuleArtifact(root, path, document.RootElement);
            if (artifact is null)
            {
                continue;
            }

            if (string.Equals(artifact.ArtifactType, "Taxonomy", StringComparison.OrdinalIgnoreCase))
            {
                await RemoveTaxonomyReferenceStubArtifactsAsync(
                    db,
                    artifact.ArtifactKey,
                    artifact.Version,
                    isActive: false,
                    cancellationToken);
            }

            await AIRuleArtifactStore.UpsertAsync(db, artifact, cancellationToken);
        }
    }

    private static string ResolveArtifactRoot(IConfiguration configuration)
    {
        var configuredRoot = configuration["AI:ArtifactPath"];
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            candidates.Add(configuredRoot);
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "App_Data"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "App_Data"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Data", "App_Data"));
        candidates.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Infrastructure", "Data", "App_Data")));
        candidates.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Infrastructure", "Data", "App_Data")));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "AI Rule Config"));
        candidates.Add(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "AI Rule Config")));
        candidates.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AI Rule Config")));
        candidates.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "AI Rule Config")));
        candidates.Add(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "APE_BE-minh", "APE_BE-minh", "AI Rule Config")));
        candidates.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "APE_BE-minh", "APE_BE-minh", "AI Rule Config")));

        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(HasUsableArtifactFiles) ?? string.Empty;
    }

    private static bool HasUsableArtifactFiles(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        return File.Exists(Path.Combine(path, "ai-prompts.json")) ||
               File.Exists(Path.Combine(path, "tag-taxonomy.json")) ||
               File.Exists(Path.Combine(path, "question-generation-review-policy.json"));
    }

    private static AIRuleArtifact? BuildHistoryRuleArtifact(string root, string path, JsonElement rootElement)
    {
        var relativePath = NormalizeRelativePath(root, path);
        var inferredType = InferArtifactTypeFromHistory(relativePath, rootElement);
        var inferredKey = InferRuleArtifactKeyFromHistory(relativePath, rootElement);
        if (string.IsNullOrWhiteSpace(inferredType) || string.IsNullOrWhiteSpace(inferredKey))
        {
            return null;
        }

        var actualContent = ExtractActualRuleContent(root, inferredType, rootElement);
        var contentJson = actualContent.GetRawText();
        var snapshotTime = ExtractSnapshotTimeFromFileName(Path.GetFileNameWithoutExtension(path)) ?? ReadDateTime(rootElement, "captured_at") ?? DateTime.UtcNow;
        return new AIRuleArtifact
        {
            ArtifactType = inferredType,
            ArtifactKey = inferredKey,
            Version = InferHistoryVersion(relativePath, rootElement),
            // History snapshots are stored for audit/reference only.
            // The active artifact for a given key/scope is seeded separately
            // by SeedActiveRuleArtifactsAsync, so history must never claim the
            // active slot guarded by the unique partial index.
            IsActive = false,
            ContentJson = contentJson,
            Description = ReadString(rootElement, "description") ?? Path.GetFileName(relativePath),
            Summary = ReadString(rootElement, "change_summary"),
            ChangeReason = ReadString(rootElement, "change_reason"),
            Source = "file-history",
            SourcePath = relativePath,
            ContentHash = AIRuleArtifactStore.ComputeContentHash(contentJson),
            CreatedAt = snapshotTime,
            UpdatedAt = snapshotTime,
            ActivatedAt = null
        };
    }

    private static string NormalizeRelativePath(string root, string fullPath)
    {
        return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    }

    private static string InferArtifactType(string artifactKey)
    {
        if (artifactKey.Contains("rubric", StringComparison.OrdinalIgnoreCase))
        {
            return "Rubric";
        }

        if (artifactKey.Contains("groundtruth", StringComparison.OrdinalIgnoreCase))
        {
            return "GroundTruth";
        }

        if (artifactKey.Contains("taxonomy", StringComparison.OrdinalIgnoreCase))
        {
            return "Taxonomy";
        }

        return "Policy";
    }

    private static string InferArtifactTypeFromHistory(string relativeHistoryPath, JsonElement rootElement)
    {
        var normalized = relativeHistoryPath.Replace('\\', '/');
        if (normalized.Contains("/prompts/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ReadString(rootElement, "artifact_type"), "prompt", StringComparison.OrdinalIgnoreCase))
        {
            return "Prompt";
        }

        if (normalized.Contains("/rubrics/", StringComparison.OrdinalIgnoreCase))
        {
            return "Rubric";
        }

        if (normalized.Contains("/groundtruth/", StringComparison.OrdinalIgnoreCase))
        {
            return "GroundTruth";
        }

        if (normalized.Contains("/taxonomy/", StringComparison.OrdinalIgnoreCase))
        {
            return "Taxonomy";
        }

        if (normalized.Contains("/policies/", StringComparison.OrdinalIgnoreCase))
        {
            return "Policy";
        }

        return InferArtifactType(normalized);
    }

    private static string InferRuleArtifactKeyFromHistory(string relativeHistoryPath, JsonElement rootElement)
    {
        var normalized = relativeHistoryPath.Replace('\\', '/');
        if (normalized.Contains("/prompts/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized.Split('/').Reverse().Skip(1).FirstOrDefault() ?? normalized;
        }

        if (normalized.Contains("/taxonomy/", StringComparison.OrdinalIgnoreCase))
        {
            return "tag-taxonomy";
        }

        if (normalized.Contains("/rubrics/", StringComparison.OrdinalIgnoreCase))
        {
            var directory = normalized.Split('/').Reverse().Skip(1).FirstOrDefault();
            return string.IsNullOrWhiteSpace(directory) ? normalized : $"ai-rubrics/{directory}.json";
        }

        if (normalized.Contains("/groundtruth/", StringComparison.OrdinalIgnoreCase))
        {
            var directory = normalized.Split('/').Reverse().Skip(1).FirstOrDefault();
            return string.IsNullOrWhiteSpace(directory) ? normalized : $"ai-groundtruth/{directory}.json";
        }

        if (normalized.Contains("/policies/", StringComparison.OrdinalIgnoreCase))
        {
            var directory = normalized.Split('/').Reverse().Skip(1).FirstOrDefault();
            return string.IsNullOrWhiteSpace(directory) ? normalized : $"{directory}.json";
        }

        return ReadString(rootElement, "stable_id") ??
               ReadString(rootElement, "artifact_key") ??
               normalized;
    }

    private static JsonElement ExtractActualRuleContent(string artifactRoot, string inferredType, JsonElement rootElement)
    {
        if (rootElement.ValueKind == JsonValueKind.Object &&
            rootElement.TryGetProperty("content", out var content) &&
            content.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            if (string.Equals(ReadString(rootElement, "artifact_type"), "prompt", StringComparison.OrdinalIgnoreCase))
            {
                return BuildPromptContentSnapshot(rootElement, content);
            }

            if (string.Equals(inferredType, "Taxonomy", StringComparison.OrdinalIgnoreCase))
            {
                var resolved = TryResolveTaxonomyContent(artifactRoot, content);
                if (resolved.HasValue)
                {
                    return resolved.Value;
                }
            }

            return content;
        }

        if (string.Equals(inferredType, "Taxonomy", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = TryResolveTaxonomyContent(artifactRoot, rootElement);
            if (resolved.HasValue)
            {
                return resolved.Value;
            }
        }

        return rootElement;
    }

    private static JsonElement? TryResolveTaxonomyContent(string artifactRoot, JsonElement candidate)
    {
        if (!LooksLikeTaxonomyReferenceStub(candidate))
        {
            return null;
        }

        var sourceFile = ReadString(candidate, "source_file");
        var candidatePaths = new List<string>();
        if (!string.IsNullOrWhiteSpace(sourceFile))
        {
            candidatePaths.Add(Path.Combine(artifactRoot, sourceFile.Replace('/', Path.DirectorySeparatorChar)));
            candidatePaths.Add(Path.Combine(Path.GetDirectoryName(artifactRoot) ?? artifactRoot, sourceFile.Replace('/', Path.DirectorySeparatorChar)));
        }

        candidatePaths.Add(Path.Combine(artifactRoot, "tag-taxonomy.json"));

        var path = candidatePaths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
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

    private static JsonElement BuildPromptContentSnapshot(JsonElement wrapper, JsonElement content)
    {
        var payload = new
        {
            key = ReadString(wrapper, "stable_id") ?? ReadString(content, "key"),
            version = ReadString(wrapper, "version") ?? ReadString(content, "version") ?? "v1",
            description = ReadString(wrapper, "description") ?? ReadString(content, "description"),
            systemPrompt = ReadString(content, "system_prompt") ?? ReadString(content, "systemPrompt") ?? string.Empty,
            userPrompt = ReadString(content, "user_prompt") ?? ReadString(content, "userPrompt") ?? string.Empty,
            variables = ReadStringList(content, "variables") ?? ReadStringList(content, "Variables"),
            isActive = ReadBool(wrapper, "is_active") ?? false
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        return document.RootElement.Clone();
    }

    private static string InferHistoryVersion(string relativeHistoryPath, JsonElement rootElement)
    {
        var fromJson = ReadString(rootElement, "version");
        if (!string.IsNullOrWhiteSpace(fromJson))
        {
            return fromJson!;
        }

        var fileName = Path.GetFileNameWithoutExtension(relativeHistoryPath);
        var parts = fileName.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1] : "snapshot";
    }

    private static DateTime? ExtractSnapshotTimeFromFileName(string fileNameWithoutExtension)
    {
        var prefix = fileNameWithoutExtension.Split('-', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length != 17)
        {
            return null;
        }

        if (!DateTime.TryParseExact(prefix, "yyyyMMddHHmmssfff", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var value))
        {
            return null;
        }

        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }

    private static List<string>? ReadStringList(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return property.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();
    }

    private static DateTime? ReadDateTime(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               DateTime.TryParse(property.GetString(), out var value)
            ? value.ToUniversalTime()
            : null;
    }
}
