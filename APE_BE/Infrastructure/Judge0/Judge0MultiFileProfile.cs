namespace Infrastructure.Judge0;

internal sealed record Judge0MultiFileProfile(
    string CompileScript,
    string RunScript,
    string? RequiredEntryFile);