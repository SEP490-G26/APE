using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Application.DTOs;
using Application.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace Application.Services;

public class FileExtractionService : IFileExtractionService
{
    private const int PreviewTextLimit = 2500;
    private const double PdfLineTolerance = 4d;
    private const double PdfMinimumImageAreaRatio = 0.005d;
    private const double PdfMinimumImageWidthPoints = 36d;
    private const double PdfMinimumImageHeightPoints = 36d;
    private const int PdfMinimumImageSampleWidth = 48;
    private const int PdfMinimumImageSampleHeight = 48;
    private const double PdfDecorativeEdgeMarginRatio = 0.08d;
    private const double PdfDecorativeMaxAreaRatio = 0.02d;
    private static readonly Regex ImagePlaceholderRegex = new(@"\[\[IMAGE:image-\d+\|ref=[^\]]+\]\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<ExtractionResultDto> ExtractPreviewTextAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var full = await ExtractTextAsync(fileStream, fileName, cancellationToken);
        var preview = SamplePreviewText(full.RawText);

        return new ExtractionResultDto
        {
            FileName = full.FileName,
            FileType = full.FileType,
            RawText = preview,
            SourceType = full.SourceType,
            IsVisionRecommended = full.IsVisionRecommended,
            ParserName = $"{full.ParserName}-preview",
            EstimatedPageCount = Math.Min(full.EstimatedPageCount, 3),
            DetectedImageReferences = full.DetectedImageReferences.Take(5).ToList(),
            ExtractedImages = full.ExtractedImages,
            EmbeddedImageCount = full.EmbeddedImageCount,
            Warnings = full.Warnings
        };
    }

