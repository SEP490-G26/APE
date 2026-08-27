namespace Application.Options;

public class AIOptions
{
    public bool Enabled { get; set; } = true;
    public bool AllowHeuristicFallbacks { get; set; } = true;
    public Dictionary<string, AIProviderOptions> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, AIFeatureOptions> Features { get; set; } = CreateDefaultFeatures();

    public AIFeatureOptions GetFeature(string featureName)
    {
        return Features.TryGetValue(featureName, out var feature)
            ? feature
            : new AIFeatureOptions();
    }

    public AIProviderOptions GetProvider(string providerName)
    {
        return Providers.TryGetValue(providerName, out var provider)
            ? provider
            : new AIProviderOptions();
    }

    private static Dictionary<string, AIFeatureOptions> CreateDefaultFeatures()
    {
        return new Dictionary<string, AIFeatureOptions>(StringComparer.OrdinalIgnoreCase)
        {
            [AIFeatureNames.Gatekeeper] = new() { Provider = "OpenAI", Model = "gpt-4o", AllowFrontendOverride = true, IsEnabled = true },
            [AIFeatureNames.ExtractedContent] = new() { Provider = "OpenAI", Model = "gpt-4o", AllowFrontendOverride = true, IsEnabled = true },
            [AIFeatureNames.ExtractedStructure] = new() { Provider = "DeepSeek", Model = "deepseek-v4-flash", AllowFrontendOverride = true, IsEnabled = true },
            [AIFeatureNames.QuestionGeneration] = new() { Provider = "OpenAI", Model = "gpt-5.4", AllowFrontendOverride = true, IsEnabled = true },
            [AIFeatureNames.QuestionReview] = new() { Provider = "OpenAI", Model = "gpt-5.4-mini", AllowFrontendOverride = true, IsEnabled = true },
            [AIFeatureNames.CodeMentor] = new() { Provider = "OpenAI", Model = "gpt-5.4", AllowFrontendOverride = true, IsEnabled = true },
            [AIFeatureNames.Embedding] = new() { Provider = "Cohere", Model = "embed-multilingual-v3.0", AllowFrontendOverride = false, IsEnabled = true },
            [AIFeatureNames.AutoTagging] = new() { Provider = "Cohere", Model = "command-r7b-12-2024", AllowFrontendOverride = false, IsEnabled = true }
        };
    }
}

public class AIProviderOptions
{
    public bool Enabled { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 180;
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class AIFeatureOptions
{
    public bool IsEnabled { get; set; } = true;
    public string DisplayName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public bool AllowFrontendOverride { get; set; }
    public List<string> AllowedProviders { get; set; } = new();
    public List<string> AllowedModels { get; set; } = new();
}

public static class AIFeatureNames
{
    public const string Gatekeeper = "Gatekeeper";
    public const string ExtractedContent = "ExtractedContent";
    public const string ExtractedStructure = "ExtractedStructure";
    public const string Embedding = "Embedding";
    public const string AutoTagging = "AutoTagging";
    public const string QuestionGeneration = "QuestionGeneration";
    public const string QuestionReview = "QuestionReview";
    public const string CodeMentor = "CodeMentor";
}
