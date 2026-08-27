using Application.Models;
using Domain.Entities;

namespace Application.Interfaces;

public interface ISubmissionResultEvaluator
{
    SubmissionEvaluation Evaluate(
        PEQuestion question,
        IReadOnlyList<CodeExecutionResult> executionResults,
        double maxScore);
}