using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Infrastructure.AI;

public sealed class DbFirstAITagTaxonomyProvider : IAITagTaxonomyProvider
{
    private readonly object _lock = new();
    private readonly DbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<DbFirstAITagTaxonomyProvider> _logger;
    private volatile AITagTaxonomyCatalog? _cached;

    public DbFirstAITagTaxonomyProvider(
        DbContext dbContext,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<DbFirstAITagTaxonomyProvider> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public AITagTaxonomyCatalog GetCatalog()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        lock (_lock)
        {
            if (_cached is not null)
            {
                return _cached;
            }

            _cached = LoadCatalog();
            return _cached;
        }
    }

    private AITagTaxonomyCatalog LoadCatalog()
    {
        var unified = TryLoadUnified();
        if (unified is not null)
        {
            return unified;
        }

        return TryLoadFromFile() ?? AITagTaxonomyCatalog.Empty;
    }

    private AITagTaxonomyCatalog? TryLoadUnified()
    {
        var artifact = _dbContext.AIRuleArtifacts
            .Find(Builders<AIRuleArtifact>.Filter.Eq(x => x.ArtifactType, "Taxonomy") &
                  Builders<AIRuleArtifact>.Filter.Eq(x => x.ArtifactKey, "tag-taxonomy") &
                  Builders<AIRuleArtifact>.Filter.Eq(x => x.IsActive, true))
            .SortByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        if (artifact is null || string.IsNullOrWhiteSpace(artifact.ContentJson))
        {
            return null;
        }

        try
        {
            return AITagTaxonomyCatalog.FromJson(artifact.ContentJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse unified taxonomy artifact from DB.");
            return null;
        }
    }

    private AITagTaxonomyCatalog? TryLoadFromFile()
    {
        if (!_hostEnvironment.IsProduction() && !_configuration.GetValue<bool>("AI:Artifacts:AllowFileFallback"))
        {
            // keep same local convenience behavior as other artifact fallback paths
        }

        var configuredRoot = _configuration["AI:ArtifactPath"];
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            candidates.Add(Path.Combine(configuredRoot, "tag-taxonomy.json"));
        }

        candidates.AddRange(new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "AI Rule Config", "tag-taxonomy.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "AI Rule Config", "tag-taxonomy.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AI Rule Config", "tag-taxonomy.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "AI Rule Config", "tag-taxonomy.json"),
            Path.Combine(AppContext.BaseDirectory, "App_Data", "tag-taxonomy.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "tag-taxonomy.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Data", "App_Data", "tag-taxonomy.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Infrastructure", "Data", "App_Data", "tag-taxonomy.json")
        });

        var path = candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);

        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogWarning("Tag taxonomy not found in DB or fallback file paths.");
            return null;
        }

        try
        {
            return AITagTaxonomyCatalog.FromJson(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse fallback file taxonomy at {Path}.", path);
            return null;
        }
    }
}
