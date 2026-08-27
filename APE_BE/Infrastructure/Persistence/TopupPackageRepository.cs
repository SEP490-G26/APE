using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class TopupPackageRepository : ITopupPackageRepository
{
    private readonly IMongoCollection<TopupPackage> _packages;

    public TopupPackageRepository(DbContext dbContext)
    {
        _packages = dbContext.TopupPackages;
    }

    public async Task<List<TopupPackage>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var filter = includeInactive
            ? Builders<TopupPackage>.Filter.Empty
            : Builders<TopupPackage>.Filter.Eq(item => item.IsActive, true);

        return await _packages.Find(filter)
            .SortBy(item => item.SortOrder)
            .ThenBy(item => item.AmountVnd)
            .ToListAsync(cancellationToken);
    }

    public async Task<TopupPackage?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _packages.Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<TopupPackage?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => await _packages.Find(item => item.Code == code).FirstOrDefaultAsync(cancellationToken);

    public async Task<TopupPackage?> GetByAmountAsync(long amountVnd, CancellationToken cancellationToken = default)
        => await _packages.Find(item => item.AmountVnd == amountVnd).FirstOrDefaultAsync(cancellationToken);

    public Task CreateAsync(TopupPackage package, CancellationToken cancellationToken = default)
        => _packages.InsertOneAsync(package, cancellationToken: cancellationToken);

    public Task UpdateAsync(TopupPackage package, CancellationToken cancellationToken = default)
        => _packages.ReplaceOneAsync(item => item.Id == package.Id, package, cancellationToken: cancellationToken);
}
