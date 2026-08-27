using Application.Interfaces;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Data;

public static class AIAgentBootstrapSeeder
{
    private const string ProviderCredentialSettingName = "AI_PROVIDER_CREDENTIALS";

    public static async Task SeedAsync(
        DbContext db,
        IConfiguration configuration,
        IAISecretProtector secretProtector,
        CancellationToken cancellationToken = default)
    {
        await SeedProviderCredentialsAsync(db, configuration, secretProtector, cancellationToken);
        await SeedAgentsAsync(db, cancellationToken);
    }

    private static async Task SeedProviderCredentialsAsync(
        DbContext db,
        IConfiguration configuration,
        IAISecretProtector secretProtector,
        CancellationToken cancellationToken)
    {
        var updatedBy = await ResolveUpdatedByAsync(db, cancellationToken);
        var filter = Builders<SystemSetting>.Filter.Eq(item => item.SettingName, ProviderCredentialSettingName);
        var existing = await db.SystemSettings.Find(filter).FirstOrDefaultAsync(cancellationToken);

        var settingData = new BsonDocument();
        AppendProvider(settingData, "OpenAI", configuration, secretProtector);
        AppendProvider(settingData, "DeepSeek", configuration, secretProtector);
        AppendProvider(settingData, "Gemini", configuration, secretProtector);
        AppendProvider(settingData, "Cohere", configuration, secretProtector);

        if (existing is null)
        {
            existing = new SystemSetting
            {
                Id = "SETTING_AI_PROVIDER_CREDENTIALS",
                SettingName = ProviderCredentialSettingName,
                SettingData = settingData,
                UpdatedBy = updatedBy,
                LastUpdated = DateTime.UtcNow
            };

            await db.SystemSettings.InsertOneAsync(existing, cancellationToken: cancellationToken);
            return;
        }

        existing.SettingData = settingData;
        existing.UpdatedBy = updatedBy;
        existing.LastUpdated = DateTime.UtcNow;
        await db.SystemSettings.ReplaceOneAsync(item => item.Id == existing.Id, existing, cancellationToken: cancellationToken);
    }

    private static void AppendProvider(
        BsonDocument root,
        string providerName,
        IConfiguration configuration,
        IAISecretProtector secretProtector)
    {
        var prefix = $"AI:Providers:{providerName}:";
        var enabled = configuration[$"{prefix}Enabled"];
        var apiKey = configuration[$"{prefix}ApiKey"];
        var baseUrl = configuration[$"{prefix}BaseUrl"];
        var apiVersion = configuration[$"{prefix}ApiVersion"];
        var timeoutSeconds = configuration[$"{prefix}TimeoutSeconds"];

        if (string.IsNullOrWhiteSpace(enabled) &&
            string.IsNullOrWhiteSpace(apiKey) &&
            string.IsNullOrWhiteSpace(baseUrl) &&
            string.IsNullOrWhiteSpace(apiVersion) &&
            string.IsNullOrWhiteSpace(timeoutSeconds))
        {
            return;
        }

        root[providerName] = new BsonDocument
        {
            ["Enabled"] = !string.IsNullOrWhiteSpace(enabled) && bool.TryParse(enabled, out var isEnabled) ? isEnabled : true,
            ["ApiKey"] = secretProtector.Protect(apiKey ?? string.Empty),
            ["BaseUrl"] = baseUrl ?? string.Empty,
            ["ApiVersion"] = apiVersion ?? string.Empty,
            ["TimeoutSeconds"] = int.TryParse(timeoutSeconds, out var timeout) ? timeout : 180,
            ["Headers"] = new BsonDocument()
        };
    }

    private static async Task SeedAgentsAsync(DbContext db, CancellationToken cancellationToken)
    {
        var updatedBy = await ResolveUpdatedByAsync(db, cancellationToken);
        var now = DateTime.UtcNow;
        var agents = new[]
        {
            BuildAgent(
                AIAgentCatalog.GatekeeperAgentId,
                AIAgentRole.Gatekeeper,
                AIProvider.DeepSeek,
                "deepseek-v4-flash",
                AIProvider.OpenAI,
                "gpt-4o-mini",
                1500,
                0.1,
                "Priority order: deepseek-v4-flash -> gpt-4o-mini"),
            BuildAgent(
                AIAgentCatalog.ExtractionAgentId,
                AIAgentRole.Extraction,
                AIProvider.OpenAI,
                "gpt-4o",
                AIProvider.OpenAI,
                "gpt-4o-mini",
                2500,
                0.2,
                "Priority order: gpt-4o -> gpt-4o-mini"),
            BuildAgent(
                AIAgentCatalog.EmbeddingAgentId,
                AIAgentRole.Embedding,
                AIProvider.Cohere,
                "embed-multilingual-v3.0",
                null,
                null,
                null,
                null,
                "Priority order: embed-multilingual-v3.0"),
            BuildAgent(
                AIAgentCatalog.TaggingAgentId,
                AIAgentRole.Tagging,
                AIProvider.DeepSeek,
                "deepseek-v4-flash",
                AIProvider.OpenAI,
                "gpt-4o-mini",
                600,
                0.1,
                "Priority order: deepseek-v4-flash -> gpt-4o-mini"),
            BuildAgent(
                AIAgentCatalog.QuestionGeneratorAgentId,
                AIAgentRole.QuestionGenerator,
                AIProvider.OpenAI,
                "gpt-5.4",
                AIProvider.DeepSeek,
                "deepseek-v4-pro",
                4000,
                0.4,
                "Priority order: gpt-5.4 -> deepseek-v4-pro"),
            BuildAgent(
                AIAgentCatalog.ReviewerAgentId,
                AIAgentRole.Reviewer,
                AIProvider.OpenAI,
                "gpt-5.4-mini",
                AIProvider.DeepSeek,
                "deepseek-v4-flash",
                2500,
                0.2,
                "Priority order: gpt-5.4-mini -> deepseek-v4-flash"),
            BuildAgent(
                AIAgentCatalog.MentorAgentId,
                AIAgentRole.Mentor,
                AIProvider.OpenAI,
                "gpt-5.4",
                AIProvider.DeepSeek,
                "deepseek-v4-pro",
                4000,
                0.3,
                "Priority order: gpt-5.4 -> deepseek-v4-pro")
        };

        foreach (var agent in agents)
        {
            agent.UpdatedBy = updatedBy;
            agent.UpdatedAt = now;
            await db.AIAgents.ReplaceOneAsync(
                item => item.Id == agent.Id,
                agent,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }
    }

    private static AIAgent BuildAgent(
        string id,
        AIAgentRole role,
        AIProvider provider,
        string modelName,
        AIProvider? fallbackProvider,
        string? fallbackModelName,
        int? maxTokens,
        double? temperature,
        string notes)
    {
        return new AIAgent
        {
            Id = id,
            AgentRole = role,
            Provider = provider,
            ModelName = modelName,
            MaxTokens = maxTokens,
            Temperature = temperature,
            CreditCost = 0,
            IsEnabled = true,
            FallbackProvider = fallbackProvider,
            FallbackModelName = fallbackModelName,
            Notes = notes
        };
    }

    private static async Task<string> ResolveUpdatedByAsync(DbContext db, CancellationToken cancellationToken)
    {
        var admin = await db.Users.Find(item => item.Email == "admin@fpt.edu.vn").FirstOrDefaultAsync(cancellationToken);
        if (admin is not null && ObjectId.TryParse(admin.Id, out _))
        {
            return admin.Id;
        }

        return ObjectId.GenerateNewId().ToString();
    }
}
