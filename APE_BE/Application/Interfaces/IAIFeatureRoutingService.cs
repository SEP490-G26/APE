using Application.DTOs;

namespace Application.Interfaces;

public interface IAIFeatureRoutingService
{
    AIResolvedExecutionOptions ResolveText(string featureName, AIRuntimeOverride? runtimeOverride = null);
    AIResolvedExecutionOptions ResolveEmbedding(string featureName, AIRuntimeOverride? runtimeOverride = null);
}
