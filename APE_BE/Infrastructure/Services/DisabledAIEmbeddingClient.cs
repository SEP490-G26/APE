using Application.DTOs;
using Application.Interfaces;

namespace Infrastructure.AI;

public class DisabledAIEmbeddingClient : IAIEmbeddingClient
{
    public string ProviderName => "Disabled";
    public bool IsConfigured => false;

    public Task<AIEmbeddingResponse> CreateEmbeddingsAsync(AIEmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("No AI embedding provider is configured yet.");
    }
}
