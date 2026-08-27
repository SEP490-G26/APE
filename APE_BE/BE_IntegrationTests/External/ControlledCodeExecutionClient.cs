using Application.Interfaces;
using Application.Models;
using Domain.Entities;

namespace BE_IntegrationTests.External;

public sealed class ControlledCodeExecutionClient : ICodeExecutionClient
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CodeExecutionResult> _resultsByToken = new(StringComparer.Ordinal);

    public Task<CodeExecutionBatchReceipt> CreateBatchAsync(
        CodeExecutionBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tokens = new List<CodeExecutionToken>();
        var executionResults = BuildResults(request);

        lock (_gate)
        {
            foreach (var result in executionResults)
            {
                _resultsByToken[result.ProviderToken] = result;
                tokens.Add(new CodeExecutionToken(result.CaseIndex, result.ProviderToken));
            }
        }

        return Task.FromResult(
            new CodeExecutionBatchReceipt(
                "ControlledJudge0",
                tokens));
    }

    public Task<IReadOnlyList<CodeExecutionResult>> GetBatchResultsAsync(
        IReadOnlyCollection<string> tokens,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        List<CodeExecutionResult> results;

        lock (_gate)
        {
            results = tokens
                .Select(token => _resultsByToken[token])
                .OrderBy(item => item.CaseIndex)
                .ToList();
        }

        return Task.FromResult<IReadOnlyList<CodeExecutionResult>>(results);
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public void Reset()
    {
        lock (_gate)
        {
            _resultsByToken.Clear();
        }
    }

    private static IReadOnlyList<CodeExecutionResult> BuildResults(CodeExecutionBatchRequest request)
    {
        var sourceText = string.Join(
            "\n",
            request.SourceFiles.Select(file => $"{file.Filename}:{file.Content}"));

        return request.Cases
            .Select(item => BuildResult(item, sourceText))
            .ToList();
    }

    private static CodeExecutionResult BuildResult(
        CodeExecutionCaseRequest testCase,
        string sourceText)
    {
        var token = $"controlled-{Guid.NewGuid():N}";

        if (sourceText.Contains("COMPILE_ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return new CodeExecutionResult(
                testCase.CaseIndex,
                token,
                CodeExecutionState.CompilationFailed,
                null,
                null,
                "Compilation failed.",
                null,
                null,
                null,
                "compile-error",
                "Compilation failed");
        }

        if (sourceText.Contains("WRONG_ANSWER", StringComparison.OrdinalIgnoreCase))
        {
            return new CodeExecutionResult(
                testCase.CaseIndex,
                token,
                CodeExecutionState.ExecutionSucceeded,
                "not-the-expected-output",
                null,
                null,
                null,
                0.02d,
                256,
                "accepted",
                "Executed");
        }

        if (sourceText.Contains("RUNTIME_ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return new CodeExecutionResult(
                testCase.CaseIndex,
                token,
                CodeExecutionState.RuntimeFailed,
                null,
                "Runtime error",
                null,
                "Runtime error",
                0.02d,
                256,
                "runtime-error",
                "Runtime error");
        }

        return new CodeExecutionResult(
            testCase.CaseIndex,
            token,
            CodeExecutionState.ExecutionSucceeded,
            testCase.StandardInput,
            null,
            null,
            null,
            0.02d,
            256,
            "accepted",
            "Executed");
    }
}
