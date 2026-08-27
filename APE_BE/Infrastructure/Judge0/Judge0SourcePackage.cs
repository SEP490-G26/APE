namespace Infrastructure.Judge0;

internal sealed record Judge0SourcePackage(
    int Judge0LanguageId,
    string? SourceCodeBase64,
    string? AdditionalFilesBase64);
