namespace Ape.AiModule.Application.Interfaces.AI;

public interface IProviderConfigurationService
{
    Task ReloadAsync(CancellationToken cancellationToken);
}
