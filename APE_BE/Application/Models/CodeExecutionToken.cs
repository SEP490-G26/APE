using Domain.Exceptions;

namespace Application.Models;

public sealed record CodeExecutionToken
{
    public CodeExecutionToken(
        int caseIndex,
        string providerToken)
    {
        if (caseIndex < 0)
        {
            throw new DomainRuleException(
                "CaseIndex cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(providerToken))
        {
            throw new DomainRuleException(
                "ProviderToken is required.");
        }

        CaseIndex = caseIndex;
        ProviderToken = providerToken.Trim();
    }

    public int CaseIndex { get; }

    public string ProviderToken { get; }
}
