using Domain.Entities;
using Domain.Exceptions;

namespace Application.Models;

public sealed class CodeExecutionBatchRequest
{
    private readonly List<CodeExecutionCaseRequest> _cases;
    private readonly List<CodeFile> _sourceFiles;

    private CodeExecutionBatchRequest(
        int languageId,
        IEnumerable<CodeFile> sourceFiles,
        IEnumerable<CodeExecutionCaseRequest> cases)
    {
        LanguageId = languageId;
        _sourceFiles = sourceFiles.ToList();
        _cases = cases
            .OrderBy(item => item.CaseIndex)
            .ToList();
    }

    public int LanguageId { get; }

    public IReadOnlyList<CodeFile> SourceFiles => _sourceFiles.AsReadOnly();

    public IReadOnlyList<CodeExecutionCaseRequest> Cases =>
        _cases.AsReadOnly();

    [Obsolete(
        "Use Cases. This compatibility projection exists only until the " +
        "Judge0 adapter is migrated.")]
    public IReadOnlyList<TestCase> TestCases =>
        _cases
            .Select(item => new TestCase
            {
                Input = item.StandardInput,
                ExpectedOutput = string.Empty,
                IsHidden = false,
                TimeLimitMs = item.TimeLimitMs,
                MemoryLimitKb = item.MemoryLimitKb
            })
            .ToList()
            .AsReadOnly();

    public bool IsMultiFile => _sourceFiles.Count > 1;

    public static CodeExecutionBatchRequest Create(
        int languageId,
        IEnumerable<CodeFile> sourceFiles,
        IEnumerable<CodeExecutionCaseRequest> cases)
    {
        if (languageId <= 0)
        {
            throw new DomainRuleException(
                "LanguageId must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(cases);

        var files = sourceFiles.ToList();
        var executionCases = cases.ToList();

        if (files.Count == 0)
        {
            throw new DomainRuleException(
                "At least one source file is required.");
        }

        if (files.Any(file => file is null))
        {
            throw new DomainRuleException(
                "Source files cannot contain null items.");
        }

        var duplicateFile = files
            .GroupBy(
                file => file.Filename,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateFile is not null)
        {
            throw new DomainRuleException(
                $"Duplicate source filename: {duplicateFile.Key}.");
        }

        if (executionCases.Count == 0)
        {
            throw new DomainRuleException(
                "At least one execution case is required.");
        }

        if (executionCases.Any(item => item is null))
        {
            throw new DomainRuleException(
                "Execution cases cannot contain null items.");
        }

        var duplicateCaseIndex = executionCases
            .GroupBy(item => item.CaseIndex)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateCaseIndex is not null)
        {
            throw new DomainRuleException(
                $"Duplicate execution case index: {duplicateCaseIndex.Key}.");
        }

        return new CodeExecutionBatchRequest(
            languageId,
            files,
            executionCases);
    }
}
