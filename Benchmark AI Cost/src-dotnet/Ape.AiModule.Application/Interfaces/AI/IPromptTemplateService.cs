using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Domain.Entities.AI;

namespace Ape.AiModule.Application.Interfaces.AI;

public interface IPromptTemplateService
{
    Task<IReadOnlyList<PromptTemplate>> GetAllAsync(CancellationToken cancellationToken);
    Task<PromptTemplate> GetAsync(string key, CancellationToken cancellationToken);
    Task<PromptTemplate> UpsertAsync(PromptTemplateUpdateRequest request, CancellationToken cancellationToken);
    Task<PromptRenderResult> RenderAsync(string key, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken);
    Task ReloadAsync(CancellationToken cancellationToken);
}
