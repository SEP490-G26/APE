using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class SystemSettingRepository : ISystemSettingRepository
{
    private readonly IMongoCollection<SystemSetting> _collection;

    public SystemSettingRepository(DbContext dbContext)
    {
        _collection = dbContext.SystemSettings;
    }

    public async Task<SystemSetting?> GetByNameAsync(string settingName, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(item => item.SettingName == settingName).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(SystemSetting setting, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(
            item => item.SettingName == setting.SettingName,
            setting,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}
