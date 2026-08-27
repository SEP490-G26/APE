using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.AI;

public class AIAgentAdminService : IAIAgentAdminService
{
    private readonly DbContext _dbContext;
    private readonly IAIProviderSettingsResolver _providerSettingsResolver;

    public AIAgentAdminService(DbContext dbContext, IAIProviderSettingsResolver providerSettingsResolver)
    {
        _dbContext = dbContext;
        _providerSettingsResolver = providerSettingsResolver;
    }

    public async Task<List<AIAgentAdminDto>> ListAgentsAsync(CancellationToken cancellationToken = default)
    {
        var agents = await _dbContext.AIAgents.Find(FilterDefinition<AIAgent>.Empty).ToListAsync(cancellationToken);
        return agents
            .OrderBy(item => item.AgentRole.ToString())
            .Select(Map)
            .ToList();
    }

    public async Task<AIAgentAdminDto?> GetAgentAsync(string agentRole, CancellationToken cancellationToken = default)
    {
        var role = ParseRole(agentRole);
        var agent = await _dbContext.AIAgents.Find(item => item.AgentRole == role).FirstOrDefaultAsync(cancellationToken);
        if (agent is null)
        {
            return null;
        }

        return Map(agent);
    }

    public async Task<AIAgentAdminDto> UpsertAgentAsync(string agentRole, UpsertAIAgentRequestDto request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var role = ParseRole(agentRole);
        var provider = ParseProvider(request.Provider);

        var agent = await _dbContext.AIAgents.Find(item => item.AgentRole == role).FirstOrDefaultAsync(cancellationToken)
            ?? new AIAgent { AgentRole = role };

        agent.Provider = provider;
        agent.ModelName = request.ModelName.Trim();
        agent.MaxTokens = request.MaxTokens;
        agent.Temperature = request.Temperature;
        agent.CreditCost = request.CreditCost;
        agent.IsEnabled = request.IsEnabled;
        agent.FallbackProvider = string.IsNullOrWhiteSpace(request.FallbackProvider) ? null : ParseProvider(request.FallbackProvider);
        agent.FallbackModelName = string.IsNullOrWhiteSpace(request.FallbackModelName) ? null : request.FallbackModelName.Trim();
        agent.UpdatedBy = NormalizeObjectIdOrNull(updatedBy);
        agent.UpdatedAt = DateTime.UtcNow;
        agent.Notes = request.Notes;

        if (string.IsNullOrWhiteSpace(agent.Id) || !await _dbContext.AIAgents.Find(item => item.Id == agent.Id).AnyAsync(cancellationToken))
        {
            await _dbContext.AIAgents.InsertOneAsync(agent, cancellationToken: cancellationToken);
        }
        else
        {
            await _dbContext.AIAgents.ReplaceOneAsync(item => item.Id == agent.Id, agent, cancellationToken: cancellationToken);
        }

        _providerSettingsResolver.Reload();
        return Map(agent);
    }

    private static AIAgentAdminDto Map(AIAgent agent)
    {
        return new AIAgentAdminDto
        {
            Id = agent.Id,
            AgentRole = agent.AgentRole.ToString(),
            Provider = agent.Provider.ToString(),
            ModelName = agent.ModelName,
            MaxTokens = agent.MaxTokens,
            Temperature = agent.Temperature,
            CreditCost = agent.CreditCost,
            IsEnabled = agent.IsEnabled,
            FallbackProvider = agent.FallbackProvider?.ToString(),
            FallbackModelName = agent.FallbackModelName,
            HasFallbackConfigured = agent.FallbackProvider.HasValue && !string.IsNullOrWhiteSpace(agent.FallbackModelName),
            UpdatedBy = agent.UpdatedBy,
            UpdatedAt = agent.UpdatedAt,
            Notes = agent.Notes
        };
    }

    private static AIAgentRole ParseRole(string role)
    {
        return Enum.TryParse<AIAgentRole>(role, true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Unknown AI agent role '{role}'.");
    }

    private static AIProvider ParseProvider(string? provider)
    {
        return Enum.TryParse<AIProvider>(provider, true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Unknown AI provider '{provider}'.");
    }

    private static string? NormalizeObjectIdOrNull(string? value)
    {
        return MongoDB.Bson.ObjectId.TryParse(value, out _) ? value : null;
    }
}
