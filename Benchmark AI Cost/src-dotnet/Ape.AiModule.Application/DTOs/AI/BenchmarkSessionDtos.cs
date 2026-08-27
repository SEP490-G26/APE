namespace Ape.AiModule.Application.DTOs.AI;

public sealed record BenchmarkSessionSaveRequest(
    string Name,
    string? Notes,
    object? SummaryResults,
    object? BatchResults,
    object? MatrixResults);

public sealed record BenchmarkSessionSummary(
    string SessionId,
    string Name,
    DateTimeOffset CreatedAt,
    string FileName,
    int SummaryCount,
    int BatchCount,
    int MatrixCount,
    string? Notes);

public sealed record BenchmarkSessionRecord(
    BenchmarkSessionSummary Summary,
    object? SummaryResults,
    object? BatchResults,
    object? MatrixResults);
