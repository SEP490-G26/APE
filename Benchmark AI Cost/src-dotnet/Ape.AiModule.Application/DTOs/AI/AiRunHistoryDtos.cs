using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Application.DTOs.AI;

public sealed record AiRunHistorySummary(
    string RunId,
    string FunctionName,
    string RouteKey,
    DateTimeOffset RecordedAt,
    string RequestType,
    string ResponseType,
    string? Status,
    string? ModelSummary,
    NormalizedModelFields? ModelFields,
    string? UsageSource,
    NormalizedError? Error,
    TokenCostBreakdown? Totals,
    string FileName);

public sealed record AiRunHistoryRecord(
    AiRunHistorySummary Summary,
    object Request,
    object Response);
