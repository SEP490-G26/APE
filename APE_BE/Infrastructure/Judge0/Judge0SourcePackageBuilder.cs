// Infrastructure/Judge0/Judge0SourcePackageBuilder.cs

using System.IO.Compression;
using System.Text;
using Application.Exceptions;
using Domain.Entities;
using Microsoft.Extensions.Options;

namespace Infrastructure.Judge0;

internal sealed class Judge0SourcePackageBuilder
    : IJudge0SourcePackageBuilder
{
    private readonly Judge0Options _options;
    private readonly IJudge0MultiFileProfileResolver _profileResolver;

    public Judge0SourcePackageBuilder(
        IOptions<Judge0Options> options,
        IJudge0MultiFileProfileResolver profileResolver)
    {
        _options = options.Value;
        _profileResolver = profileResolver;
    }

    public Judge0SourcePackage Build(
        int originalLanguageId,
        IReadOnlyCollection<CodeFile> sourceFiles)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);

        if (sourceFiles.Count == 0)
        {
            throw new CodeExecutionClientException(
                "At least one source file is required.",
                isTransient: false);
        }

        if (sourceFiles.Count > _options.MaximumSourceFiles)
        {
            throw new CodeExecutionClientException(
                $"The submission contains {sourceFiles.Count} files. " +
                $"The maximum allowed is {_options.MaximumSourceFiles}.",
                isTransient: false);
        }

        ValidateSourceFiles(sourceFiles);

        return sourceFiles.Count == 1
            ? BuildSingleFilePackage(
                originalLanguageId,
                sourceFiles.Single())
            : BuildMultiFilePackage(
                originalLanguageId,
                sourceFiles);
    }

    private static Judge0SourcePackage BuildSingleFilePackage(
        int originalLanguageId,
        CodeFile sourceFile)
    {
        return new Judge0SourcePackage(
            Judge0LanguageId: originalLanguageId,
            SourceCodeBase64: EncodeBase64(sourceFile.Content),
            AdditionalFilesBase64: null);
    }

    private Judge0SourcePackage BuildMultiFilePackage(
        int originalLanguageId,
        IReadOnlyCollection<CodeFile> sourceFiles)
    {
        var profile = _profileResolver.Resolve(originalLanguageId);

        if (profile.RequiredEntryFile is not null &&
            !sourceFiles.Any(file =>
                string.Equals(
                    NormalizePath(file.Filename),
                    profile.RequiredEntryFile,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new CodeExecutionClientException(
                $"Multi-file submission for LanguageId " +
                $"{originalLanguageId} requires " +
                $"'{profile.RequiredEntryFile}'.",
                isTransient: false);
        }

        using var archiveStream = new MemoryStream();

        using (var archive = new ZipArchive(
                   archiveStream,
                   ZipArchiveMode.Create,
                   leaveOpen: true))
        {
            foreach (var sourceFile in sourceFiles)
            {
                AddTextEntry(
                    archive,
                    NormalizePath(sourceFile.Filename),
                    sourceFile.Content,
                    executable: false);
            }

            AddTextEntry(
                archive,
                "compile",
                profile.CompileScript,
                executable: true);

            AddTextEntry(
                archive,
                "run",
                profile.RunScript,
                executable: true);
        }

        if (archiveStream.Length > _options.MaximumArchiveBytes)
        {
            throw new CodeExecutionClientException(
                $"The generated source archive is " +
                $"{archiveStream.Length} bytes. " +
                $"The maximum allowed is " +
                $"{_options.MaximumArchiveBytes} bytes.",
                isTransient: false);
        }

        return new Judge0SourcePackage(
            Judge0LanguageId: _options.MultiFileLanguageId,
            SourceCodeBase64: null,
            AdditionalFilesBase64:
                Convert.ToBase64String(archiveStream.ToArray()));
    }

    private static void ValidateSourceFiles(
        IReadOnlyCollection<CodeFile> sourceFiles)
    {
        var normalizedPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var sourceFile in sourceFiles)
        {
            if (sourceFile is null)
            {
                throw new CodeExecutionClientException(
                    "Source files cannot contain null items.",
                    isTransient: false);
            }

            if (string.IsNullOrWhiteSpace(sourceFile.Filename))
            {
                throw new CodeExecutionClientException(
                    "Source filename is required.",
                    isTransient: false);
            }

            if (sourceFile.Content is null)
            {
                throw new CodeExecutionClientException(
                    $"Source content is required for " +
                    $"'{sourceFile.Filename}'.",
                    isTransient: false);
            }

            var normalizedPath = NormalizePath(sourceFile.Filename);

            if (!normalizedPaths.Add(normalizedPath))
            {
                throw new CodeExecutionClientException(
                    $"Duplicate source filename: '{normalizedPath}'.",
                    isTransient: false);
            }
        }
    }

    private static string NormalizePath(string filename)
    {
        var path = filename
            .Trim()
            .Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new CodeExecutionClientException(
                "Source filename is required.",
                isTransient: false);
        }

        if (path.StartsWith('/') ||
            Path.IsPathRooted(path) ||
            path.Contains(':'))
        {
            throw new CodeExecutionClientException(
                $"Absolute source path is not allowed: '{filename}'.",
                isTransient: false);
        }

        var segments = path.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 ||
            segments.Any(segment =>
                segment is "." or ".."))
        {
            throw new CodeExecutionClientException(
                $"Invalid source path: '{filename}'.",
                isTransient: false);
        }

        return string.Join('/', segments);
    }

    private static void AddTextEntry(
        ZipArchive archive,
        string entryName,
        string content,
        bool executable)
    {
        var entry = archive.CreateEntry(
            entryName,
            CompressionLevel.Optimal);

        if (executable)
        {
            entry.ExternalAttributes = 0x81ED << 16;
        }

        using var stream = entry.Open();
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        writer.Write(content.Replace("\r\n", "\n"));
    }

    private static string EncodeBase64(string value)
    {
        return Convert.ToBase64String(
            Encoding.UTF8.GetBytes(value));
    }
}