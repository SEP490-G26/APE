using Application.DTOs;
using Application.Interfaces;

namespace Infrastructure.AI;

public class DisabledAITextClient : IAITextClient
{
    public string ProviderName => "Disabled";
    public bool IsConfigured => false;

    public Task<AITextResponse> GenerateAsync(AITextRequest request, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(request.Images is { Count: > 0 }
            ? "No AI multimodal text provider is configured yet."
            : "No AI text provider is configured yet.");
    }
}
