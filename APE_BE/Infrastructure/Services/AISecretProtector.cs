using Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.AI;

public class AISecretProtector : IAISecretProtector
{
    private const string Prefix = "enc::";
    private readonly IDataProtector _protector;

    public AISecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("APE_BE.AI.ProviderCredentials.v1");
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        if (IsProtected(plainText))
        {
            return plainText;
        }

        return Prefix + _protector.Protect(plainText);
    }

    public string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return string.Empty;
        }

        if (!IsProtected(protectedText))
        {
            return protectedText;
        }

        return _protector.Unprotect(protectedText[Prefix.Length..]);
    }

    public bool IsProtected(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
