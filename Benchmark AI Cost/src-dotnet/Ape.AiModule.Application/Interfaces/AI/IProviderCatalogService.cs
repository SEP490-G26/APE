using Ape.AiModule.Application.DTOs.AI;

namespace Ape.AiModule.Application.Interfaces.AI;

public interface IProviderCatalogService
{
    Task<IReadOnlyList<ProviderConnectionSummary>> GetProvidersAsync(CancellationToken cancellationToken);
    Task<ProviderCatalogResult> GetProviderCatalogAsync(string provider, CancellationToken cancellationToken);
    Task<ProviderPingResult> PingProviderAsync(string provider, CancellationToken cancellationToken);
}
