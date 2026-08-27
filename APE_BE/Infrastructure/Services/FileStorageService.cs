using Application.Interfaces;
using Application.Options;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly AzureBlobStorageOptions _options;
    private readonly BlobContainerClient? _containerClient;
    private readonly string _localRootPath;
    private readonly bool _useAzureBlobStorage;

    public FileStorageService(IOptions<AzureBlobStorageOptions> options)
    {
        _options = options.Value;
        _useAzureBlobStorage =
            !string.IsNullOrWhiteSpace(_options.ConnectionString) &&
            !string.IsNullOrWhiteSpace(_options.ContainerName);

        if (_useAzureBlobStorage)
        {
            var serviceClient = new BlobServiceClient(_options.ConnectionString);
            _containerClient = serviceClient.GetBlobContainerClient(_options.ContainerName);
            _containerClient.CreateIfNotExists(PublicAccessType.None);
        }

        var localRelativeRoot = BuildLocalRelativeRoot();
        _localRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", localRelativeRoot);
        Directory.CreateDirectory(_localRootPath);
    }

    public async Task<string> SaveAsync(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        if (_useAzureBlobStorage)
        {
            var blobPath = BuildBlobPath(uniqueFileName);
            var blobClient = _containerClient!.GetBlobClient(blobPath);
            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = ResolveContentType(extension)
                    }
                });

            return blobPath;
        }

        var localRelativePath = BuildLocalRelativePath(uniqueFileName);
        var localAbsolutePath = Path.Combine(_localRootPath, uniqueFileName);
        await using var output = File.Create(localAbsolutePath);
        await stream.CopyToAsync(output);
        return localRelativePath;
    }

    public async Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (_useAzureBlobStorage)
        {
            var blobPath = NormalizeBlobPath(relativePath);
            var blobClient = _containerClient!.GetBlobClient(blobPath);
            var exists = await blobClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                return null;
            }

            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }

        var localAbsolutePath = ResolveLocalAbsolutePath(relativePath);
        if (!File.Exists(localAbsolutePath))
        {
            return null;
        }

        return File.OpenRead(localAbsolutePath);
    }

    public async Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (_useAzureBlobStorage)
        {
            var blobPath = NormalizeBlobPath(relativePath);
            var blobClient = _containerClient!.GetBlobClient(blobPath);
            var exists = await blobClient.ExistsAsync(cancellationToken);
            return exists.Value;
        }

        return File.Exists(ResolveLocalAbsolutePath(relativePath));
    }

    public async Task<long?> GetSizeAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        if (_useAzureBlobStorage)
        {
            var blobPath = NormalizeBlobPath(relativePath);
            var blobClient = _containerClient!.GetBlobClient(blobPath);
            var exists = await blobClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                return null;
            }

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength;
        }

        var localAbsolutePath = ResolveLocalAbsolutePath(relativePath);
        if (!File.Exists(localAbsolutePath))
        {
            return null;
        }

        return new FileInfo(localAbsolutePath).Length;
    }

    public async Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        if (_useAzureBlobStorage)
        {
            var blobPath = NormalizeBlobPath(relativePath);
            var blobClient = _containerClient!.GetBlobClient(blobPath);
            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return response.Value;
        }

        var localAbsolutePath = ResolveLocalAbsolutePath(relativePath);
        if (!File.Exists(localAbsolutePath))
        {
            return false;
        }

        File.Delete(localAbsolutePath);
        return true;
    }

    private string BuildBlobPath(string fileName)
    {
        var prefix = (_options.DocumentsPrefix ?? string.Empty).Trim().Trim('/');
        return string.IsNullOrWhiteSpace(prefix)
            ? fileName
            : $"{prefix}/{fileName}";
    }

    private static string NormalizeBlobPath(string relativePath)
    {
        return (relativePath ?? string.Empty).Trim().TrimStart('/').Replace("\\", "/");
    }

    private string BuildLocalRelativeRoot()
    {
        var prefix = NormalizeBlobPath(_options.DocumentsPrefix ?? string.Empty);
        return string.IsNullOrWhiteSpace(prefix) ? Path.Combine("uploads", "documents") : prefix.Replace("/", Path.DirectorySeparatorChar.ToString());
    }

    private string BuildLocalRelativePath(string fileName)
    {
        var prefix = NormalizeBlobPath(_options.DocumentsPrefix ?? string.Empty);
        return string.IsNullOrWhiteSpace(prefix) ? fileName : $"{prefix}/{fileName}";
    }

    private string ResolveLocalAbsolutePath(string relativePath)
    {
        var normalized = NormalizeBlobPath(relativePath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return _localRootPath;
        }

        var prefix = NormalizeBlobPath(_options.DocumentsPrefix ?? string.Empty);
        var relativeWithoutPrefix = !string.IsNullOrWhiteSpace(prefix) &&
                                    normalized.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase)
            ? normalized[(prefix.Length + 1)..]
            : normalized;

        var localRelative = relativeWithoutPrefix.Replace("/", Path.DirectorySeparatorChar.ToString());
        return Path.Combine(_localRootPath, localRelative);
    }

    private static string ResolveContentType(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };
}
