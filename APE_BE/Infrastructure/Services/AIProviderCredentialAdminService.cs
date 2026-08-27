using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace Infrastructure.AI;

public class AIProviderCredentialAdminService : IAIProviderCredentialAdminService
{
    private const string SettingName = "AI_PROVIDER_CREDENTIALS";
    private static readonly Regex ProviderNameRegex = new("^[A-Za-z][A-Za-z0-9_-]{1,63}$", RegexOptions.Compiled);
    private readonly DbContext _dbContext;
    private readonly IAIProviderSettingsResolver _providerSettingsResolver;
    private readonly IAISecretProtector _secretProtector;

    public AIProviderCredentialAdminService(
        DbContext dbContext,
        IAIProviderSettingsResolver providerSettingsResolver,
        IAISecretProtector secretProtector)
    {
        _dbContext = dbContext;
        _providerSettingsResolver = providerSettingsResolver;
        _secretProtector = secretProtector;
    }

    public async Task<AIProviderCredentialsConfigDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<SystemSetting>.Filter.Eq(item => item.SettingName, SettingName);
        var setting = await _dbContext.SystemSettings.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return Map(setting, _secretProtector);
    }

    public async Task<List<AIProviderCredentialRecordDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<SystemSetting>.Filter.Eq(item => item.SettingName, SettingName);
        var setting = await _dbContext.SystemSettings.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return MapRecords(setting, _secretProtector);
    }

    public async Task<AIProviderCredentialRecordDto> CreateAsync(CreateAIProviderCredentialRequestDto request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var providerName = NormalizeProviderName(request.Provider);
        ValidateProviderName(providerName);
        ValidateProviderPayload(request, providerName, requireApiKey: false);

        var filter = Builders<SystemSetting>.Filter.Eq(item => item.SettingName, SettingName);
        var existing = await _dbContext.SystemSettings.Find(filter).FirstOrDefaultAsync(cancellationToken);
        var root = CloneSettingData(existing?.SettingData);
        if (root.Contains(providerName))
        {
            throw new ValidationException($"Provider '{providerName}' already exists.");
        }

        AppendProvider(root, providerName, request, _secretProtector);
        var saved = await SaveSettingAsync(existing, root, updatedBy, cancellationToken);
        return FindRecord(saved, providerName, _secretProtector)
            ?? throw new InvalidOperationException($"Provider '{providerName}' could not be created.");
    }

    public async Task<AIProviderCredentialRecordDto> UpdateAsync(string providerName, UpdateAIProviderCredentialRequestDto request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var normalizedProviderName = NormalizeProviderName(providerName);
        ValidateProviderName(normalizedProviderName);
        ValidateProviderIdentity(normalizedProviderName, request.Provider);
        ValidateProviderPayload(request, normalizedProviderName, requireApiKey: false);

        var filter = Builders<SystemSetting>.Filter.Eq(item => item.SettingName, SettingName);
        var existing = await _dbContext.SystemSettings.Find(filter).FirstOrDefaultAsync(cancellationToken);
        var root = CloneSettingData(existing?.SettingData);
        if (!root.Contains(normalizedProviderName))
        {
            throw new ValidationException($"Provider '{normalizedProviderName}' not found.");
        }

        AppendProvider(root, normalizedProviderName, request, _secretProtector);
        var saved = await SaveSettingAsync(existing, root, updatedBy, cancellationToken);
        return FindRecord(saved, normalizedProviderName, _secretProtector)
            ?? throw new InvalidOperationException($"Provider '{normalizedProviderName}' could not be updated.");
    }

    public async Task DeleteAsync(string providerName, string updatedBy, CancellationToken cancellationToken = default)
    {
        var normalizedProviderName = NormalizeProviderName(providerName);
        ValidateProviderName(normalizedProviderName);

        var filter = Builders<SystemSetting>.Filter.Eq(item => item.SettingName, SettingName);
        var existing = await _dbContext.SystemSettings.Find(filter).FirstOrDefaultAsync(cancellationToken);
        var root = CloneSettingData(existing?.SettingData);
        if (!root.Contains(normalizedProviderName))
        {
            throw new ValidationException($"Provider '{normalizedProviderName}' not found.");
        }

        var dependentAgents = await _dbContext.AIAgents
            .Find(item =>
                item.Provider.ToString() == normalizedProviderName ||
                (item.FallbackProvider.HasValue && item.FallbackProvider.Value.ToString() == normalizedProviderName))
            .Project(item => item.AgentRole.ToString())
            .ToListAsync(cancellationToken);

        if (dependentAgents.Count > 0)
        {
            throw new ValidationException(
                $"Cannot delete provider '{normalizedProviderName}' because it is used by agent(s): {string.Join(", ", dependentAgents)}.");
        }

        root.Remove(normalizedProviderName);
        await SaveSettingAsync(existing, root, updatedBy, cancellationToken);
    }

    public async Task<AIProviderCredentialsConfigDto> UpsertAsync(AIProviderCredentialsUpsertRequestDto request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var filter = Builders<SystemSetting>.Filter.Eq(item => item.SettingName, SettingName);
        var existing = await _dbContext.SystemSettings.Find(filter).FirstOrDefaultAsync(cancellationToken);
        ValidateBatchRequest(request);
        var settingData = BuildSettingData(request, existing?.SettingData, _secretProtector);
        var saved = await SaveSettingAsync(existing, settingData, updatedBy, cancellationToken);
        return Map(saved, _secretProtector);
    }

    private static BsonDocument BuildSettingData(
        AIProviderCredentialsUpsertRequestDto request,
        object? existingSettingData,
        IAISecretProtector secretProtector)
    {
        var root = existingSettingData switch
        {
            BsonDocument existingDocument => existingDocument.DeepClone().AsBsonDocument,
            null => new BsonDocument(),
            _ => existingSettingData.ToBsonDocument()
        };

        AppendProvider(root, "OpenAI", request.OpenAI, secretProtector);
        AppendProvider(root, "DeepSeek", request.DeepSeek, secretProtector);
        AppendProvider(root, "Gemini", request.Gemini, secretProtector);
        AppendProvider(root, "Cohere", request.Cohere, secretProtector);
        return root;
    }

    private async Task<SystemSetting> SaveSettingAsync(
        SystemSetting? existing,
        BsonDocument settingData,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (existing is null)
        {
            existing = new SystemSetting
            {
                Id = ObjectId.GenerateNewId().ToString(),
                SettingName = SettingName,
                SettingData = settingData,
                UpdatedBy = NormalizeUpdatedBy(updatedBy),
                LastUpdated = now
            };

            await _dbContext.SystemSettings.InsertOneAsync(existing, cancellationToken: cancellationToken);
        }
        else
        {
            existing.SettingData = settingData;
            existing.UpdatedBy = NormalizeUpdatedBy(updatedBy);
            existing.LastUpdated = now;
            await _dbContext.SystemSettings.ReplaceOneAsync(
                Builders<SystemSetting>.Filter.Eq(item => item.Id, existing.Id),
                existing,
                cancellationToken: cancellationToken);
        }

        _providerSettingsResolver.Reload();
        return existing;
    }

    private static BsonDocument CloneSettingData(object? existingSettingData)
    {
        return existingSettingData switch
        {
            BsonDocument existingDocument => existingDocument.DeepClone().AsBsonDocument,
            null => new BsonDocument(),
            _ => existingSettingData.ToBsonDocument()
        };
    }

    private static void AppendProvider(
        BsonDocument root,
        string providerName,
        AIProviderCredentialInputDto? provider,
        IAISecretProtector secretProtector)
    {
        if (provider is null)
        {
            return;
        }

        var existingProvider = root.TryGetValue(providerName, out var existingValue) && existingValue is BsonDocument existingProviderDoc
            ? existingProviderDoc
            : new BsonDocument();
        var headers = new BsonDocument();
        foreach (var header in provider.Headers)
        {
            headers[header.Key] = header.Value ?? string.Empty;
        }

        var protectedApiKey = existingProvider.TryGetValue("ApiKey", out var currentApiKeyValue)
            ? currentApiKeyValue.AsString
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            protectedApiKey = secretProtector.Protect(provider.ApiKey);
        }

        root[providerName] = new BsonDocument
        {
            ["Enabled"] = provider.Enabled,
            ["ApiKey"] = protectedApiKey,
            ["BaseUrl"] = provider.BaseUrl ?? string.Empty,
            ["ApiVersion"] = provider.ApiVersion ?? string.Empty,
            ["TimeoutSeconds"] = provider.TimeoutSeconds ?? 0,
            ["Headers"] = headers
        };
    }

    private static AIProviderCredentialsConfigDto Map(SystemSetting? setting, IAISecretProtector secretProtector)
    {
        if (setting?.SettingData is null)
        {
            return new AIProviderCredentialsConfigDto();
        }

        BsonDocument bson;
        try
        {
            bson = setting.SettingData switch
            {
                BsonDocument doc => doc,
                _ => setting.SettingData.ToBsonDocument()
            };
        }
        catch
        {
            return new AIProviderCredentialsConfigDto
            {
                LastUpdated = setting.LastUpdated,
                UpdatedBy = setting.UpdatedBy
            };
        }

        return new AIProviderCredentialsConfigDto
        {
            OpenAI = ReadProvider(bson, "OpenAI", secretProtector),
            DeepSeek = ReadProvider(bson, "DeepSeek", secretProtector),
            Gemini = ReadProvider(bson, "Gemini", secretProtector),
            Cohere = ReadProvider(bson, "Cohere", secretProtector),
            LastUpdated = setting.LastUpdated,
            UpdatedBy = setting.UpdatedBy
        };
    }

    private static List<AIProviderCredentialRecordDto> MapRecords(SystemSetting? setting, IAISecretProtector secretProtector)
    {
        if (setting?.SettingData is null)
        {
            return new List<AIProviderCredentialRecordDto>();
        }

        BsonDocument bson;
        try
        {
            bson = setting.SettingData switch
            {
                BsonDocument doc => doc,
                _ => setting.SettingData.ToBsonDocument()
            };
        }
        catch
        {
            return new List<AIProviderCredentialRecordDto>();
        }

        return bson.Elements
            .Where(item => item.Value is BsonDocument)
            .Select(item => BuildRecord(item.Name, item.Value.AsBsonDocument, setting, secretProtector))
            .OrderBy(item => item.Provider, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AIProviderCredentialRecordDto? FindRecord(SystemSetting? setting, string providerName, IAISecretProtector secretProtector)
    {
        return MapRecords(setting, secretProtector)
            .FirstOrDefault(item => string.Equals(item.Provider, providerName, StringComparison.OrdinalIgnoreCase));
    }

    private static AIProviderCredentialViewDto? ReadProvider(
        BsonDocument root,
        string providerName,
        IAISecretProtector secretProtector)
    {
        if (!root.TryGetValue(providerName, out var value) || value is not BsonDocument providerDoc)
        {
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (providerDoc.TryGetValue("Headers", out var headersValue) && headersValue is BsonDocument headersDoc)
        {
            foreach (var header in headersDoc.Elements)
            {
                headers[header.Name] = header.Value.AsString;
            }
        }

        var rawApiKey = providerDoc.TryGetValue("ApiKey", out var apiKeyValue) ? apiKeyValue.AsString : string.Empty;
        var visibleApiKey = TryUnprotect(rawApiKey, secretProtector);
        return new AIProviderCredentialViewDto
        {
            Enabled = providerDoc.TryGetValue("Enabled", out var enabledValue) && enabledValue.ToBoolean(),
            BaseUrl = providerDoc.TryGetValue("BaseUrl", out var baseUrlValue) ? baseUrlValue.AsString : string.Empty,
            ApiVersion = providerDoc.TryGetValue("ApiVersion", out var apiVersionValue) ? apiVersionValue.AsString : string.Empty,
            TimeoutSeconds = providerDoc.TryGetValue("TimeoutSeconds", out var timeoutValue) ? timeoutValue.ToInt32() : 0,
            Headers = headers,
            HasApiKey = !string.IsNullOrWhiteSpace(visibleApiKey),
            ApiKeyMasked = MaskApiKey(visibleApiKey)
        };
    }

    private static AIProviderCredentialRecordDto BuildRecord(
        string providerName,
        BsonDocument providerDoc,
        SystemSetting setting,
        IAISecretProtector secretProtector)
    {
        var view = ReadProvider(new BsonDocument(providerName, providerDoc), providerName, secretProtector) ?? new AIProviderCredentialViewDto();
        return new AIProviderCredentialRecordDto
        {
            Provider = providerName,
            Enabled = view.Enabled,
            HasApiKey = view.HasApiKey,
            ApiKeyMasked = view.ApiKeyMasked,
            BaseUrl = view.BaseUrl,
            ApiVersion = view.ApiVersion,
            TimeoutSeconds = view.TimeoutSeconds,
            Headers = view.Headers,
            UpdatedBy = setting.UpdatedBy,
            LastUpdated = setting.LastUpdated
        };
    }

    private static string TryUnprotect(string rawValue, IAISecretProtector secretProtector)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        try
        {
            return secretProtector.Unprotect(rawValue);
        }
        catch
        {
            return rawValue;
        }
    }

    private static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        if (apiKey.Length <= 8)
        {
            return new string('*', apiKey.Length);
        }

        return $"{apiKey[..4]}***{apiKey[^4..]}";
    }

    private static string NormalizeUpdatedBy(string updatedBy)
    {
        return ObjectId.TryParse(updatedBy, out _)
            ? updatedBy
            : ObjectId.GenerateNewId().ToString();
    }

    private static string NormalizeProviderName(string? providerName)
    {
        return providerName?.Trim() ?? string.Empty;
    }

    private static void ValidateBatchRequest(AIProviderCredentialsUpsertRequestDto request)
    {
        if (request.OpenAI is not null)
        {
            ValidateProviderPayload(request.OpenAI, "OpenAI", requireApiKey: false);
        }

        if (request.DeepSeek is not null)
        {
            ValidateProviderPayload(request.DeepSeek, "DeepSeek", requireApiKey: false);
        }

        if (request.Gemini is not null)
        {
            ValidateProviderPayload(request.Gemini, "Gemini", requireApiKey: false);
        }

        if (request.Cohere is not null)
        {
            ValidateProviderPayload(request.Cohere, "Cohere", requireApiKey: false);
        }
    }

    private static void ValidateProviderIdentity(string routeProviderName, string? bodyProviderName)
    {
        if (string.IsNullOrWhiteSpace(bodyProviderName))
        {
            return;
        }

        var normalizedBodyProviderName = NormalizeProviderName(bodyProviderName);
        if (!string.Equals(routeProviderName, normalizedBodyProviderName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Provider in request body must match provider being updated.");
        }
    }

    private static void ValidateProviderName(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ValidationException("Provider name is required.");
        }

        if (!ProviderNameRegex.IsMatch(providerName))
        {
            throw new ValidationException("Provider name must start with a letter and contain only letters, numbers, '_' or '-'. Length must be 2-64 characters.");
        }
    }

    private static void ValidateProviderPayload(AIProviderCredentialInputDto provider, string providerName, bool requireApiKey)
    {
        if (provider is null)
        {
            throw new ValidationException($"Provider payload for '{providerName}' is required.");
        }

        if (requireApiKey && string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            throw new ValidationException($"API key for provider '{providerName}' is required.");
        }

        if (!string.IsNullOrWhiteSpace(provider.ApiKey) && provider.ApiKey.Trim().Length > 512)
        {
            throw new ValidationException($"API key for provider '{providerName}' must be 512 characters or fewer.");
        }

        if (!string.IsNullOrWhiteSpace(provider.BaseUrl))
        {
            if (!Uri.TryCreate(provider.BaseUrl.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ValidationException($"Base URL for provider '{providerName}' must be a valid absolute HTTP/HTTPS URL.");
            }
        }

        if (!string.IsNullOrWhiteSpace(provider.ApiVersion))
        {
            var trimmedVersion = provider.ApiVersion.Trim();
            if (trimmedVersion.Length > 64)
            {
                throw new ValidationException($"API version for provider '{providerName}' must be 64 characters or fewer.");
            }

            if (trimmedVersion.Contains(' ') || trimmedVersion.Contains('\t'))
            {
                throw new ValidationException($"API version for provider '{providerName}' must not contain whitespace.");
            }
        }

        if (provider.TimeoutSeconds is not null &&
            (provider.TimeoutSeconds.Value < 1 || provider.TimeoutSeconds.Value > 600))
        {
            throw new ValidationException($"TimeoutSeconds for provider '{providerName}' must be between 1 and 600.");
        }

        if (provider.Headers.Count > 20)
        {
            throw new ValidationException($"Provider '{providerName}' supports at most 20 custom headers.");
        }

        foreach (var header in provider.Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
            {
                throw new ValidationException($"Provider '{providerName}' has a custom header with an empty name.");
            }

            if (header.Key.Length > 128)
            {
                throw new ValidationException($"Header name '{header.Key}' for provider '{providerName}' must be 128 characters or fewer.");
            }

            if (header.Key.Contains('\r') || header.Key.Contains('\n') || header.Key.Contains(':'))
            {
                throw new ValidationException($"Header name '{header.Key}' for provider '{providerName}' contains invalid characters.");
            }

            if ((header.Value ?? string.Empty).Length > 1024)
            {
                throw new ValidationException($"Header '{header.Key}' for provider '{providerName}' must be 1024 characters or fewer.");
            }

            if ((header.Value ?? string.Empty).Contains('\r') || (header.Value ?? string.Empty).Contains('\n'))
            {
                throw new ValidationException($"Header '{header.Key}' for provider '{providerName}' contains invalid line breaks.");
            }
        }
    }
}
