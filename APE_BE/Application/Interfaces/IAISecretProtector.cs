namespace Application.Interfaces;

public interface IAISecretProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
    bool IsProtected(string? value);
}
