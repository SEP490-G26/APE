using Application.DTOs;

namespace Application.Interfaces;

public interface IPECodeRunService
{
    Task<PECodeRunServiceResult> RunAsync(
        string studentId,
        PECodeRunInputDto request,
        CancellationToken cancellationToken = default);
}
