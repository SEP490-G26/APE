using Domain.Exceptions;

namespace Application.Models;

public sealed class CodeExecutionBatchReceipt
{
    private readonly List<CodeExecutionToken> _tokens;

    public CodeExecutionBatchReceipt(
        string providerName,
        IReadOnlyList<CodeExecutionToken> tokens)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new DomainRuleException(
                "ProviderName is required.");
        }

        ArgumentNullException.ThrowIfNull(tokens);

        var normalizedTokens = tokens
            .Select(item => item ?? throw new DomainRuleException(
                "Execution tokens cannot contain null items."))
            .OrderBy(item => item.CaseIndex)
            .ToList();

        if (normalizedTokens.Count == 0)
        {
            throw new DomainRuleException(
                "Execution receipt requires at least one token.");
        }

        var duplicateCaseIndex = normalizedTokens
            .GroupBy(item => item.CaseIndex)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateCaseIndex is not null)
        {
            throw new DomainRuleException(
                $"Duplicate execution case index: {duplicateCaseIndex.Key}.");
        }

        var duplicateToken = normalizedTokens
            .GroupBy(item => item.ProviderToken, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateToken is not null)
        {
            throw new DomainRuleException(
                $"Duplicate execution provider token: {duplicateToken.Key}.");
        }

        ProviderName = providerName.Trim();
        _tokens = normalizedTokens;
    }

    [Obsolete(
        "Use the provider-neutral constructor with explicit case-indexed tokens.")]
    public CodeExecutionBatchReceipt(
        IReadOnlyList<string> tokens)
        : this(
            "Judge0",
            tokens.Select((token, index) => new CodeExecutionToken(index, token))
                .ToList())
    {
    }

    public string ProviderName { get; }

    public IReadOnlyList<CodeExecutionToken> Tokens =>
        _tokens.AsReadOnly();
}