    public async Task<ExtractionResultDto> ExtractTextAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        var ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? string.Empty;
        return ext switch
        {
            ".txt" => await ExtractTxtAsync(fileStream, fileName),
            ".docx" => await ExtractDocxAsync(fileStream, fileName),
            ".pptx" => await ExtractPptxAsync(fileStream, fileName),
            ".pdf" => await ExtractPdfAsync(fileStream, fileName),
            _ => throw new ArgumentException("Unsupported file type.")
        };
    }

    private static async Task<ExtractionResultDto> ExtractTxtAsync(Stream fileStream, string fileName)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, true, 1024, leaveOpen: true);
        var text = await reader.ReadToEndAsync();

        return new ExtractionResultDto
        {
            FileName = fileName,
            FileType = ".txt",
            RawText = text,
            SourceType = "text",
            IsVisionRecommended = false,
            ParserName = "plain-text-parser",
            EstimatedPageCount = 1
        };
    }

    private static async Task<ExtractionResultDto> ExtractDocxAsync(Stream fileStream, string fileName)
    {
        using var memory = new MemoryStream();
        await fileStream.CopyToAsync(memory);
        memory.Position = 0;

        using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        var relationshipMap = BuildRelationshipMap(archive, "word/_rels/document.xml.rels", "word/");
        var entry = archive.GetEntry("word/document.xml")
                    ?? throw new InvalidOperationException("DOCX document.xml not found.");

        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8);
        var xmlContent = await reader.ReadToEndAsync();

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlContent);
        var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
        namespaceManager.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");

        var paragraphs = xmlDoc.SelectNodes("//w:p", namespaceManager);
        var lines = new List<string>();
        var imageRefs = new List<string>();
        var extractedImages = new List<AIImageInput>();
        var imageCounter = 0;

        if (paragraphs is not null)
        {
            foreach (XmlNode paragraph in paragraphs)
            {
                var lineParts = new List<string>();
                foreach (XmlNode child in paragraph.ChildNodes)
                {
                    AppendDocxNodeText(child, namespaceManager, archive, relationshipMap, lineParts, imageRefs, extractedImages, ref imageCounter);
                }

                var line = string.Join(" ", lineParts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }
        }

        return new ExtractionResultDto
        {
            FileName = fileName,
            FileType = ".docx",
            RawText = string.Join(Environment.NewLine, lines),
            SourceType = "docx_text",
            IsVisionRecommended = imageRefs.Count > 0,
            ParserName = "docx-xml-parser",
            EstimatedPageCount = Math.Max(1, lines.Count / 40),
            DetectedImageReferences = imageRefs,
            ExtractedImages = extractedImages,
            EmbeddedImageCount = imageRefs.Count,
            Warnings = imageRefs.Count > 0
                ? new List<string> { "Document contains image placeholders that should be enriched by vision to fully preserve diagram meaning." }
                : new List<string>()
        };
    }

    private static async Task<ExtractionResultDto> ExtractPptxAsync(Stream fileStream, string fileName)
    {
        using var memory = new MemoryStream();
        await fileStream.CopyToAsync(memory);
        memory.Position = 0;

        using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        var slideEntries = archive.Entries
            .Where(entry => Regex.IsMatch(entry.FullName, @"^ppt/slides/slide\d+\.xml$", RegexOptions.IgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var slides = new List<string>();
        var imageRefs = new List<string>();
        var extractedImages = new List<AIImageInput>();
        var imageCounter = 0;
        foreach (var entry in slideEntries)
        {
            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);
            var xmlContent = await reader.ReadToEndAsync();
            var slideNumber = slides.Count + 1;
            var relationshipMap = BuildRelationshipMap(archive, GetPptxSlideRelsPath(entry.FullName), "ppt/slides/");
            var content = ExtractPptxSlideContent(archive, xmlContent, slideNumber, relationshipMap, imageRefs, extractedImages, ref imageCounter);

            if (!string.IsNullOrWhiteSpace(content))
            {
                slides.Add(content);
            }
        }

        return new ExtractionResultDto
        {
            FileName = fileName,
            FileType = ".pptx",
            RawText = string.Join(Environment.NewLine + Environment.NewLine, slides),
            SourceType = "pptx_text",
            IsVisionRecommended = imageRefs.Count > 0,
            ParserName = "pptx-xml-parser",
            EstimatedPageCount = Math.Max(1, slideEntries.Count),
            DetectedImageReferences = imageRefs,
            ExtractedImages = extractedImages,
            EmbeddedImageCount = imageRefs.Count,
            Warnings = imageRefs.Count > 0
                ? new List<string> { "Presentation contains image placeholders that should be enriched by vision to fully preserve slide diagrams." }
                : new List<string>()
        };
    }

    private static async Task<ExtractionResultDto> ExtractPdfAsync(Stream fileStream, string fileName)
    {
        using var memory = new MemoryStream();
        await fileStream.CopyToAsync(memory);
        var bytes = memory.ToArray();
        var extractedImages = new List<AIImageInput>();
        var imageRefs = new List<string>();
        var imageCounter = 0;
        var pageCount = 0;
        var warnings = new List<string>();
        string raw;

        try
        {
            raw = ExtractPdfLayoutAwareText(bytes, extractedImages, imageRefs, warnings, ref imageCounter, out pageCount);
        }
        catch
        {
            raw = string.Empty;
            pageCount = 0;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = ExtractPrintableText(bytes);
        }

        raw = Regex.Replace(raw, @"[ \t]{2,}", " ");
        raw = Regex.Replace(raw, @"(\r?\n){3,}", $"{Environment.NewLine}{Environment.NewLine}").Trim();

        if (string.IsNullOrWhiteSpace(raw) || raw.Length < 80)
        {
            warnings.Add("Low-confidence PDF text extraction. A vision/OCR fallback may still be required for scanned PDFs.");
        }

        var embeddedImageCount = extractedImages.Count > 0 ? extractedImages.Count : EstimatePdfImageCount(bytes);
        if (embeddedImageCount > 0)
        {
            warnings.Add("PDF contains embedded images. Runtime extraction now preserves page-aware image placeholders for phase 1 positional vision reinjection.");
        }

        return new ExtractionResultDto
        {
            FileName = fileName,
            FileType = ".pdf",
            RawText = raw,
            SourceType = extractedImages.Count > 0 ? "pdf_layout_text" : "pdf_text",
            IsVisionRecommended = warnings.Count > 0 || embeddedImageCount > 0,
            ParserName = extractedImages.Count > 0 ? "pdf-layout-parser" : "pdf-basic-text-parser",
            EstimatedPageCount = Math.Max(1, pageCount > 0 ? pageCount : Regex.Matches(raw, @"(?im)^\[page\s+\d+\]").Count),
            DetectedImageReferences = imageRefs,
            ExtractedImages = extractedImages,
            EmbeddedImageCount = embeddedImageCount,
            Warnings = warnings
        };
    }

    private static void AppendDocxNodeText(
        XmlNode node,
        XmlNamespaceManager namespaceManager,
        ZipArchive archive,
        IReadOnlyDictionary<string, string> relationshipMap,
        List<string> output,
        List<string> imageRefs,
        List<AIImageInput> extractedImages,
        ref int imageCounter)
    {
        if (node.Name == "w:r")
        {
            var texts = node.SelectNodes(".//w:t", namespaceManager);
            if (texts is not null)
            {
                var text = string.Concat(texts.Cast<XmlNode>().Select(item => item.InnerText));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    output.Add(text.Trim());
                }
            }

            var drawings = node.SelectNodes(".//w:drawing", namespaceManager);
            if (drawings is not null)
            {
                foreach (XmlNode drawing in drawings)
                {
                    var placeholder = BuildDocxImagePlaceholder(drawing, namespaceManager, archive, relationshipMap, imageRefs, extractedImages, ref imageCounter);
                    if (!string.IsNullOrWhiteSpace(placeholder))
                    {
                        output.Add(placeholder);
                    }
                }
            }

            return;
        }

        foreach (XmlNode child in node.ChildNodes)
        {
            AppendDocxNodeText(child, namespaceManager, archive, relationshipMap, output, imageRefs, extractedImages, ref imageCounter);
        }
    }

    private static string? BuildDocxImagePlaceholder(
        XmlNode drawingNode,
        XmlNamespaceManager namespaceManager,
        ZipArchive archive,
        IReadOnlyDictionary<string, string> relationshipMap,
        List<string> imageRefs,
        List<AIImageInput> extractedImages,
        ref int imageCounter)
    {
        var docPr = drawingNode.SelectSingleNode(".//*[local-name()='docPr']", namespaceManager);
        var blip = drawingNode.SelectSingleNode(".//*[local-name()='blip']", namespaceManager);

        var name = docPr?.Attributes?["name"]?.Value;
        var descr = docPr?.Attributes?["descr"]?.Value;
        var embed = blip?.Attributes?["r:embed"]?.Value ?? blip?.Attributes?["embed"]?.Value;

        imageCounter += 1;
        var reference = !string.IsNullOrWhiteSpace(descr)
            ? descr!
            : !string.IsNullOrWhiteSpace(name)
                ? name!
                : !string.IsNullOrWhiteSpace(embed)
                    ? embed!
                    : $"docx-image-{imageCounter:000}";

        imageRefs.Add(reference);
        if (!string.IsNullOrWhiteSpace(embed) &&
            relationshipMap.TryGetValue(embed!, out var targetPath))
        {
            var asset = BuildImageInputFromArchive(archive, targetPath, $"image-{imageCounter:000}", reference, name);
            if (asset is not null)
            {
                extractedImages.Add(asset);
            }
        }

        return BuildImagePlaceholder(imageCounter, reference, name, null, null);
    }

    private static string ExtractPptxSlideContent(
        ZipArchive archive,
        string xmlContent,
        int slideNumber,
        IReadOnlyDictionary<string, string> relationshipMap,
        List<string> imageRefs,
        List<AIImageInput> extractedImages,
        ref int imageCounter)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlContent);
        var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
        namespaceManager.AddNamespace("p", "http://schemas.openxmlformats.org/presentationml/2006/main");
        namespaceManager.AddNamespace("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

        var nodes = xmlDoc.SelectNodes("//p:spTree/*", namespaceManager);
        var lines = new List<string> { $"[Slide {slideNumber}]" };

        if (nodes is not null)
        {
            foreach (XmlNode node in nodes)
            {
                if (string.Equals(node.Name, "p:sp", StringComparison.OrdinalIgnoreCase))
                {
                    var texts = node.SelectNodes(".//a:t", namespaceManager);
                    var text = string.Join(Environment.NewLine,
                        texts?.Cast<XmlNode>()
                            .Select(item => System.Net.WebUtility.HtmlDecode(item.InnerText))
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .ToList() ?? new List<string>());

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        lines.Add(text);
                    }

                    continue;
                }

                if (string.Equals(node.Name, "p:pic", StringComparison.OrdinalIgnoreCase))
                {
                    var cNvPr = node.SelectSingleNode(".//p:cNvPr", namespaceManager);
                    var name = cNvPr?.Attributes?["name"]?.Value;
                    var descr = cNvPr?.Attributes?["descr"]?.Value;
                    var blip = node.SelectSingleNode(".//*[local-name()='blip']", namespaceManager);
                    var embed = blip?.Attributes?["r:embed"]?.Value ?? blip?.Attributes?["embed"]?.Value;
                    var reference = !string.IsNullOrWhiteSpace(descr)
                        ? descr!
                        : !string.IsNullOrWhiteSpace(name)
                            ? name!
                            : $"pptx-image-slide-{slideNumber}-{imageCounter + 1:000}";

                    imageCounter += 1;
                    imageRefs.Add(reference);
                    if (!string.IsNullOrWhiteSpace(embed) &&
                        relationshipMap.TryGetValue(embed!, out var targetPath))
                    {
                        var asset = BuildImageInputFromArchive(archive, targetPath, $"image-{imageCounter:000}", reference, name);
                        if (asset is not null)
                        {
                            extractedImages.Add(asset);
                        }
                    }

                    lines.Add(BuildImagePlaceholder(imageCounter, reference, name, slideNumber, null));
                }
            }
        }

        return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string BuildImagePlaceholder(int imageCounter, string reference, string? label, int? slideNumber, int? pageNumber)
    {
        var safeReference = Regex.Replace(reference, @"[\r\n\|\]]+", " ").Trim();
        var safeLabel = Regex.Replace(label ?? reference, @"[\r\n\]]+", " ").Trim();
        var slidePart = slideNumber.HasValue ? $"|slide={slideNumber.Value}" : string.Empty;
        var pagePart = pageNumber.HasValue ? $"|page={pageNumber.Value}" : string.Empty;
        return $"[[IMAGE:image-{imageCounter:000}|ref={safeReference}|label={safeLabel}{slidePart}{pagePart}]]";
    }

    private static IReadOnlyDictionary<string, string> BuildRelationshipMap(ZipArchive archive, string relsPath, string baseDirectory)
    {
        var entry = archive.GetEntry(relsPath);
        if (entry is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var xmlContent = reader.ReadToEnd();
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlContent);

        var relationships = xmlDoc.SelectNodes("//*[local-name()='Relationship']");
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (relationships is null)
        {
            return result;
        }

        foreach (XmlNode relationship in relationships)
        {
            var id = relationship.Attributes?["Id"]?.Value;
            var target = relationship.Attributes?["Target"]?.Value;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            result[id] = NormalizeZipPath(baseDirectory, target);
        }

        return result;
    }

    private static string GetPptxSlideRelsPath(string slidePath)
    {
        var directory = Path.GetDirectoryName(slidePath)?.Replace('\\', '/') ?? "ppt/slides";
        var fileName = Path.GetFileName(slidePath);
        return $"{directory}/_rels/{fileName}.rels";
    }

    private static string NormalizeZipPath(string baseDirectory, string target)
    {
        var baseSegments = baseDirectory.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var targetSegments = target.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in targetSegments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (baseSegments.Count > 0)
                {
                    baseSegments.RemoveAt(baseSegments.Count - 1);
                }

                continue;
            }

            baseSegments.Add(segment);
        }

        return string.Join('/', baseSegments);
    }

    private static AIImageInput? BuildImageInputFromArchive(
        ZipArchive archive,
        string entryPath,
        string imageId,
        string reference,
        string? label)
    {
        var entry = archive.GetEntry(entryPath);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        return new AIImageInput
        {
            ImageId = imageId,
            MimeType = InferMimeType(entry.FullName),
            Base64Data = Convert.ToBase64String(memory.ToArray()),
            Reference = reference,
            Label = label
        };
    }

    private static string InferMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "image/png"
        };
    }

    private static int EstimatePdfImageCount(byte[] bytes)
    {
        var text = Encoding.ASCII.GetString(bytes);
        var matches = Regex.Matches(text, @"(?i)/Subtype\s*/Image");
        return matches.Count;
    }

    private static string ExtractPrintableText(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length / 2);
        var current = new StringBuilder();

        foreach (var b in bytes)
        {
            if (b is 9 or 10 or 13 || (b >= 32 && b <= 126))
            {
                current.Append((char)b);
                continue;
            }

            FlushCurrent();
        }

        FlushCurrent();
        return builder.ToString();

        void FlushCurrent()
        {
            if (current.Length < 4)
            {
                current.Clear();
                return;
            }

            var value = current.ToString();
            if (value.Contains("BT", StringComparison.Ordinal) ||
                value.Contains("ET", StringComparison.Ordinal) ||
                value.Contains("/Font", StringComparison.Ordinal))
            {
                current.Clear();
                return;
            }

            builder.AppendLine(value);
            current.Clear();
        }
    }

    private static string SamplePreviewText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(rawText, @"\s+", " ").Trim();
        if (normalized.Length <= PreviewTextLimit)
        {
            return normalized;
        }

        var headLength = Math.Min(900, normalized.Length);
        var middleStart = Math.Max(0, (normalized.Length / 2) - 350);
        var middleLength = Math.Min(700, normalized.Length - middleStart);
        var tailLength = Math.Min(700, normalized.Length);

        var head = normalized[..headLength];
        var middle = normalized.Substring(middleStart, middleLength);
        var tail = normalized[^tailLength..];

        return $"{head}{Environment.NewLine}{Environment.NewLine}[...preview-snippet...]{Environment.NewLine}{Environment.NewLine}{middle}{Environment.NewLine}{Environment.NewLine}[...preview-snippet...]{Environment.NewLine}{Environment.NewLine}{tail}";
    }

    private static string ExtractPdfLayoutAwareText(
        byte[] bytes,
        List<AIImageInput> extractedImages,
        List<string> imageRefs,
        List<string> warnings,
        ref int imageCounter,
        out int pageCount)
    {
        using var document = PdfDocument.Open(bytes);
        pageCount = document.NumberOfPages;
        var pageOutputs = new List<string>();

        foreach (var page in document.GetPages())
        {
            var blocks = new List<PdfContentBlock>();
            blocks.AddRange(ExtractPdfTextBlocks(page));
            blocks.AddRange(ExtractPdfImageBlocks(page, extractedImages, imageRefs, warnings, ref imageCounter));

            var orderedBlocks = blocks
                .OrderByDescending(block => block.Top)
                .ThenBy(block => block.Left)
                .ToList();

            var pageLines = new List<string> { $"[Page {page.Number}]" };
            foreach (var block in orderedBlocks)
            {
                if (!string.IsNullOrWhiteSpace(block.Content))
                {
                    pageLines.Add(block.Content);
                }
            }

            if (pageLines.Count > 1)
            {
                pageOutputs.Add(string.Join(Environment.NewLine, pageLines));
            }
        }

        return string.Join(Environment.NewLine + Environment.NewLine, pageOutputs).Trim();
    }

    private static List<PdfContentBlock> ExtractPdfTextBlocks(Page page)
    {
        var words = page.GetWords()
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .OrderByDescending(word => word.BoundingBox.Top)
            .ThenBy(word => word.BoundingBox.Left)
            .ToList();

        var blocks = new List<PdfContentBlock>();
        if (words.Count == 0)
        {
            return blocks;
        }

        var currentLine = new List<Word>();
        double? currentBaseline = null;
        foreach (var word in words)
        {
            var baseline = word.BoundingBox.Bottom;
            if (currentBaseline is null || Math.Abs(currentBaseline.Value - baseline) <= PdfLineTolerance)
            {
                currentLine.Add(word);
                currentBaseline ??= baseline;
                continue;
            }

            AddPdfTextBlock(currentLine, blocks);
            currentLine = new List<Word> { word };
            currentBaseline = baseline;
        }

        AddPdfTextBlock(currentLine, blocks);
        return blocks;
    }

    private static void AddPdfTextBlock(List<Word> words, List<PdfContentBlock> blocks)
    {
        if (words.Count == 0)
        {
            return;
        }

        var orderedWords = words.OrderBy(word => word.BoundingBox.Left).ToList();
        var text = string.Join(" ", orderedWords.Select(word => word.Text)).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var left = orderedWords.Min(word => word.BoundingBox.Left);
        var top = orderedWords.Max(word => word.BoundingBox.Top);
        blocks.Add(new PdfContentBlock(text, left, top));
    }

    private static List<PdfContentBlock> ExtractPdfImageBlocks(
        Page page,
        List<AIImageInput> extractedImages,
        List<string> imageRefs,
        List<string> warnings,
        ref int imageCounter)
    {
        var blocks = new List<PdfContentBlock>();
        var pageArea = Math.Max(1d, page.Width * page.Height);
        var pageImageIndex = 0;

        foreach (var image in page.GetImages())
        {
            var normalizedBounds = NormalizeRectangle(image.Bounds);
            var imageWidth = Math.Abs(normalizedBounds.Right - normalizedBounds.Left);
            var imageHeight = Math.Abs(normalizedBounds.Top - normalizedBounds.Bottom);
            var imageArea = Math.Abs(imageWidth * imageHeight);
            var imageAreaRatio = imageArea / pageArea;

            if (ShouldSkipPdfImage(page, image, normalizedBounds, imageWidth, imageHeight, imageAreaRatio))
            {
                continue;
            }

            pageImageIndex += 1;
            imageCounter += 1;
            var reference = $"pdf-page-{page.Number:000}-image-{pageImageIndex:000}";
            var label = $"PDF Page {page.Number} Image {pageImageIndex}";

            var asset = BuildPdfImageInput(image, $"image-{imageCounter:000}", reference, label);
            if (asset is null)
            {
                warnings.Add($"PDF image on page {page.Number} could not be converted to a supported runtime payload.");
                continue;
            }

            extractedImages.Add(asset);
            imageRefs.Add(reference);
            var placeholder = BuildImagePlaceholder(imageCounter, reference, label, null, page.Number);
            blocks.Add(new PdfContentBlock(placeholder, normalizedBounds.Left, normalizedBounds.Top));
        }

        return blocks;
    }

    private static AIImageInput? BuildPdfImageInput(IPdfImage image, string imageId, string reference, string label)
    {
        if (image.TryGetPng(out var pngBytes) && pngBytes is { Length: > 0 })
        {
            return new AIImageInput
            {
                ImageId = imageId,
                MimeType = "image/png",
                Base64Data = Convert.ToBase64String(pngBytes),
                Reference = reference,
                Label = label,
                Width = image.WidthInSamples,
                Height = image.HeightInSamples
            };
        }

        if (TryInferPdfImagePayload(image.RawBytes, out var mimeType) && image.RawBytes is { Count: > 0 })
        {
            return new AIImageInput
            {
                ImageId = imageId,
                MimeType = mimeType,
                Base64Data = Convert.ToBase64String(image.RawBytes.ToArray()),
                Reference = reference,
                Label = label,
                Width = image.WidthInSamples,
                Height = image.HeightInSamples
            };
        }

        return null;
    }

    private static bool ShouldSkipPdfImage(
        Page page,
        IPdfImage image,
        PdfRectangle bounds,
        double imageWidth,
        double imageHeight,
        double imageAreaRatio)
    {
        if (imageAreaRatio < PdfMinimumImageAreaRatio)
        {
            return true;
        }

        if (imageWidth < PdfMinimumImageWidthPoints || imageHeight < PdfMinimumImageHeightPoints)
        {
            return true;
        }

        if (image.WidthInSamples < PdfMinimumImageSampleWidth || image.HeightInSamples < PdfMinimumImageSampleHeight)
        {
            return true;
        }

        var nearTop = bounds.Top >= page.Height * (1d - PdfDecorativeEdgeMarginRatio);
        var nearBottom = bounds.Bottom <= page.Height * PdfDecorativeEdgeMarginRatio;
        var nearLeft = bounds.Left <= page.Width * PdfDecorativeEdgeMarginRatio;
        var nearRight = bounds.Right >= page.Width * (1d - PdfDecorativeEdgeMarginRatio);
        var touchesEdge = nearTop || nearBottom || nearLeft || nearRight;
        if (touchesEdge && imageAreaRatio <= PdfDecorativeMaxAreaRatio)
        {
            return true;
        }

        return false;
    }

    private static bool TryInferPdfImagePayload(IReadOnlyList<byte> bytes, out string mimeType)
    {
        mimeType = "image/png";
        if (bytes.Count < 4)
        {
            return false;
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            mimeType = "image/jpeg";
            return true;
        }

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            mimeType = "image/png";
            return true;
        }

        return false;
    }

    private static PdfRectangle NormalizeRectangle(PdfRectangle rectangle)
    {
        var left = Math.Min(rectangle.Left, rectangle.Right);
        var right = Math.Max(rectangle.Left, rectangle.Right);
        var bottom = Math.Min(rectangle.Bottom, rectangle.Top);
        var top = Math.Max(rectangle.Bottom, rectangle.Top);
        return new PdfRectangle(left, bottom, right, top);
    }

    private sealed record PdfContentBlock(string Content, double Left, double Top);
}
