using Application.Models;

namespace Application.Interfaces;

public interface ICodeExecutionClient
{
    Task<CodeExecutionBatchReceipt> CreateBatchAsync(
        CodeExecutionBatchRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CodeExecutionResult>> GetBatchResultsAsync(
        IReadOnlyCollection<string> tokens,
        CancellationToken cancellationToken = default);

    Task<bool> IsHealthyAsync(
        CancellationToken cancellationToken = default);
}
