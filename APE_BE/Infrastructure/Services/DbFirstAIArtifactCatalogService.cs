using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Infrastructure.AI;

public class DbFirstAIArtifactCatalogService : IAIArtifactCatalogService
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly DbContext _dbContext;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<DbFirstAIArtifactCatalogService> _logger;

    public DbFirstAIArtifactCatalogService(
        IMemoryCache cache,
        IConfiguration configuration,
        DbContext dbContext,
        IHostEnvironment hostEnvironment,
        ILogger<DbFirstAIArtifactCatalogService> logger)
    {
        _cache = cache;
        _configuration = configuration;
        _dbContext = dbContext;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<AIPromptTemplateDto?> GetPromptAsync(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"ai-prompt::{key}";
        if (_cache.TryGetValue(cacheKey, out AIPromptTemplateDto? cached) && cached is not null)
        {
            return cached;
        }

        var prompt = await TryGetPromptFromDbAsync(key, cancellationToken);
        if (prompt is null)
        {
            if (AllowFileFallback())
            {
                _logger.LogWarning(
                    "AI prompt '{PromptKey}' was not found in DB. Falling back to file artifacts because file fallback is enabled for environment '{EnvironmentName}'.",
                    key,
                    _hostEnvironment.EnvironmentName);
                prompt = await TryGetPromptFromFileAsync(key, cancellationToken);
            }
            else
            {
                throw BuildDbOnlyMissingArtifactException("prompt", key);
            }
        }

        if (prompt is not null)
        {
            _cache.Set(cacheKey, prompt, TimeSpan.FromMinutes(10));
        }

        return prompt;
    }

    public async Task<AIJsonArtifactDto?> GetArtifactAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeArtifactKey(fileName);
        var cacheKey = $"ai-artifact::{normalizedKey}";
        if (_cache.TryGetValue(cacheKey, out AIJsonArtifactDto? cached) && cached is not null)
        {
            return cached;
        }

        var artifact = await TryGetArtifactFromDbAsync(normalizedKey, cancellationToken);
        if (artifact is null)
        {
            if (AllowFileFallback())
            {
                _logger.LogWarning(
                    "AI artifact '{ArtifactKey}' was not found in DB. Falling back to file artifacts because file fallback is enabled for environment '{EnvironmentName}'.",
                    normalizedKey,
                    _hostEnvironment.EnvironmentName);
                artifact = await TryGetArtifactFromFileAsync(fileName, cancellationToken);
            }
            else
            {
                throw BuildDbOnlyMissingArtifactException("artifact", normalizedKey);
            }
        }

        if (artifact is not null)
        {
            _cache.Set(cacheKey, artifact, TimeSpan.FromMinutes(10));
        }

        return artifact;
    }

    public async Task<JsonElement?> GetPolicyAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var artifact = await GetArtifactAsync(fileName, cancellationToken);
        return artifact?.Content;
    }

    private async Task<AIPromptTemplateDto?> TryGetPromptFromDbAsync(string key, CancellationToken cancellationToken)
    {
        return await TryGetPromptFromUnifiedDbAsync(key, cancellationToken);
    }

    private async Task<AIJsonArtifactDto?> TryGetArtifactFromDbAsync(string artifactKey, CancellationToken cancellationToken)
    {
        return await TryGetArtifactFromUnifiedDbAsync(artifactKey, cancellationToken);
    }

    private async Task<AIPromptTemplateDto?> TryGetPromptFromUnifiedDbAsync(string key, CancellationToken cancellationToken)
    {
        var filter = Builders<AIRuleArtifact>.Filter.Eq(item => item.ArtifactType, "Prompt") &
                     Builders<AIRuleArtifact>.Filter.Eq(item => item.ArtifactKey, key) &
                     Builders<AIRuleArtifact>.Filter.Eq(item => item.IsActive, true);

        var artifact = await _dbContext.AIRuleArtifacts
            .Find(filter)
            .SortByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (artifact is null || string.IsNullOrWhiteSpace(artifact.ContentJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(artifact.ContentJson);
        var root = document.RootElement;
        return new AIPromptTemplateDto
        {
            Key = artifact.ArtifactKey,
            Version = artifact.Version,
            Description = artifact.Description ?? TryReadString(root, "description") ?? string.Empty,
            ArtifactType = artifact.ArtifactType,
            Source = "db",
            SystemPrompt = TryReadString(root, "systemPrompt") ?? TryReadString(root, "SystemPrompt") ?? string.Empty,
            UserPrompt = TryReadString(root, "userPrompt") ?? TryReadString(root, "UserPrompt") ?? string.Empty,
            Variables = TryReadStringList(root, "variables") ?? TryReadStringList(root, "Variables"),
            IsActive = artifact.IsActive,
            UpdatedAt = artifact.UpdatedAt
        };
    }

    private async Task<AIJsonArtifactDto?> TryGetArtifactFromUnifiedDbAsync(string artifactKey, CancellationToken cancellationToken)
    {
        var filter = Builders<AIRuleArtifact>.Filter.Ne(item => item.ArtifactType, "Prompt") &
                     Builders<AIRuleArtifact>.Filter.Eq(item => item.ArtifactKey, artifactKey) &
                     Builders<AIRuleArtifact>.Filter.Eq(item => item.IsActive, true);

        var artifact = await _dbContext.AIRuleArtifacts
            .Find(filter)
            .SortByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (artifact is null || string.IsNullOrWhiteSpace(artifact.ContentJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(artifact.ContentJson);
        return new AIJsonArtifactDto
        {
            ArtifactKey = artifact.ArtifactKey,
            ArtifactType = artifact.ArtifactType,
            Version = artifact.Version,
            Description = artifact.Description ?? string.Empty,
            Source = "db",
            IsActive = artifact.IsActive,
            UpdatedAt = artifact.UpdatedAt,
            Content = document.RootElement.Clone()
        };
    }

    private async Task<AIPromptTemplateDto?> TryGetPromptFromFileAsync(string key, CancellationToken cancellationToken)
    {
        var prompts = await GetPromptCatalogFromFileAsync(cancellationToken);
        return prompts.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<AIPromptTemplateDto>> GetPromptCatalogFromFileAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "ai-prompts-file-catalog";
        if (_cache.TryGetValue(cacheKey, out List<AIPromptTemplateDto>? cached) && cached is not null)
        {
            return cached;
        }

        var promptFiles = ResolvePromptCatalogPaths();
        if (promptFiles.Count == 0)
        {
            _logger.LogWarning("AI prompt catalog files not found in AI Rule Config or App_Data.");
            return new List<AIPromptTemplateDto>();
        }

        var prompts = new List<AIPromptTemplateDto>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in promptFiles)
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var key = TryReadString(item, "Key") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key) || !seenKeys.Add(key))
                {
                    continue;
                }

                prompts.Add(new AIPromptTemplateDto
                {
                    Key = key,
                    Version = TryReadString(item, "Version") ?? string.Empty,
                    Description = TryReadString(item, "Description") ?? string.Empty,
                    ArtifactType = "Prompt",
                    Source = "file",
                    SystemPrompt = TryReadString(item, "SystemPrompt") ?? string.Empty,
                    UserPrompt = TryReadString(item, "UserPrompt") ?? string.Empty,
                    Variables = TryReadStringList(item, "Variables"),
                    IsActive = TryReadBool(item, "IsActive") ?? true,
                    UpdatedAt = TryReadDateTimeOffset(item, "UpdatedAt")
                });
            }
        }

        _cache.Set(cacheKey, prompts, TimeSpan.FromMinutes(10));
        return prompts;
    }

    private List<string> ResolvePromptCatalogPaths()
    {
        var configuredRoot = _configuration["AI:ArtifactPath"];
        var primaryCandidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            primaryCandidates.Add(Path.Combine(configuredRoot, "ai-prompts.json"));
        }

        primaryCandidates.AddRange(new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "AI Rule Config", "ai-prompts.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "AI Rule Config", "ai-prompts.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AI Rule Config", "ai-prompts.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "AI Rule Config", "ai-prompts.json")
        });

        var primaryMatches = primaryCandidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToList();
        if (primaryMatches.Count > 0)
        {
            return primaryMatches;
        }

        var fallbackCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "App_Data", "ai-prompts.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "ai-prompts.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Data", "App_Data", "ai-prompts.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Infrastructure", "Data", "App_Data", "ai-prompts.json")
        };

        return fallbackCandidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToList();
    }

    private async Task<AIJsonArtifactDto?> TryGetArtifactFromFileAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = ResolvePath(fileName);
        if (!File.Exists(path))
        {
            _logger.LogWarning("AI policy file not found: {Path}", path);
            return null;
        }

        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement.Clone();
        var normalizedKey = NormalizeArtifactKey(fileName);
        return new AIJsonArtifactDto
        {
            ArtifactKey = normalizedKey,
            ArtifactType = InferArtifactType(normalizedKey),
            Version = TryReadString(root, "version") ?? "file-current",
            Description = TryReadString(root, "description") ?? Path.GetFileName(normalizedKey),
            Source = "file",
            IsActive = TryReadBool(root, "is_active") ?? true,
            UpdatedAt = TryReadDateTimeOffset(root, "updated_at") ?? TryReadDateTimeOffset(root, "updatedAt"),
            Content = root
        };
    }

    private string ResolvePath(string fileName)
    {
        var configuredRoot = _configuration["AI:ArtifactPath"];
        var normalized = fileName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            candidates.Add(Path.Combine(configuredRoot, normalized));
        }

        candidates.AddRange(new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "AI Rule Config", normalized),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "AI Rule Config", normalized),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AI Rule Config", normalized),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "AI Rule Config", normalized),
            Path.Combine(AppContext.BaseDirectory, "App_Data", normalized),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data", normalized),
            Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Data", "App_Data", normalized),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Infrastructure", "Data", "App_Data", normalized)
        });

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return Path.GetFullPath(candidates[0]);
    }

    private bool AllowFileFallback()
    {
        var configured = _configuration.GetValue<bool?>("AI:Artifacts:AllowFileFallback");
        if (configured.HasValue)
        {
            return configured.Value;
        }

        return !_hostEnvironment.IsProduction();
    }

    private InvalidOperationException BuildDbOnlyMissingArtifactException(string artifactType, string key)
    {
        return new InvalidOperationException(
            $"AI {artifactType} '{key}' is missing from DB while runtime is in DB-only mode. " +
            "Seed the required AI Rule Config artifacts into DB or explicitly enable AI:Artifacts:AllowFileFallback for local/non-production use.");
    }

    private static string NormalizeArtifactKey(string fileName)
    {
        return fileName.Replace('\\', '/').Trim();
    }

    private static string? TryReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool? TryReadBool(JsonElement element, string propertyName)
    {
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

    private static DateTimeOffset? TryReadDateTimeOffset(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               DateTimeOffset.TryParse(property.GetString(), out var value)
            ? value
            : null;
    }

    private static List<string>? TryReadStringList(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
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

        return "Policy";
    }
}
