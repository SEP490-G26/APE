using Domain.Entities;

namespace Infrastructure.Judge0;

internal interface IJudge0SourcePackageBuilder
{
    Judge0SourcePackage Build(
        int originalLanguageId,
        IReadOnlyCollection<CodeFile> sourceFiles);
}