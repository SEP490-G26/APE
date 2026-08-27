namespace Ape.AiModule.Application.DTOs.AI;

public sealed record AIUsageLogPayload(
    string PipelineType,
    string Subject,
    string? QuestionType,
    string? Difficulty,
    string? Result,
    object Input,
    object Output);
