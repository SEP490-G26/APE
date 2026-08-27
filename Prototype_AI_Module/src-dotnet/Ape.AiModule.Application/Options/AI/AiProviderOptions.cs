namespace Ape.AiModule.Application.Options.AI;

public sealed class AiProviderOptions
{
    public const string SectionName = "AIProviders";

    public OpenAiProviderOptions OpenAI { get; init; } = new();
    public GeminiProviderOptions Gemini { get; init; } = new();
    public CohereProviderOptions Cohere { get; init; } = new();
}

public abstract class ProviderOptionsBase
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string? DefaultTextModel { get; set; }
    public string? DefaultVisionModel { get; set; }
    public string? DefaultEmbeddingModel { get; set; }
    public int TimeoutSeconds { get; set; } = 100;
}

public sealed class OpenAiProviderOptions : ProviderOptionsBase;

public sealed class GeminiProviderOptions : ProviderOptionsBase;

public sealed class CohereProviderOptions : ProviderOptionsBase;
