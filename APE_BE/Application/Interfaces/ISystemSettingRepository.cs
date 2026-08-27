using Domain.Entities;

namespace Application.Interfaces;

public interface ISystemSettingRepository
{
    Task<SystemSetting?> GetByNameAsync(string settingName, CancellationToken cancellationToken = default);
    Task UpsertAsync(SystemSetting setting, CancellationToken cancellationToken = default);
}
