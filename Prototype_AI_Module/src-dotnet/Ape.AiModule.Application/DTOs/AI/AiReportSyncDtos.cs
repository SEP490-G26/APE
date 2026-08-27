namespace Ape.AiModule.Application.DTOs.AI;

public sealed record AiReportSyncResult(
    bool Success,
    string Trigger,
    string Message,
    string ReportRoot,
    string WorkbookPath,
    DateTimeOffset SyncedAt,
    string? StdOut = null,
    string? StdErr = null);
