namespace Ape.AiModule.Domain.Entities.AI;

public sealed record PromptTemplate(
    string Key,
    string Version,
    string Description,
    string SystemPrompt,
    string UserPrompt,
    bool IsActive,
    DateTimeOffset UpdatedAt);
