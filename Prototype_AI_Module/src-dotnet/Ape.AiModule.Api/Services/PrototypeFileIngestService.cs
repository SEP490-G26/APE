using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ape.AiModule.Api.Models;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig;

namespace Ape.AiModule.Api.Services;

public sealed class PrototypeFileIngestService
{
    private readonly string _uploadRoot;

    public PrototypeFileIngestService(IWebHostEnvironment environment)
    {
        _uploadRoot = Path.Combine(environment.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(_uploadRoot);
    }

    public async Task<PrototypeUploadResult> IngestAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var safeFileName = SanitizeFileName(file.FileName);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        var folder = Path.Combine(_uploadRoot, DateTimeOffset.UtcNow.ToString("yyyyMMdd"));
        Directory.CreateDirectory(folder);

        var storedFileName = $"{stamp}-{Guid.NewGuid().ToString("N")[..8]}-{safeFileName}";
        var savedPath = Path.Combine(folder, storedFileName);

        await using (var output = File.Create(savedPath))
        {
            await file.CopyToAsync(output, cancellationToken);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var warnings = new List<string>();
        var fileKind = DetectFileKind(extension, file.ContentType);
        string? extractedText = null;
        var suggestedMode = "TextOnly";
        var suggestedRawContent = string.Empty;
        var canUseAsText = false;
        var canUseAsVisionInput = false;
        var parserName = "prototype-file-ingest";
        var parserVersion = "v3";
        var extractedImagePaths = new List<string>();

        switch (fileKind)
        {
            case "text":
                extractedText = await File.ReadAllTextAsync(savedPath, cancellationToken);
                suggestedRawContent = extractedText;
                canUseAsText = true;
                break;

            case "docx":
                parserName = "openxml-docx-parser";
                extractedText = await ExtractDocxTextAsync(savedPath, cancellationToken);
                extractedImagePaths.AddRange(await ExtractZipMediaAsync(savedPath, folder, storedFileName, "word/media", cancellationToken));
                extractedText = AppendImageMarkers(extractedText, extractedImagePaths);
                suggestedRawContent = extractedText;
                canUseAsText = !string.IsNullOrWhiteSpace(extractedText);
                canUseAsVisionInput = extractedImagePaths.Count > 0;
                if (!canUseAsText)
                {
                    warnings.Add("DOCX file was uploaded, but no readable text was extracted from word/document.xml.");
                }
                break;

            case "pptx":
                parserName = "openxml-pptx-parser";
                extractedText = await ExtractPptxTextAsync(savedPath, cancellationToken);
                extractedImagePaths.AddRange(await ExtractZipMediaAsync(savedPath, folder, storedFileName, "ppt/media", cancellationToken));
                extractedText = AppendImageMarkers(extractedText, extractedImagePaths);
                suggestedRawContent = extractedText;
                canUseAsText = !string.IsNullOrWhiteSpace(extractedText);
                canUseAsVisionInput = extractedImagePaths.Count > 0;
                if (!canUseAsText)
                {
                    warnings.Add("PPTX file was uploaded, but no readable text was extracted from ppt/slides/*.xml.");
                }
                break;

            case "image":
                parserName = "direct-image-upload";
                suggestedMode = "FullMultimodalPage";
                suggestedRawContent = $"[image:{savedPath}]";
                extractedText = null;
                extractedImagePaths.Add(savedPath);
                canUseAsVisionInput = true;
                warnings.Add("Image upload will be passed into the current vision placeholder flow using a server-side saved file path.");
                break;

            case "pdf":
                parserName = "pdfpig-text-image-parser";
                var pdfExtracted = await ExtractPdfContentAsync(savedPath, folder, storedFileName, cancellationToken);
                extractedText = pdfExtracted.Text;
                extractedImagePaths.AddRange(pdfExtracted.ImagePaths);
                extractedText = AppendImageMarkers(extractedText, extractedImagePaths);
                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    suggestedRawContent = extractedText;
                    canUseAsText = true;
                    canUseAsVisionInput = extractedImagePaths.Count > 0;
                    warnings.Add("PDF text was extracted using a lightweight prototype parser. Complex layouts may still be noisy.");
                    if (extractedImagePaths.Count > 0)
                    {
                        warnings.Add("Embedded PDF images were extracted and appended as vision markers for multimodal expansion.");
                    }
                }
                else
                {
                    if (extractedImagePaths.Count > 0)
                    {
                        suggestedMode = "FullMultimodalPage";
                        suggestedRawContent = AppendImageMarkers(string.Empty, extractedImagePaths);
                        canUseAsVisionInput = true;
                        warnings.Add("PDF text extraction returned no readable text, but embedded images were extracted for vision-based expansion.");
                    }
                    else
                    {
                        suggestedRawContent = $"[pdf-uploaded:{savedPath}]";
                        warnings.Add("PDF upload is stored successfully, but no readable text or extractable images were found by the prototype parser.");
                    }
                }
                break;

            default:
                suggestedRawContent = $"[binary-uploaded:{savedPath}]";
                warnings.Add("This file type is stored successfully, but binary extraction is not implemented in this prototype.");
                break;
        }

        if (string.IsNullOrWhiteSpace(suggestedRawContent))
        {
            suggestedRawContent = $"[uploaded-file:{savedPath}]";
        }

        return new PrototypeUploadResult(
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            file.Length,
            extension,
            fileKind,
            savedPath,
            suggestedMode,
            suggestedRawContent,
            extractedText,
            parserName,
            parserVersion,
            extractedImagePaths,
            canUseAsText,
            canUseAsVisionInput,
            warnings);
    }

