using Domain.Entities;

namespace Application.Interfaces;

public interface ICourseRepository
{
    // =========================
    // CRUD
    // =========================

    Task<Course> CreateAsync(Course course);

    Task<Course?> GetByIdAsync(string id);

    Task<Course?> GetByCodeAsync(string code);

    Task UpdateAsync(Course course);

    Task<bool> SoftDeleteAsync(string id, string? userId);

    // =========================
    // Query
    // =========================

    Task<(IReadOnlyList<Course> Items, long Total)> ListAsync(  
        int page,
        int pageSize,
        string? keyword = null,
        string? status = null);

    Task<IReadOnlyList<Course>> GetAllAsync(
        bool includeDeleted = false);

    Task<bool> ExistsByCodeAsync(
        string code,
        string? excludeId = null);

    Task<bool> UpdateStatusAsync(string id, string status, string? modifiedBy);
}