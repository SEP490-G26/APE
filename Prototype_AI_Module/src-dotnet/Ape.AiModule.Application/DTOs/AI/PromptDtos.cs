using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Application.DTOs.AI;

public sealed record PromptTemplateUpdateRequest(
    string Key,
    string Version,
    string Description,
    string SystemPrompt,
    string UserPrompt,
    bool IsActive);

public sealed record PromptRenderRequest(
    string Key,
    IReadOnlyDictionary<string, string> Variables);

public sealed record PromptRenderResult(
    PromptTemplate Template,
    string RenderedSystemPrompt,
    string RenderedUserPrompt);
