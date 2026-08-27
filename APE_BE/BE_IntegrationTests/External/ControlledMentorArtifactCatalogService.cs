using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;

namespace BE_IntegrationTests.External;

public sealed class ControlledMentorArtifactCatalogService : IAIArtifactCatalogService
{
    public Task<AIPromptTemplateDto?> GetPromptAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.Equals(key, "code_mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<AIPromptTemplateDto?>(new AIPromptTemplateDto
            {
                Key = "code_mentor",
                Version = "integration-test",
                Source = "BE_IntegrationTests",
                ArtifactType = "Prompt",
                SystemPrompt = "You are a deterministic code mentor.",
                UserPrompt = "{{problem}}\n\n{{code}}\n\n{{mentor_policy_json}}\n\n{{mentor_rubric_json}}"
            });
        }

        if (string.Equals(key, "question_generation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "question_review", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "question_generation_repair", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<AIPromptTemplateDto?>(new AIPromptTemplateDto
            {
                Key = key,
                Version = "integration-test",
                Source = "BE_IntegrationTests",
                ArtifactType = "Prompt",
                SystemPrompt = $"Controlled prompt for {key}.",
                UserPrompt = "{{context_text}}\n{{question_type}}\n{{difficulty}}\n{{topic_tags}}"
            });
        }

        return Task.FromResult<AIPromptTemplateDto?>(null);
    }

    public Task<AIJsonArtifactDto?> GetArtifactAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var json = "{}";

        if (string.Equals(fileName, "code-mentor-policy.json", StringComparison.OrdinalIgnoreCase))
        {
            json = """{"policy":"controlled"}""";
        }
        else if (string.Equals(fileName, Path.Combine("ai-rubrics", "CODE_MENTOR.json"), StringComparison.OrdinalIgnoreCase))
        {
            json = """{"rubric":"controlled"}""";
        }
        else if (string.Equals(fileName, "question-generation-review-policy.json", StringComparison.OrdinalIgnoreCase))
        {
            json = """
            {
              "minimum_review_score": 0.8,
              "max_revision_attempts": 2,
              "require_grounding": true
            }
            """;
        }
        else if (fileName.Contains("ai-rubrics", StringComparison.OrdinalIgnoreCase) &&
                 (fileName.EndsWith("_FE.json", StringComparison.OrdinalIgnoreCase) ||
                  fileName.EndsWith("_PE.json", StringComparison.OrdinalIgnoreCase)))
        {
            json = """
            {
              "rubric": "controlled-question-generation",
              "version": "integration-test"
            }
            """;
        }
        else
        {
            return Task.FromResult<AIJsonArtifactDto?>(null);
        }

        return Task.FromResult<AIJsonArtifactDto?>(new AIJsonArtifactDto
        {
            ArtifactKey = fileName,
            ArtifactType = "Json",
            Version = "integration-test",
            Source = "BE_IntegrationTests",
            Content = JsonDocument.Parse(json).RootElement.Clone()
        });
    }

    public async Task<JsonElement?> GetPolicyAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var artifact = await GetArtifactAsync(fileName, cancellationToken);
        return artifact?.Content;
    }
}
