using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.AspNetCore.Mvc;

namespace Ape.AiModule.Api.Controllers;

[ApiController]
[Route("api/ai-module/prompts")]
public sealed class AIPromptsController : ControllerBase
{
    private readonly IPromptTemplateService _promptTemplateService;

    public AIPromptsController(IPromptTemplateService promptTemplateService)
    {
        _promptTemplateService = promptTemplateService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
        => Ok(await _promptTemplateService.GetAllAsync(cancellationToken));

    [HttpGet("{key}")]
    public async Task<IActionResult> GetAsync(string key, CancellationToken cancellationToken)
        => Ok(await _promptTemplateService.GetAsync(key, cancellationToken));

    [HttpPost("reload")]
    public async Task<IActionResult> ReloadAsync(CancellationToken cancellationToken)
    {
        await _promptTemplateService.ReloadAsync(cancellationToken);
        return Ok(new { reloaded = true, prompts = await _promptTemplateService.GetAllAsync(cancellationToken) });
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> UpsertAsync(string key, [FromBody] PromptTemplateUpdateRequest request, CancellationToken cancellationToken)
        => Ok(await _promptTemplateService.UpsertAsync(request with { Key = key }, cancellationToken));

    [HttpPost("render")]
    public async Task<IActionResult> RenderAsync([FromBody] PromptRenderRequest request, CancellationToken cancellationToken)
        => Ok(await _promptTemplateService.RenderAsync(request.Key, request.Variables, cancellationToken));
}
