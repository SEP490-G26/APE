using Application.Interfaces;

namespace BE_IntegrationTests.External;

public sealed class ControlledFileStorageService : IFileStorageService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public Task<string> SaveAsync(Stream stream, string fileName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        using var memory = new MemoryStream();
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        stream.CopyTo(memory);
        var relativePath = $"integration-storage/{Guid.NewGuid():N}-{fileName}";

        lock (_gate)
        {
            _files[relativePath] = memory.ToArray();
        }

        return Task.FromResult(relativePath);
    }

    public void Register(string relativePath, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);

        lock (_gate)
        {
            _files[relativePath] = content.ToArray();
        }
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_files.TryGetValue(relativePath, out var content))
            {
                return Task.FromResult<Stream?>(null);
            }

            return Task.FromResult<Stream?>(new MemoryStream(content, writable: false));
        }
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_files.ContainsKey(relativePath));
        }
    }

    public Task<long?> GetSizeAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<long?>(_files.TryGetValue(relativePath, out var content) ? content.LongLength : null);
        }
    }

    public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_files.Remove(relativePath));
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _files.Clear();
        }
    }
}
