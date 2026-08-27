using Domain.Entities;
using Domain.Enums;

namespace Domain.Constants;

public static class AIAgentCatalog
{
    public const string GatekeeperAgentId = "687b1a010000000000000001";
    public const string ExtractionAgentId = "687b1a010000000000000002";
    public const string EmbeddingAgentId = "687b1a010000000000000003";
    public const string TaggingAgentId = "687b1a010000000000000004";
    public const string QuestionGeneratorAgentId = "687b1a010000000000000005";
    public const string ReviewerAgentId = "687b1a010000000000000006";
    public const string MentorAgentId = "687b1a010000000000000007";

    public static IReadOnlyList<AIAgent> CreateSeedAgents()
    {
        var now = DateTime.UtcNow;
        return new List<AIAgent>
        {
            new()
            {
                Id = GatekeeperAgentId,
                AgentRole = AIAgentRole.Gatekeeper,
                Provider = AIProvider.DeepSeek,
                ModelName = "deepseek-v4-flash",
                MaxTokens = 1500,
                Temperature = 0.1,
                CreditCost = 0,
                IsEnabled = true,
                FallbackProvider = AIProvider.OpenAI,
                FallbackModelName = "gpt-4o-mini",
                UpdatedAt = now,
                Notes = "Priority order: deepseek-v4-flash -> gpt-4o-mini"
            },
            new()
            {
                Id = ExtractionAgentId,
                AgentRole = AIAgentRole.Extraction,
                Provider = AIProvider.OpenAI,
                ModelName = "gpt-4o",
                MaxTokens = 2500,
                Temperature = 0.2,
                CreditCost = 0,
                IsEnabled = true,
                FallbackProvider = AIProvider.OpenAI,
                FallbackModelName = "gpt-4o-mini",
                UpdatedAt = now,
                Notes = "Priority order: gpt-4o -> gpt-4o-mini"
            },
            new()
            {
                Id = EmbeddingAgentId,
                AgentRole = AIAgentRole.Embedding,
                Provider = AIProvider.Cohere,
                ModelName = "embed-multilingual-v3.0",
                CreditCost = 0,
                IsEnabled = true,
                UpdatedAt = now,
                Notes = "Priority order: embed-multilingual-v3.0"
            },
            new()
            {
                Id = TaggingAgentId,
                AgentRole = AIAgentRole.Tagging,
                Provider = AIProvider.DeepSeek,
                ModelName = "deepseek-v4-flash",
                MaxTokens = 600,
                Temperature = 0.1,
                CreditCost = 0,
                IsEnabled = true,
                FallbackProvider = AIProvider.OpenAI,
                FallbackModelName = "gpt-4o-mini",
                UpdatedAt = now,
                Notes = "Priority order: deepseek-v4-flash -> gpt-4o-mini"
            },
            new()
            {
                Id = QuestionGeneratorAgentId,
                AgentRole = AIAgentRole.QuestionGenerator,
                Provider = AIProvider.OpenAI,
                ModelName = "gpt-5.4",
                MaxTokens = 4000,
                Temperature = 0.4,
                CreditCost = 0,
                IsEnabled = true,
                FallbackProvider = AIProvider.DeepSeek,
                FallbackModelName = "deepseek-v4-pro",
                UpdatedAt = now,
                Notes = "Priority order: gpt-5.4 -> deepseek-v4-pro"
            },
            new()
            {
                Id = ReviewerAgentId,
                AgentRole = AIAgentRole.Reviewer,
                Provider = AIProvider.OpenAI,
                ModelName = "gpt-5.4-mini",
                MaxTokens = 2500,
                Temperature = 0.2,
                CreditCost = 0,
                IsEnabled = true,
                FallbackProvider = AIProvider.DeepSeek,
                FallbackModelName = "deepseek-v4-flash",
                UpdatedAt = now,
                Notes = "Priority order: gpt-5.4-mini -> deepseek-v4-flash"
            },
            new()
            {
                Id = MentorAgentId,
                AgentRole = AIAgentRole.Mentor,
                Provider = AIProvider.OpenAI,
                ModelName = "gpt-5.4",
                MaxTokens = 4000,
                Temperature = 0.3,
                CreditCost = 0,
                IsEnabled = true,
                FallbackProvider = AIProvider.DeepSeek,
                FallbackModelName = "deepseek-v4-pro",
                UpdatedAt = now,
                Notes = "Priority order: gpt-5.4 -> deepseek-v4-pro"
            }
        };
    }
}
