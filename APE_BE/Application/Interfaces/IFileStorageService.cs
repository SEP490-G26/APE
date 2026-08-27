namespace Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream stream, string fileName);
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<long?> GetSizeAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}
