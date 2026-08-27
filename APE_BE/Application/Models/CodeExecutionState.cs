namespace Application.Models;

public enum CodeExecutionState
{
    Queued = 0,
    Running = 1,
    ExecutionSucceeded = 2,
    CompilationFailed = 3,
    RuntimeFailed = 4,
    TimedOut = 5,
    MemoryLimitExceeded = 6,
    OutputLimitExceeded = 7,
    ProviderInternalError = 8,
    Unknown = 9
}
