using Domain.Exceptions;

namespace Application.Models;

public sealed record CodeExecutionCaseRequest
{
    public CodeExecutionCaseRequest(
        int caseIndex,
        string standardInput,
        int timeLimitMs,
        int memoryLimitKb)
    {
        if (caseIndex < 0)
        {
            throw new DomainRuleException(
                "CaseIndex cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(standardInput);

        if (timeLimitMs <= 0)
        {
            throw new DomainRuleException(
                "TimeLimitMs must be greater than zero.");
        }

        if (memoryLimitKb <= 0)
        {
            throw new DomainRuleException(
                "MemoryLimitKb must be greater than zero.");
        }

        CaseIndex = caseIndex;
        StandardInput = standardInput;
        TimeLimitMs = timeLimitMs;
        MemoryLimitKb = memoryLimitKb;
    }

    public int CaseIndex { get; }

    public string StandardInput { get; }

    public int TimeLimitMs { get; }

    public int MemoryLimitKb { get; }
}
