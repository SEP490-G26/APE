namespace Ape.AiModule.Application.DTOs.AI;

public sealed record ProviderConnectionSummary(
    string Provider,
    bool Enabled,
    bool HasApiKey,
    string BaseUrl,
    string ApiVersion,
    string? DefaultTextModel,
    string? DefaultVisionModel,
    string? DefaultEmbeddingModel);

public sealed record ProviderModelInfo(
    string Id,
    string Name,
    IReadOnlyList<string> Capabilities);

public sealed record ProviderCatalogResult(
    ProviderConnectionSummary Summary,
    IReadOnlyList<ProviderModelInfo> Models);

public sealed record ProviderPingResult(
    string Provider,
    bool Success,
    int? StatusCode,
    string Message);
