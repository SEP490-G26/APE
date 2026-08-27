using Domain.Entities;

namespace Application.Interfaces;

public interface IExamRepository
{
    Task InsertAsync(
        Exam exam,
        CancellationToken cancellationToken = default);

    Task<Exam?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<(List<Exam> Items, long Total)> ListAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<(List<Exam> Items, long Total)> ListByCourseAsync(
        string courseId,
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<(List<Exam> Items, long Total)> ListPublicAsync(
        string? courseId,
        string? examType,
        string? mode,
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task<(List<Exam> Items, long Total)> ListByCreatorAsync(
        string creatorUserId,
        string? courseId,
        string? examType,
        string? mode,
        int page,
        int limit,
        CancellationToken cancellationToken = default);

    Task ReplaceAsync(
        Exam exam,
        CancellationToken cancellationToken = default);
}
