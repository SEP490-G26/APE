using Application.DTOs;

namespace Application.Interfaces;

public interface IAITextClient
{
    string ProviderName { get; }
    bool IsConfigured { get; }
    Task<AITextResponse> GenerateAsync(AITextRequest request, CancellationToken cancellationToken = default);
}
