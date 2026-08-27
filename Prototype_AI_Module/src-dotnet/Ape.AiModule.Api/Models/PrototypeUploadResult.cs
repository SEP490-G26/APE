namespace Ape.AiModule.Api.Models;

public sealed record PrototypeUploadResult(
    string FileName,
    string ContentType,
    long SizeBytes,
    string Extension,
    string FileKind,
    string SavedPath,
    string SuggestedMode,
    string SuggestedRawContent,
    string? ExtractedText,
    string ParserName,
    string ParserVersion,
    IReadOnlyList<string> ExtractedImagePaths,
    bool CanUseAsText,
    bool CanUseAsVisionInput,
    IReadOnlyList<string> Warnings);
