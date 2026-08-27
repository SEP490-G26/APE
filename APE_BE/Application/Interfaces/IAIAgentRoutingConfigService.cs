using Domain.Entities;

namespace Application.Interfaces;

public interface IAIAgentRoutingConfigService
{
    Task<AIAgent?> GetActiveAgentAsync(string featureName, CancellationToken cancellationToken = default);
}
