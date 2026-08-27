namespace Application.Models;

public sealed record CodeExecutionResult(
    int CaseIndex,
    string ProviderToken,
    CodeExecutionState State,
    string? StandardOutput,
    string? StandardError,
    string? CompileOutput,
    string? Message,
    double? TimeSeconds,
    int? MemoryKb,
    string? ProviderStatusCode,
    string? ProviderStatusDescription)
{
    [Obsolete(
        "Use the provider-neutral constructor with CodeExecutionState and " +
        "explicit CaseIndex.")]
    public CodeExecutionResult(
        string Token,
        int StatusId,
        string? StatusDescription,
        string? StandardOutput,
        string? StandardError,
        string? CompileOutput,
        string? Message,
        double? TimeSeconds,
        int? MemoryKb)
        : this(
            CaseIndex: -1,
            ProviderToken: Token,
            State: MapLegacyStatus(StatusId, StatusDescription, StandardError, Message),
            StandardOutput: StandardOutput,
            StandardError: StandardError,
            CompileOutput: CompileOutput,
            Message: Message,
            TimeSeconds: TimeSeconds,
            MemoryKb: MemoryKb,
            ProviderStatusCode: StatusId.ToString(),
            ProviderStatusDescription: StatusDescription)
    {
    }

    public bool IsPending =>
        State is CodeExecutionState.Queued or CodeExecutionState.Running;

    public bool IsTerminal => !IsPending;

    private static CodeExecutionState MapLegacyStatus(
        int statusId,
        string? statusDescription,
        string? standardError,
        string? message)
    {
        return statusId switch
        {
            1 => CodeExecutionState.Queued,
            2 => CodeExecutionState.Running,
            3 or 4 => CodeExecutionState.ExecutionSucceeded,
            5 => CodeExecutionState.TimedOut,
            6 => CodeExecutionState.CompilationFailed,
            8 => CodeExecutionState.OutputLimitExceeded,
            13 => CodeExecutionState.ProviderInternalError,
            7 or 9 or 10 or 11 or 12 or 14 =>
                ResolveRuntimeLikeState(
                    statusDescription,
                    standardError,
                    message),
            _ => CodeExecutionState.Unknown
        };
    }

    private static CodeExecutionState ResolveRuntimeLikeState(
        string? statusDescription,
        string? standardError,
        string? message)
    {
        var diagnostic = string.Join(
            ' ',
            statusDescription,
            standardError,
            message);

        if (diagnostic.Contains(
                "memory",
                StringComparison.OrdinalIgnoreCase))
        {
            return CodeExecutionState.MemoryLimitExceeded;
        }

        return CodeExecutionState.RuntimeFailed;
    }
}
