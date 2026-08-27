using Application.DTOs;
using Application.Interfaces;

namespace BE_IntegrationTests.External;

public sealed class ControlledFileExtractionService : IFileExtractionService
{
    public Task<ExtractionResultDto> ExtractPreviewTextAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
        => Task.FromResult(BuildResult(fileName, ReadAllText(fileStream)));

    public Task<ExtractionResultDto> ExtractTextAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
        => Task.FromResult(BuildResult(fileName, ReadAllText(fileStream)));

    private static ExtractionResultDto BuildResult(string fileName, string rawText)
    {
        var safeText = string.IsNullOrWhiteSpace(rawText)
            ? "Controlled integration extraction content."
            : rawText.Trim();

        return new ExtractionResultDto
        {
            FileName = fileName,
            FileType = Path.GetExtension(fileName),
            RawText = safeText,
            SourceType = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
            ParserName = "controlled-parser",
            EstimatedPageCount = Math.Max(1, safeText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length)
        };
    }

    private static string ReadAllText(Stream fileStream)
    {
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        using var reader = new StreamReader(fileStream, leaveOpen: true);
        var text = reader.ReadToEnd();
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        return text;
    }
}
