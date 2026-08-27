/**
 * ExecutionFeedbackDiagnosticParser.cs
 * Subsystem in Step 5 of the APE AI Pipeline: Diagnostic Parser for Code Execution Results.
 * 
 * Responsibilities:
 * Parses raw compiler logs, runtime stack traces, and failed test cases from Judge0,
 * extracting line numbers, error categories, and contextual diagnostic tokens for the Code Mentor.
 */

using System.Linq;
using System.Text.RegularExpressions;
using Application.DTOs;
using Application.Interfaces;
using Application.Models;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Parser that transforms raw execution and compiler output into structured diagnostics.
/// </summary>
public sealed class ExecutionFeedbackDiagnosticParser
    : IExecutionFeedbackDiagnosticParser
{
    private const int MaximumDiagnostics = 10;
    private const int MaximumSuggestions = 3;

    private static readonly Regex JavaStackFramePattern =
        new(
            @"\bat [\w.$<>]+\((?<file>[^():]+):(?<line>\d+)\)",
            RegexOptions.Compiled);

    public IReadOnlyCollection<ExecutionFeedbackDiagnosticDto>
        ParseCompilationDiagnostics(
            string? compileOutput,
            IReadOnlyCollection<CodeFile> submittedFiles,
            bool includeRawDetails)
    {
        if (string.IsNullOrWhiteSpace(compileOutput))
        {
            return
            [
                CreateDiagnostic(
                    ExecutionFeedbackCategory.Compilation,
                    "COMPILATION_FAILED",
                    "The code did not compile",
                    "The compiler could not build the submitted code.",
                    suggestions:
                    [
                        "Check the syntax near the reported line.",
                        "Review the spelling of class, method, and variable names."
                    ],
                    rawDetails: includeRawDetails
                        ? null
                        : null)
            ];
        }

        var diagnostics = new List<ExecutionFeedbackDiagnosticDto>();
        var sanitizedOutput = ExecutionFeedbackSanitizer.SanitizeTechnicalText(
            compileOutput,
            submittedFiles);

        foreach (var line in compileOutput
                     .Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var diagnostic = CreateCompilationDiagnostic(
                line,
                sanitizedOutput,
                submittedFiles,
                includeRawDetails);

            diagnostics.Add(diagnostic);

            if (diagnostics.Count >= MaximumDiagnostics)
            {
                break;
            }
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(
                CreateDiagnostic(
                    ExecutionFeedbackCategory.Compilation,
                    "COMPILATION_FAILED",
                    "The code did not compile",
                    "The compiler reported an error, but the exact pattern was not recognized.",
                    suggestions:
                    [
                        "Review the compiler output for the first reported error.",
                        "Check the code near the file and line reported by the compiler."
                    ],
                    rawDetails: includeRawDetails
                        ? sanitizedOutput
                        : null));
        }

        return diagnostics;
    }

    public IReadOnlyCollection<ExecutionFeedbackDiagnosticDto>
        ParseExecutionDiagnostics(
            CodeExecutionResult result,
            IReadOnlyCollection<CodeFile> submittedFiles,
            bool includeRawDetails)
    {
        var details = string.Join(
            "\n",
            result.ProviderStatusDescription,
            result.StandardError,
            result.Message);

        var sanitizedDetails =
            ExecutionFeedbackSanitizer.SanitizeTechnicalText(
                details,
                submittedFiles);

        return result.State switch
        {
            CodeExecutionState.TimedOut =>
            [
                CreateDiagnostic(
                    ExecutionFeedbackCategory.ResourceLimit,
                    "TIME_LIMIT_EXCEEDED",
                    "The program took too long to finish",
                    "The program did not finish before the time limit.",
                    suggestions:
                    [
                        "Check whether a loop may run longer than expected.",
                        "Review whether the algorithm is efficient enough for the input size."
                    ],
                    rawDetails: includeRawDetails ? sanitizedDetails : null)
            ],

            CodeExecutionState.MemoryLimitExceeded =>
            [
                CreateDiagnostic(
                    ExecutionFeedbackCategory.ResourceLimit,
                    "MEMORY_LIMIT_EXCEEDED",
                    "The program used too much memory",
                    "The program used more memory than the execution limit allowed.",
                    suggestions:
                    [
                        "Review large arrays, lists, or recursion depth.",
                        "Check whether temporary objects are being created too often."
                    ],
                    rawDetails: includeRawDetails ? sanitizedDetails : null)
            ],

            CodeExecutionState.OutputLimitExceeded =>
            [
                CreateDiagnostic(
                    ExecutionFeedbackCategory.ResourceLimit,
                    "OUTPUT_LIMIT_EXCEEDED",
                    "The program produced too much output",
                    "The program printed more output than the execution limit allowed.",
                    suggestions:
                    [
                        "Check whether debugging output is still enabled.",
                        "Review loops that may print repeatedly."
                    ],
                    rawDetails: includeRawDetails ? sanitizedDetails : null)
            ],

            CodeExecutionState.RuntimeFailed =>
            [
                CreateRuntimeDiagnostic(
                    details,
                    sanitizedDetails,
                    submittedFiles,
                    includeRawDetails)
            ],

            _ => Array.Empty<ExecutionFeedbackDiagnosticDto>()
        };
    }

    private static ExecutionFeedbackDiagnosticDto
        CreateCompilationDiagnostic(
            string line,
            string? sanitizedOutput,
            IReadOnlyCollection<CodeFile> submittedFiles,
            bool includeRawDetails)
    {
        ExecutionFeedbackSanitizer.TryExtractSafeSourceLocation(
            line,
            submittedFiles,
            out var filename,
            out var sourceLine,
            out var column);

        if (line.Contains("';' expected", StringComparison.Ordinal))
        {
            return CreateDiagnostic(
                ExecutionFeedbackCategory.Compilation,
                "MISSING_SEMICOLON",
                "A semicolon is missing",
                "A statement appears to be missing a semicolon.",
                filename,
                sourceLine,
                column,
                suggestions:
                [
                    "Check the end of the statement on the reported line.",
                    "Compare the line with nearby statements that compile correctly."
                ],
                rawDetails: includeRawDetails ? sanitizedOutput : null);
        }

        if (line.Contains(
                "cannot find symbol",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateDiagnostic(
                ExecutionFeedbackCategory.Compilation,
                "CANNOT_FIND_SYMBOL",
                "A name could not be resolved",
                "The compiler could not find a class, method, or variable with this name.",
                filename,
                sourceLine,
                column,
                suggestions:
                [
                    "Check the spelling and capitalization of the name.",
                    "Make sure the variable or method is declared before it is used."
                ],
                rawDetails: includeRawDetails ? sanitizedOutput : null);
        }

        if (line.Contains(
                "undeclared",
                StringComparison.OrdinalIgnoreCase) ||
            line.Contains(
                "was not declared in this scope",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateDiagnostic(
                ExecutionFeedbackCategory.Compilation,
                "UNDECLARED_IDENTIFIER",
                "A name is used before it is declared",
                "The compiler found a variable or function name that is not declared in the current scope.",
                filename,
                sourceLine,
                column,
                suggestions:
                [
                    "Check the spelling of the identifier.",
                    "Make sure the declaration is visible before the usage."
                ],
                rawDetails: includeRawDetails ? sanitizedOutput : null);
        }

        return CreateDiagnostic(
            ExecutionFeedbackCategory.Compilation,
            "COMPILATION_FAILED",
            "The code did not compile",
            "The compiler reported an error near the reported source location.",
            filename,
            sourceLine,
            column,
            suggestions:
            [
                "Review the reported file and line first.",
                "Fix the first compiler error before checking later errors."
            ],
            rawDetails: includeRawDetails ? sanitizedOutput : null);
    }

    private static ExecutionFeedbackDiagnosticDto
        CreateRuntimeDiagnostic(
            string details,
            string? sanitizedDetails,
            IReadOnlyCollection<CodeFile> submittedFiles,
            bool includeRawDetails)
    {
        var normalized = details ?? string.Empty;

        if (normalized.Contains(
                "NullPointerException",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateJavaRuntimeDiagnostic(
                "NULL_REFERENCE",
                "A null value was used like an object",
                "The program tried to use an object reference that was null.",
                normalized,
                sanitizedDetails,
                submittedFiles,
                includeRawDetails,
                [
                    "Check whether the object was created before it is used.",
                    "Review variables that may still be null at this point."
                ]);
        }

        if (normalized.Contains(
                "ArrayIndexOutOfBoundsException",
                StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(
                "IndexOutOfBoundsException",
                StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(
                "std::out_of_range",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateJavaRuntimeDiagnostic(
                "INDEX_OUT_OF_RANGE",
                "An index is outside the valid range",
                "The program tried to access a position that does not exist.",
                normalized,
                sanitizedDetails,
                submittedFiles,
                includeRawDetails,
                [
                    "Check the start and end conditions of the loop.",
                    "Make sure the index is smaller than the collection length."
                ]);
        }

        if (normalized.Contains(
                "ArithmeticException",
                StringComparison.OrdinalIgnoreCase) &&
            normalized.Contains(
                "/ by zero",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateJavaRuntimeDiagnostic(
                "DIVISION_BY_ZERO",
                "A division used zero as the divisor",
                "The program tried to divide a value by zero.",
                normalized,
                sanitizedDetails,
                submittedFiles,
                includeRawDetails,
                [
                    "Check the value before dividing.",
                    "Review whether input data or loop counters can become zero."
                ]);
        }

        if (normalized.Contains(
                "NumberFormatException",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateJavaRuntimeDiagnostic(
                "INVALID_NUMBER_FORMAT",
                "Text could not be converted to a number",
                "The program tried to parse text that is not in a valid numeric format.",
                normalized,
                sanitizedDetails,
                submittedFiles,
                includeRawDetails,
                [
                    "Check the input before converting it to a number.",
                    "Review whether spaces or unexpected characters are present."
                ]);
        }

        if (normalized.Contains(
                "StackOverflowError",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateJavaRuntimeDiagnostic(
                "STACK_OVERFLOW",
                "The call stack became too deep",
                "The program used more stack space than the runtime allowed.",
                normalized,
                sanitizedDetails,
                submittedFiles,
                includeRawDetails,
                [
                    "Check whether recursion has a correct stopping condition.",
                    "Review whether method calls may repeat indefinitely."
                ]);
        }

        if (normalized.Contains(
                "OutOfMemoryError",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateJavaRuntimeDiagnostic(
                "OUT_OF_MEMORY",
                "The program ran out of memory",
                "The program tried to allocate more memory than the runtime could provide.",
                normalized,
                sanitizedDetails,
                submittedFiles,
                includeRawDetails,
                [
                    "Review large collections or arrays.",
                    "Check whether the program keeps references longer than needed."
                ]);
        }

        if (normalized.Contains(
                "SIGSEGV",
                StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(
                "segmentation fault",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateDiagnostic(
                ExecutionFeedbackCategory.Runtime,
                "INVALID_MEMORY_ACCESS",
                "The program accessed invalid memory",
                "The program may have accessed memory that does not belong to the current object or array.",
                suggestions:
                [
                    "Check pointer usage and array boundaries.",
                    "Review whether an object or buffer is initialized before access."
                ],
                rawDetails: includeRawDetails ? sanitizedDetails : null);
        }

        if (normalized.Contains(
                "SIGFPE",
                StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(
                "floating point exception",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateDiagnostic(
                ExecutionFeedbackCategory.Runtime,
                "ARITHMETIC_ERROR",
                "The program triggered an arithmetic error",
                "The program may have performed an invalid arithmetic operation.",
                suggestions:
                [
                    "Check divisors and modulo operations.",
                    "Review whether numeric values stay within valid ranges."
                ],
                rawDetails: includeRawDetails ? sanitizedDetails : null);
        }

        if (normalized.Contains(
                "assert",
                StringComparison.OrdinalIgnoreCase))
        {
            return CreateDiagnostic(
                ExecutionFeedbackCategory.Runtime,
                "ASSERTION_FAILED",
                "An assertion failed during execution",
                "A runtime assertion reported that an expected condition was not true.",
                suggestions:
                [
                    "Review the condition that the assertion checks.",
                    "Trace the values that reach that condition."
                ],
                rawDetails: includeRawDetails ? sanitizedDetails : null);
        }

        return CreateDiagnostic(
            ExecutionFeedbackCategory.Runtime,
            "RUNTIME_ERROR",
            "The program stopped with a runtime error",
            "The program stopped during execution, but the exact runtime pattern was not recognized.",
            suggestions:
            [
                "Review the runtime error output for the first failing line.",
                "Check whether the program handles unexpected values safely."
            ],
            rawDetails: includeRawDetails ? sanitizedDetails : null);
    }

    private static ExecutionFeedbackDiagnosticDto
        CreateJavaRuntimeDiagnostic(
            string code,
            string title,
            string message,
            string details,
            string? sanitizedDetails,
            IReadOnlyCollection<CodeFile> submittedFiles,
            bool includeRawDetails,
            IReadOnlyList<string> suggestions)
    {
        string? filename = null;
        int? line = null;

        foreach (Match match in JavaStackFramePattern.Matches(details))
        {
            var candidateFile = match.Groups["file"].Value;
            var submitted = submittedFiles.Any(
                file => string.Equals(
                    Path.GetFileName(file.Filename),
                    candidateFile,
                    StringComparison.OrdinalIgnoreCase));

            if (!submitted)
            {
                continue;
            }

            filename = candidateFile;
            line = int.TryParse(
                match.Groups["line"].Value,
                out var parsed)
                ? parsed
                : null;
            break;
        }

        return CreateDiagnostic(
            ExecutionFeedbackCategory.Runtime,
            code,
            title,
            message,
            filename,
            line,
            null,
            suggestions,
            includeRawDetails ? sanitizedDetails : null);
    }

    private static ExecutionFeedbackDiagnosticDto
        CreateDiagnostic(
            ExecutionFeedbackCategory category,
            string code,
            string title,
            string message,
            string? filename = null,
            int? line = null,
            int? column = null,
            IReadOnlyList<string>? suggestions = null,
            string? rawDetails = null)
    {
        return new ExecutionFeedbackDiagnosticDto
        {
            Category = category,
            Code = code,
            Title = ExecutionFeedbackSanitizer.Truncate(
                title,
                ExecutionFeedbackSanitizer.MaximumTitleLength),
            Message = ExecutionFeedbackSanitizer.Truncate(
                message,
                ExecutionFeedbackSanitizer.MaximumMessageLength),
            Filename = filename,
            Line = line,
            Column = column,
            Suggestions = (suggestions ?? Array.Empty<string>())
                .Take(MaximumSuggestions)
                .Select(item => ExecutionFeedbackSanitizer.Truncate(
                    item,
                    ExecutionFeedbackSanitizer.MaximumMessageLength))
                .ToArray(),
            RawDetails = rawDetails is null
                ? null
                : ExecutionFeedbackSanitizer.Truncate(
                    rawDetails,
                    ExecutionFeedbackSanitizer.MaximumRawDetailsLength)
        };
    }
}
