using Application.DTOs;
using Application.Interfaces;
using Application.Options;

namespace BE_IntegrationTests.External;

public sealed class ControlledMentorFeatureRoutingService : IAIFeatureRoutingService
{
    public AIResolvedExecutionOptions ResolveText(string featureName, AIRuntimeOverride? runtimeOverride = null)
    {
        return new AIResolvedExecutionOptions
        {
            FeatureName = featureName,
            Provider = "ControlledAI",
            Model = "mentor-v1",
            Temperature = 0,
            MaxTokens = 512,
            UsedFrontendOverride = false,
            FallbackProvider = null,
            FallbackModel = null
        };
    }

    public AIResolvedExecutionOptions ResolveEmbedding(string featureName, AIRuntimeOverride? runtimeOverride = null)
    {
        return new AIResolvedExecutionOptions
        {
            FeatureName = featureName,
            Provider = "ControlledAI",
            Model = "embedding-v1",
            UsedFrontendOverride = false
        };
    }
}
