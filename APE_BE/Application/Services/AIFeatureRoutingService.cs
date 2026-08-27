using Application.DTOs;
using Application.Interfaces;
using Application.Options;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class AIFeatureRoutingService : IAIFeatureRoutingService
{
    private readonly AIOptions _options;
    private readonly IAIAgentRoutingConfigService? _agentRoutingConfigService;

    public AIFeatureRoutingService(IOptions<AIOptions> options)
    {
        _options = options.Value;
    }

    public AIFeatureRoutingService(IOptions<AIOptions> options, IAIAgentRoutingConfigService agentRoutingConfigService)
    {
        _options = options.Value;
        _agentRoutingConfigService = agentRoutingConfigService;
    }

    public AIResolvedExecutionOptions ResolveText(string featureName, AIRuntimeOverride? runtimeOverride = null)
    {
        return Resolve(featureName, runtimeOverride);
    }

    public AIResolvedExecutionOptions ResolveEmbedding(string featureName, AIRuntimeOverride? runtimeOverride = null)
    {
        return Resolve(featureName, runtimeOverride);
    }

    private AIResolvedExecutionOptions Resolve(string featureName, AIRuntimeOverride? runtimeOverride)
    {
        var feature = _options.GetFeature(featureName);
        if (!feature.IsEnabled)
        {
            throw new InvalidOperationException($"AI feature '{featureName}' is disabled.");
        }

        var usedOverride = false;
        var provider = feature.Provider;
        var model = feature.Model;
        var temperature = feature.Temperature;
        var maxTokens = feature.MaxTokens;

        var dbAgent = _agentRoutingConfigService?.GetActiveAgentAsync(featureName).GetAwaiter().GetResult();
        if (dbAgent is not null)
        {
            if (!dbAgent.IsEnabled)
            {
                throw new InvalidOperationException($"AI agent for feature '{featureName}' is disabled.");
            }

            provider = dbAgent.Provider.ToString();
            model = dbAgent.ModelName;
            temperature = dbAgent.Temperature ?? temperature;
            maxTokens = dbAgent.MaxTokens ?? maxTokens;
        }

        if (runtimeOverride is not null && feature.AllowFrontendOverride)
        {
            if (!string.IsNullOrWhiteSpace(runtimeOverride.Provider) && IsAllowedProvider(feature, runtimeOverride.Provider))
            {
                provider = runtimeOverride.Provider;
                usedOverride = true;
            }

            if (!string.IsNullOrWhiteSpace(runtimeOverride.Model) && IsAllowedModel(feature, runtimeOverride.Model))
            {
                model = runtimeOverride.Model;
                usedOverride = true;
            }

            if (runtimeOverride.Temperature.HasValue)
            {
                temperature = runtimeOverride.Temperature;
                usedOverride = true;
            }

            if (runtimeOverride.MaxTokens.HasValue)
            {
                maxTokens = runtimeOverride.MaxTokens;
                usedOverride = true;
            }
        }

        return new AIResolvedExecutionOptions
        {
            FeatureName = featureName,
            Provider = provider,
            Model = model,
            Temperature = temperature,
            MaxTokens = maxTokens,
            UsedFrontendOverride = usedOverride,
            FallbackProvider = dbAgent?.FallbackProvider?.ToString(),
            FallbackModel = dbAgent?.FallbackModelName
        };
    }

    private static bool IsAllowedProvider(AIFeatureOptions feature, string provider)
    {
        return feature.AllowedProviders.Count == 0 ||
               feature.AllowedProviders.Contains(provider, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAllowedModel(AIFeatureOptions feature, string model)
    {
        return feature.AllowedModels.Count == 0 ||
               feature.AllowedModels.Contains(model, StringComparer.OrdinalIgnoreCase);
    }
}
