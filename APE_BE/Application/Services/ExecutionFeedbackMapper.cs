using Application.DTOs;
using Application.Exceptions;
using Application.Models;
using Domain.Enums;

namespace Application.Services;

internal static class ExecutionFeedbackMapper
{
    public static SubmissionVerdict ResolveVerdict(
        CodeExecutionResult result,
        string expectedOutput)
    {
        return result.State switch
        {
            CodeExecutionState.ExecutionSucceeded =>
                NormalizeOutput(result.StandardOutput) ==
                NormalizeOutput(expectedOutput)
                    ? SubmissionVerdict.Accepted
                    : SubmissionVerdict.WrongAnswer,

            CodeExecutionState.CompilationFailed =>
                SubmissionVerdict.CompilationError,

            CodeExecutionState.RuntimeFailed =>
                ResolveRuntimeVerdict(result),

            CodeExecutionState.TimedOut =>
                SubmissionVerdict.TimeLimitExceeded,

            CodeExecutionState.MemoryLimitExceeded =>
                SubmissionVerdict.MemoryLimitExceeded,

            CodeExecutionState.OutputLimitExceeded =>
                SubmissionVerdict.OutputLimitExceeded,

            CodeExecutionState.ProviderInternalError =>
                throw new CodeExecutionClientException(
                    "The provider returned an internal execution error.",
                    isTransient: true,
                    errorCategory: CodeExecutionErrorCategory.Provider),

            CodeExecutionState.Queued or
            CodeExecutionState.Running =>
                throw new CodeExecutionClientException(
                    "Provider results have not finished.",
                    isTransient: true,
                    errorCategory: CodeExecutionErrorCategory.Provider),

            CodeExecutionState.Unknown =>
                throw new CodeExecutionClientException(
                    "The provider returned an unknown execution state.",
                    isTransient: false,
                    errorCategory: CodeExecutionErrorCategory.Provider),

            _ => throw new CodeExecutionClientException(
                $"Unsupported execution state: {result.State}.",
                isTransient: false,
                errorCategory: CodeExecutionErrorCategory.Provider)
        };
    }

    public static SubmissionVerdict ResolveRuntimeVerdict(
        CodeExecutionResult result)
    {
        var diagnosticText = string.Join(
            ' ',
            result.ProviderStatusDescription,
            result.StandardError,
            result.Message);

        if (diagnosticText.Contains(
                "memory",
                StringComparison.OrdinalIgnoreCase))
        {
            return SubmissionVerdict.MemoryLimitExceeded;
        }

        return SubmissionVerdict.RuntimeError;
    }

    public static int ConvertRuntimeToMilliseconds(
        double? seconds)
    {
        if (!seconds.HasValue ||
            seconds.Value <= 0)
        {
            return 0;
        }

        var milliseconds = Math.Ceiling(
            seconds.Value * 1_000d);

        return milliseconds >= int.MaxValue
            ? int.MaxValue
            : (int)milliseconds;
    }

    public static string NormalizeOutput(
        string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n', '\r');
    }

    public static string MapVerdictStatus(
        SubmissionVerdict verdict)
    {
        return verdict switch
        {
            SubmissionVerdict.Accepted => "Accepted",
            SubmissionVerdict.WrongAnswer => "Wrong Answer",
            SubmissionVerdict.CompilationError => "Compilation Error",
            SubmissionVerdict.RuntimeError => "Runtime Error",
            SubmissionVerdict.TimeLimitExceeded => "Time Limit Exceeded",
            SubmissionVerdict.MemoryLimitExceeded => "Memory Limit Exceeded",
            SubmissionVerdict.OutputLimitExceeded => "Output Limit Exceeded",
            _ => "System Error"
        };
    }

    public static ExecutionFeedbackStatus
        MapOfficialExecutionStatus(
            SubmissionVerdict verdict)
    {
        return verdict switch
        {
            SubmissionVerdict.Accepted =>
                ExecutionFeedbackStatus.Passed,

            SubmissionVerdict.WrongAnswer or
            SubmissionVerdict.RuntimeError or
            SubmissionVerdict.TimeLimitExceeded or
            SubmissionVerdict.MemoryLimitExceeded or
            SubmissionVerdict.OutputLimitExceeded =>
                ExecutionFeedbackStatus.Failed,

            SubmissionVerdict.CompilationError or
            SubmissionVerdict.SystemError =>
                ExecutionFeedbackStatus.NotExecuted,

            _ => ExecutionFeedbackStatus.NotExecuted
        };
    }

    public static ExecutionFeedbackStatus
        MapCustomExecutionStatus(
            CodeExecutionState state)
    {
        return state switch
        {
            CodeExecutionState.ExecutionSucceeded =>
                ExecutionFeedbackStatus.Completed,

            CodeExecutionState.CompilationFailed =>
                ExecutionFeedbackStatus.NotExecuted,

            CodeExecutionState.RuntimeFailed or
            CodeExecutionState.TimedOut or
            CodeExecutionState.MemoryLimitExceeded or
            CodeExecutionState.OutputLimitExceeded =>
                ExecutionFeedbackStatus.Failed,

            _ => ExecutionFeedbackStatus.NotExecuted
        };
    }

    public static string MapCustomStatusText(
        CodeExecutionState state)
    {
        return MapCustomExecutionStatus(state) switch
        {
            ExecutionFeedbackStatus.Completed => "Completed",
            ExecutionFeedbackStatus.Failed => "Failed",
            _ => "NotExecuted"
        };
    }
}
