using System.Text.Json;
using Application.DTOs;

namespace Application.Interfaces;

public interface IAIArtifactCatalogService
{
    Task<AIPromptTemplateDto?> GetPromptAsync(string key, CancellationToken cancellationToken = default);
    Task<AIJsonArtifactDto?> GetArtifactAsync(string fileName, CancellationToken cancellationToken = default);
    Task<JsonElement?> GetPolicyAsync(string fileName, CancellationToken cancellationToken = default);
}
