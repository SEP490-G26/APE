using Domain.Entities;

namespace Application.Interfaces;

public interface ITopupPackageRepository
{
    Task<List<TopupPackage>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<TopupPackage?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<TopupPackage?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<TopupPackage?> GetByAmountAsync(long amountVnd, CancellationToken cancellationToken = default);
    Task CreateAsync(TopupPackage package, CancellationToken cancellationToken = default);
    Task UpdateAsync(TopupPackage package, CancellationToken cancellationToken = default);
}
