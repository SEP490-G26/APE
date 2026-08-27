using Application.Interfaces;
using Application.Options;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.Serialization;

namespace Infrastructure.AI;

public class DbFirstAIProviderSettingsResolver : IAIProviderSettingsResolver
{
    private const string SettingName = "AI_PROVIDER_CREDENTIALS";
    private const string CacheKey = "ai-provider-credentials";
    private readonly AIOptions _options;
    private readonly DbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly IAISecretProtector _secretProtector;
    private readonly ILogger<DbFirstAIProviderSettingsResolver> _logger;

    public DbFirstAIProviderSettingsResolver(
        IOptions<AIOptions> options,
        DbContext dbContext,
        IMemoryCache cache,
        IAISecretProtector secretProtector,
        ILogger<DbFirstAIProviderSettingsResolver> logger)
    {
        _options = options.Value;
        _dbContext = dbContext;
        _cache = cache;
        _secretProtector = secretProtector;
        _logger = logger;
    }

    public AIProviderOptions GetProvider(string providerName)
    {
        var baseOptions = Clone(_options.GetProvider(providerName));
        var overrideOptions = GetOverride(providerName);
        if (overrideOptions is null)
        {
            return baseOptions;
        }

        if (!string.IsNullOrWhiteSpace(overrideOptions.ApiKey))
        {
            baseOptions.ApiKey = overrideOptions.ApiKey;
        }

        if (!string.IsNullOrWhiteSpace(overrideOptions.BaseUrl))
        {
            baseOptions.BaseUrl = overrideOptions.BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(overrideOptions.ApiVersion))
        {
            baseOptions.ApiVersion = overrideOptions.ApiVersion;
        }

        if (overrideOptions.TimeoutSeconds > 0)
        {
            baseOptions.TimeoutSeconds = overrideOptions.TimeoutSeconds;
        }

        if (overrideOptions.Headers.Count > 0)
        {
            foreach (var header in overrideOptions.Headers)
            {
                baseOptions.Headers[header.Key] = header.Value;
            }
        }

        baseOptions.Enabled = overrideOptions.Enabled;
        return baseOptions;
    }

    private AIProviderOptions? GetOverride(string providerName)
    {
        var map = _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return LoadOverrides();
        });

        return map is not null && map.TryGetValue(providerName, out var value)
            ? value
            : null;
    }

    public void Reload()
    {
        _cache.Remove(CacheKey);
    }

    private Dictionary<string, AIProviderOptions> LoadOverrides()
    {
        var filter = Builders<SystemSetting>.Filter.Eq(item => item.SettingName, SettingName);
        var setting = _dbContext.SystemSettings.Find(filter).FirstOrDefault();
        if (setting?.SettingData is null)
        {
            return new Dictionary<string, AIProviderOptions>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var bson = setting.SettingData switch
            {
                BsonDocument document => document,
                _ => setting.SettingData.ToBsonDocument()
            };

            var result = new Dictionary<string, AIProviderOptions>(StringComparer.OrdinalIgnoreCase);
            var providersNeedingRepair = new List<string>();
            foreach (var element in bson.Elements)
            {
                if (element.Value is not BsonDocument providerDoc)
                {
                    continue;
                }

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (providerDoc.TryGetValue("Headers", out var headersValue) && headersValue is BsonDocument headersDoc)
                {
                    foreach (var header in headersDoc.Elements)
                    {
                        headers[header.Name] = header.Value.AsString;
                    }
                }

                var apiKey = ReadApiKey(providerDoc, element.Name, out var unreadableProtectedKey);
                if (unreadableProtectedKey)
                {
                    var baseOptions = _options.GetProvider(element.Name);
                    if (!string.IsNullOrWhiteSpace(baseOptions.ApiKey))
                    {
                        apiKey = baseOptions.ApiKey;
                        providersNeedingRepair.Add(element.Name);
                        _logger.LogWarning(
                            "AI provider credential for {ProviderName} could not be decrypted from DB. Falling back to base configuration and scheduling DB repair for this runtime.",
                            element.Name);
                    }
                }

                result[element.Name] = new AIProviderOptions
                {
                    Enabled = providerDoc.TryGetValue("Enabled", out var enabledValue) ? enabledValue.ToBoolean() : true,
                    ApiKey = apiKey,
                    BaseUrl = providerDoc.TryGetValue("BaseUrl", out var baseUrlValue) ? baseUrlValue.AsString : string.Empty,
                    ApiVersion = providerDoc.TryGetValue("ApiVersion", out var apiVersionValue) ? apiVersionValue.AsString : string.Empty,
                    TimeoutSeconds = providerDoc.TryGetValue("TimeoutSeconds", out var timeoutValue) ? timeoutValue.ToInt32() : 0,
                    Headers = headers
                };
            }

            if (providersNeedingRepair.Count > 0)
            {
                TryRepairProtectedKeys(setting, bson, providersNeedingRepair);
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, AIProviderOptions>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static AIProviderOptions Clone(AIProviderOptions source)
    {
        return new AIProviderOptions
        {
            Enabled = source.Enabled,
            ApiKey = source.ApiKey,
            BaseUrl = source.BaseUrl,
            ApiVersion = source.ApiVersion,
            TimeoutSeconds = source.TimeoutSeconds,
            Headers = new Dictionary<string, string>(source.Headers, StringComparer.OrdinalIgnoreCase)
        };
    }

    private string ReadApiKey(BsonDocument providerDoc, string providerName, out bool unreadableProtectedKey)
    {
        unreadableProtectedKey = false;
        var rawApiKey = providerDoc.TryGetValue("ApiKey", out var apiKeyValue) ? apiKeyValue.AsString : string.Empty;
        if (string.IsNullOrWhiteSpace(rawApiKey))
        {
            return string.Empty;
        }

        try
        {
            return _secretProtector.Unprotect(rawApiKey);
        }
        catch
        {
            if (_secretProtector.IsProtected(rawApiKey))
            {
                unreadableProtectedKey = true;
                _logger.LogError(
                    "Failed to unprotect AI provider credential from DB for provider {ProviderName}. A protected API key could not be decrypted in the current runtime context.",
                    providerName);
                return string.Empty;
            }

            return rawApiKey;
        }
    }

    private void TryRepairProtectedKeys(SystemSetting setting, BsonDocument bson, IReadOnlyCollection<string> providersNeedingRepair)
    {
        try
        {
            var repaired = false;
            foreach (var providerName in providersNeedingRepair.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!bson.TryGetValue(providerName, out var providerValue) || providerValue is not BsonDocument providerDoc)
                {
                    continue;
                }

                var baseOptions = _options.GetProvider(providerName);
                if (string.IsNullOrWhiteSpace(baseOptions.ApiKey))
                {
                    continue;
                }

                providerDoc["ApiKey"] = _secretProtector.Protect(baseOptions.ApiKey);
                repaired = true;
            }

            if (!repaired)
            {
                return;
            }

            setting.SettingData = BsonSerializer.Deserialize<BsonDocument>(bson.ToJson());
            setting.LastUpdated = DateTime.UtcNow;
            _dbContext.SystemSettings.ReplaceOne(
                item => item.Id == setting.Id,
                setting);

            _logger.LogWarning(
                "Repaired AI provider credential protection in DB for providers: {Providers}.",
                string.Join(", ", providersNeedingRepair.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to auto-repair AI provider credentials after DB decryption fallback.");
        }
    }
}
