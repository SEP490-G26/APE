using Application.DTOs;
using Application.Interfaces;

namespace BE_IntegrationTests.External;

public sealed class ControlledAIProviderModelCatalogService : IAIProviderModelCatalogService
{
    private static readonly Dictionary<string, List<AIProviderModelOptionDto>> ModelsByProvider =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["OpenAI"] =
            [
                new AIProviderModelOptionDto { Id = "gpt-4.1-mini", Label = "gpt-4.1-mini" },
                new AIProviderModelOptionDto { Id = "gpt-4o-mini", Label = "gpt-4o-mini" }
            ],
            ["DeepSeek"] =
            [
                new AIProviderModelOptionDto { Id = "deepseek-chat", Label = "deepseek-chat" }
            ],
            ["Gemini"] =
            [
                new AIProviderModelOptionDto { Id = "gemini-2.0-flash", Label = "gemini-2.0-flash" }
            ],
            ["Cohere"] =
            [
                new AIProviderModelOptionDto { Id = "command-r-plus", Label = "command-r-plus" }
            ]
        };

    public Task<List<AIProviderModelOptionDto>> ListModelsAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new InvalidOperationException("Provider is required.");
        }

        if (!ModelsByProvider.TryGetValue(providerName.Trim(), out var models))
        {
            throw new InvalidOperationException($"Provider '{providerName}' is not supported for model discovery.");
        }

        return Task.FromResult(models.Select(item => new AIProviderModelOptionDto
        {
            Id = item.Id,
            Label = item.Label
        }).ToList());
    }
}
