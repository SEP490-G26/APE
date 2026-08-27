using Application.DTOs;

namespace Application.Interfaces;

public interface IAIProviderCredentialAdminService
{
    Task<AIProviderCredentialsConfigDto> GetAsync(CancellationToken cancellationToken = default);
    Task<AIProviderCredentialsConfigDto> UpsertAsync(AIProviderCredentialsUpsertRequestDto request, string updatedBy, CancellationToken cancellationToken = default);
    Task<List<AIProviderCredentialRecordDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<AIProviderCredentialRecordDto> CreateAsync(CreateAIProviderCredentialRequestDto request, string updatedBy, CancellationToken cancellationToken = default);
    Task<AIProviderCredentialRecordDto> UpdateAsync(string providerName, UpdateAIProviderCredentialRequestDto request, string updatedBy, CancellationToken cancellationToken = default);
    Task DeleteAsync(string providerName, string updatedBy, CancellationToken cancellationToken = default);
}
