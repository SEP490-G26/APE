using Application.DTOs;
using Application.Models;
using Domain.Entities;

namespace Application.Interfaces;

public interface IExecutionFeedbackDiagnosticParser
{
    IReadOnlyCollection<ExecutionFeedbackDiagnosticDto>
        ParseCompilationDiagnostics(
            string? compileOutput,
            IReadOnlyCollection<CodeFile> submittedFiles,
            bool includeRawDetails);

    IReadOnlyCollection<ExecutionFeedbackDiagnosticDto>
        ParseExecutionDiagnostics(
            CodeExecutionResult result,
            IReadOnlyCollection<CodeFile> submittedFiles,
            bool includeRawDetails);
}
