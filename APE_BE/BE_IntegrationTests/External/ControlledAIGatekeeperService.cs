using Application.DTOs;
using Application.Interfaces;

namespace BE_IntegrationTests.External;

public sealed class ControlledAIGatekeeperService : IAIGatekeeperService
{
    public Task<GatekeeperResult> ValidateAsync(string content, GatekeeperRequestContext? context = null, CancellationToken cancellationToken = default)
    {
        var primaryDomain = !string.IsNullOrWhiteSpace(context?.SubjectHint)
            ? context.SubjectHint!
            : "PRN212";

        return Task.FromResult(new GatekeeperResult
        {
            IsSupported = true,
            PrimaryDomain = primaryDomain,
            Reason = "Controlled integration approval",
            Verdict = "supported",
            MatchedSubjects = [primaryDomain],
            DetectedTopics = ["queues", "stacks"],
            Confidence = 0.99,
            Provider = "ControlledAI",
            Model = "gatekeeper-v1",
            ConfiguredModel = "gatekeeper-v1",
            EffectiveModel = "gatekeeper-v1",
            UsageSource = "controlled",
            CostSource = "controlled",
            InputTokens = 32,
            OutputTokens = 8,
            TotalTokens = 40,
            CostUsd = 0.001m
        });
    }
}