    private static string DetectFileKind(string extension, string? contentType)
    {
        if (new[] { ".txt", ".md", ".json", ".csv", ".tsv", ".xml", ".html", ".htm", ".c", ".h", ".java", ".cs", ".js", ".ts", ".py", ".log" }.Contains(extension))
        {
            return "text";
        }

        return extension switch
        {
            ".docx" => "docx",
            ".pptx" => "pptx",
            ".pdf" => "pdf",
            ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" => "image",
            _ when (contentType ?? string.Empty).StartsWith("text/", StringComparison.OrdinalIgnoreCase) => "text",
            _ => "binary"
        };
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "uploaded-file.bin" : sanitized;
    }

    private static async Task<string> ExtractDocxTextAsync(string path, CancellationToken cancellationToken)
    {
        await using var fileStream = File.OpenRead(path);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);
        var documentEntry = archive.GetEntry("word/document.xml");
        if (documentEntry is null)
        {
            return string.Empty;
        }

        await using var entryStream = documentEntry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8);
        var xml = await reader.ReadToEndAsync(cancellationToken);
        return NormalizeOpenXmlText(xml, "w:t", "w:p");
    }

    private static async Task<string> ExtractPptxTextAsync(string path, CancellationToken cancellationToken)
    {
        await using var fileStream = File.OpenRead(path);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);
        var slideEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (slideEntries.Count == 0)
        {
            return string.Empty;
        }

        var blocks = new List<string>();
        foreach (var entry in slideEntries)
        {
            await using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);
            var xml = await reader.ReadToEndAsync(cancellationToken);
            var text = NormalizeOpenXmlText(xml, "a:t", "a:p");
            if (!string.IsNullOrWhiteSpace(text))
            {
                blocks.Add($"# {Path.GetFileNameWithoutExtension(entry.Name)}\n{text}");
            }
        }

        return string.Join("\n\n", blocks);
    }

    private static string NormalizeOpenXmlText(string xml, string textElementName, string paragraphElementName)
    {
        try
        {
            var document = XDocument.Parse(xml);
            var paragraphs = document
                .Descendants()
                .Where(node => string.Equals(node.Name.LocalName, paragraphElementName.Split(':').Last(), StringComparison.OrdinalIgnoreCase))
                .Select(paragraph =>
                {
                    var text = string.Concat(
                        paragraph
                            .Descendants()
                            .Where(node => string.Equals(node.Name.LocalName, textElementName.Split(':').Last(), StringComparison.OrdinalIgnoreCase))
                            .Select(node => node.Value));
                    return CollapseWhitespace(WebUtility.HtmlDecode(text));
                })
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            if (paragraphs.Count > 0)
            {
                return string.Join("\n", paragraphs);
            }
        }
        catch
        {
        }

        var newlineInjected = Regex.Replace(xml, @"</[^>]*p>", "\n", RegexOptions.IgnoreCase);
        var stripped = Regex.Replace(newlineInjected, "<[^>]+>", " ");
        return CollapseWhitespace(WebUtility.HtmlDecode(stripped));
    }

    private static string CollapseWhitespace(string value)
    {
        var normalized = value.Replace("\r\n", "\n").Replace("\r", "\n");
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    private static string AppendImageMarkers(string? text, IReadOnlyList<string> imagePaths)
    {
        var baseText = text ?? string.Empty;
        if (imagePaths.Count == 0)
        {
            return baseText;
        }

        var markers = string.Join("\n", imagePaths.Select(static path => $"[image:{path}]"));
        return string.IsNullOrWhiteSpace(baseText) ? markers : $"{baseText}\n\n{markers}";
    }

    private static async Task<IReadOnlyList<string>> ExtractZipMediaAsync(string archivePath, string folder, string storedFileName, string mediaPrefix, CancellationToken cancellationToken)
    {
        var result = new List<string>();
        var mediaFolder = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(storedFileName)}-media");
        Directory.CreateDirectory(mediaFolder);

        await using var fileStream = File.OpenRead(archivePath);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith(mediaPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            var fileName = SanitizeFileName(Path.GetFileName(entry.FullName));
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var outputPath = Path.Combine(mediaFolder, fileName);
            await using var entryStream = entry.Open();
            await using var output = File.Create(outputPath);
            await entryStream.CopyToAsync(output, cancellationToken);
            result.Add(outputPath);
        }

        return result;
    }

    private static async Task<PdfExtractedContent> ExtractPdfContentAsync(string path, string folder, string storedFileName, CancellationToken cancellationToken)
    {
        await Task.Yield();
        using var document = PdfDocument.Open(path);
        var pages = new List<string>();
        var imagePaths = new List<string>();
        var pdfMediaFolder = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(storedFileName)}-pdf-media");
        Directory.CreateDirectory(pdfMediaFolder);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageText = CollapseWhitespace(page.Text);
            if (string.IsNullOrWhiteSpace(pageText))
            {
                if (page.Number == 0)
                {
                    continue;
                }
            }
            else
            {
                pages.Add($"# page-{page.Number}\n{pageText}");
            }

            imagePaths.AddRange(ExtractPdfPageImages(page, pdfMediaFolder));
        }

        return new PdfExtractedContent(
            pages.Count == 0 ? string.Empty : string.Join("\n\n", pages),
            imagePaths);
    }

    private static IReadOnlyList<string> ExtractPdfPageImages(Page page, string outputFolder)
    {
        var result = new List<string>();
        var imageIndex = 0;

        foreach (var image in page.GetImages())
        {
            imageIndex++;
            if (!TryGetPdfImageBytes(image, out var pngBytes))
            {
                continue;
            }

            var outputPath = Path.Combine(outputFolder, $"page-{page.Number:D4}-image-{imageIndex:D2}.png");
            File.WriteAllBytes(outputPath, pngBytes);
            result.Add(outputPath);
        }

        return result;
    }

    private static bool TryGetPdfImageBytes(IPdfImage image, out byte[] pngBytes)
    {
        pngBytes = Array.Empty<byte>();
        try
        {
            if (image.TryGetPng(out var directPng) && directPng is { Length: > 0 })
            {
                pngBytes = directPng;
                return true;
            }
        }
        catch
        {
        }

        try
        {
            if (image.RawBytes is { Length: > 0 } rawBytes)
            {
                pngBytes = rawBytes.ToArray();
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private sealed record PdfExtractedContent(string Text, IReadOnlyList<string> ImagePaths);
}
