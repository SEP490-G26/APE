using Application.DTOs;

namespace Application.Interfaces;

public interface IAIAgentAdminService
{
    Task<List<AIAgentAdminDto>> ListAgentsAsync(CancellationToken cancellationToken = default);
    Task<AIAgentAdminDto?> GetAgentAsync(string agentRole, CancellationToken cancellationToken = default);
    Task<AIAgentAdminDto> UpsertAgentAsync(string agentRole, UpsertAIAgentRequestDto request, string updatedBy, CancellationToken cancellationToken = default);
}
