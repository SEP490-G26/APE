namespace Ape.AiModule.Domain.Entities.AI;

public sealed record DifficultyAlignmentResult(
    string RequestedDifficulty,
    string EstimatedDifficulty,
    bool IsAligned,
    IReadOnlyList<string> Signals,
    decimal Confidence);
