using Application.DTOs;

namespace Application.Interfaces;

public interface IAIProviderModelCatalogService
{
    Task<List<AIProviderModelOptionDto>> ListModelsAsync(string providerName, CancellationToken cancellationToken = default);
}
