using DotNetEnv;
using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class ProviderConfigurationService : IProviderConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;

    public ProviderConfigurationService(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
    }

    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        var envFilePath = Path.Combine(_hostEnvironment.ContentRootPath, ".env");
        if (File.Exists(envFilePath))
        {
            Env.Load(envFilePath);
        }

        if (_configuration is IConfigurationRoot root)
        {
            root.Reload();
        }

        return Task.CompletedTask;
    }
}
