using Application.Options;

namespace Application.Interfaces;

public interface IAIProviderSettingsResolver
{
    AIProviderOptions GetProvider(string providerName);
    void Reload();
}
