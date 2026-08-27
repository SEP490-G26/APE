// Application/Services/SubmissionResultEvaluator.cs

using Application.Exceptions;
using Application.Interfaces;
using Application.Models;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;

namespace Application.Services;

public sealed class SubmissionResultEvaluator
    : ISubmissionResultEvaluator
{
    private readonly ISubmissionScoringService _scoringService;

    public SubmissionResultEvaluator(
        ISubmissionScoringService scoringService)
    {
        _scoringService = scoringService;
    }

    public SubmissionEvaluation Evaluate(
        PEQuestion question,
        IReadOnlyList<CodeExecutionResult> executionResults,
        double maxScore)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(executionResults);

        var testCases = question.TestCases.ToList();

        if (testCases.Count == 0)
        {
            throw new DomainRuleException(
                "Programming question has no test cases.");
        }

        if (executionResults.Count != testCases.Count)
        {
            throw new CodeExecutionClientException(
                $"The provider returned {executionResults.Count} results " +
                $"for {testCases.Count} test cases.",
                isTransient: true,
                errorCategory: CodeExecutionErrorCategory.Provider);
        }

        if (executionResults.Any(result => !result.IsTerminal))
        {
            throw new CodeExecutionClientException(
                "Provider results have not finished.",
                isTransient: true,
                errorCategory: CodeExecutionErrorCategory.Provider);
        }

        var orderedResults = executionResults
            .OrderBy(result => result.CaseIndex)
            .ToList();

        var duplicateIndex = orderedResults
            .GroupBy(result => result.CaseIndex)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateIndex is not null)
        {
            throw new CodeExecutionClientException(
                $"Duplicate execution result case index: {duplicateIndex.Key}.",
                isTransient: false,
                errorCategory: CodeExecutionErrorCategory.Provider);
        }

        var caseIndexes = orderedResults
            .Select(result => result.CaseIndex)
            .ToArray();

        if (!caseIndexes.SequenceEqual(Enumerable.Range(0, testCases.Count)))
        {
            throw new CodeExecutionClientException(
                "Provider results do not cover the expected case indexes.",
                isTransient: false,
                errorCategory: CodeExecutionErrorCategory.Provider);
        }

        var testResults = new List<TestResultItem>(
            orderedResults.Count);

        for (var index = 0;
             index < orderedResults.Count;
             index++)
        {
            var executionResult = orderedResults[index];
            var testCase = testCases[index];

            var verdict = ExecutionFeedbackMapper.ResolveVerdict(
                executionResult,
                testCase.ExpectedOutput);

            var testResult = TestResultItem.Create(
                testCaseIndex: index,
                verdict: verdict,
                testCase: testCase,
                actualOutput:
                    executionResult.StandardOutput,
                standardError:
                    executionResult.StandardError ??
                    executionResult.Message,
                compileOutput:
                    executionResult.CompileOutput,
                runtimeMs:
                    ExecutionFeedbackMapper.ConvertRuntimeToMilliseconds(
                        executionResult.TimeSeconds),
                memoryKb:
                    Math.Max(
                        executionResult.MemoryKb ?? 0,
                        0));

            testResults.Add(testResult);
        }

        return _scoringService.Calculate(
            testResults,
            maxScore);
    }
}
