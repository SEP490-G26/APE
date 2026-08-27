

using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;

namespace Application.Services;

public sealed class EqualWeightSubmissionScoringService
    : ISubmissionScoringService
{
    public SubmissionEvaluation Calculate(
        IReadOnlyCollection<TestResultItem> testResults,
        double maxScore)
    {
        ArgumentNullException.ThrowIfNull(testResults);

        if (testResults.Count == 0)
            throw new DomainRuleException(
                "Cannot calculate score without test results.");

        if (maxScore <= 0)
            throw new DomainRuleException(
                "MaxScore must be greater than zero.");

        if (testResults.Any(result =>
                result.Verdict == SubmissionVerdict.SystemError))
        {
            throw new DomainRuleException(
                "Cannot calculate score when a test case has a system error.");
        }

        var passedCount = testResults.Count(
            result => result.IsPassed);

        var totalCount = testResults.Count;

        var score = Math.Round(
            (double)passedCount / totalCount * maxScore,
            2,
            MidpointRounding.AwayFromZero);

        score = Math.Clamp(score, 0, maxScore);

        var finalVerdict = ResolveFinalVerdict(
            testResults,
            passedCount,
            totalCount);

        return SubmissionEvaluation.Create(
            testResults,
            score,
            finalVerdict,
            passedCount,
            totalCount);
    }

    private static SubmissionVerdict ResolveFinalVerdict(
        IReadOnlyCollection<TestResultItem> testResults,
        int passedCount,
        int totalCount)
    {
        if (passedCount == totalCount)
            return SubmissionVerdict.Accepted;

        if (testResults.Any(result =>
                result.Verdict ==
                SubmissionVerdict.CompilationError))
        {
            return SubmissionVerdict.CompilationError;
        }

        if (testResults.Any(result =>
                result.Verdict ==
                SubmissionVerdict.TimeLimitExceeded))
        {
            return SubmissionVerdict.TimeLimitExceeded;
        }

        if (testResults.Any(result =>
                result.Verdict ==
                SubmissionVerdict.MemoryLimitExceeded))
        {
            return SubmissionVerdict.MemoryLimitExceeded;
        }

        if (testResults.Any(result =>
                result.Verdict ==
                SubmissionVerdict.OutputLimitExceeded))
        {
            return SubmissionVerdict.OutputLimitExceeded;
        }

        if (testResults.Any(result =>
                result.Verdict ==
                SubmissionVerdict.RuntimeError))
        {
            return SubmissionVerdict.RuntimeError;
        }

        return SubmissionVerdict.WrongAnswer;
    }
}