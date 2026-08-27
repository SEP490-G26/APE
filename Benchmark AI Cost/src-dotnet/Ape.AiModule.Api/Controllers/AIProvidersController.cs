using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.AspNetCore.Mvc;

namespace Ape.AiModule.Api.Controllers;

[ApiController]
[Route("api/ai-module/providers")]
public sealed class AIProvidersController : ControllerBase
{
    private readonly IProviderCatalogService _providerCatalogService;
    private readonly IProviderConfigurationService _providerConfigurationService;

    public AIProvidersController(
        IProviderCatalogService providerCatalogService,
        IProviderConfigurationService providerConfigurationService)
    {
        _providerCatalogService = providerCatalogService;
        _providerConfigurationService = providerConfigurationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProvidersAsync(CancellationToken cancellationToken)
    {
        var providers = await _providerCatalogService.GetProvidersAsync(cancellationToken);
        return Ok(providers);
    }

    [HttpGet("{provider}/models")]
    public async Task<IActionResult> GetProviderModelsAsync(string provider, CancellationToken cancellationToken)
    {
        var catalog = await _providerCatalogService.GetProviderCatalogAsync(provider, cancellationToken);
        return Ok(catalog);
    }

    [HttpGet("{provider}/ping")]
    public async Task<IActionResult> PingProviderAsync(string provider, CancellationToken cancellationToken)
    {
        var ping = await _providerCatalogService.PingProviderAsync(provider, cancellationToken);
        return Ok(ping);
    }

    [HttpPost("reload")]
    public async Task<IActionResult> ReloadAsync(CancellationToken cancellationToken)
    {
        await _providerConfigurationService.ReloadAsync(cancellationToken);
        var providers = await _providerCatalogService.GetProvidersAsync(cancellationToken);
        return Ok(new
        {
            reloaded = true,
            providers
        });
    }
}
