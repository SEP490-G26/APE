using Infrastructure.Data;
using SchemaMigration;

var exitCode = await RunAsync(args);
return exitCode;

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0)
    {
        PrintUsage();
        return 1;
    }

    var command = args[0];
    var requireConfirmation = args.Skip(1).Any(arg =>
        string.Equals(arg, "--confirm-database-update", StringComparison.OrdinalIgnoreCase));

    if (string.Equals(command, "apply", StringComparison.OrdinalIgnoreCase) && !requireConfirmation)
    {
        Console.Error.WriteLine("Apply mode requires --confirm-database-update.");
        return 2;
    }

    if (string.Equals(command, "apply-unused-collections", StringComparison.OrdinalIgnoreCase) && !requireConfirmation)
    {
        Console.Error.WriteLine("apply-unused-collections requires --confirm-database-update.");
        return 2;
    }

    if (string.Equals(command, "apply-course-ai-normalization", StringComparison.OrdinalIgnoreCase) && !requireConfirmation)
    {
        Console.Error.WriteLine("apply-course-ai-normalization requires --confirm-database-update.");
        return 2;
    }

    if (string.Equals(command, "apply-question-bank-document-links", StringComparison.OrdinalIgnoreCase) && !requireConfirmation)
    {
        Console.Error.WriteLine("apply-question-bank-document-links requires --confirm-database-update.");
        return 2;
    }

    MigrationConfiguration configuration;

    try
    {
        configuration = MigrationConfiguration.Load(ResolveWorkspaceRoot());
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }

    try
    {
        var context = new DbContext(configuration.ConnectionString, configuration.DatabaseName);
        var runner = new SchemaMigrationRunner(context);

        return command switch
        {
            "inspect" => await runner.InspectAsync(CancellationToken.None),
            "apply" => await runner.ApplyAsync(CancellationToken.None),
            "inspect-unused-collections" => await runner.InspectUnusedCollectionsAsync(CancellationToken.None),
            "apply-unused-collections" => await runner.ApplyUnusedCollectionsCleanupAsync(CancellationToken.None),
            "inspect-course-ai-normalization" => await runner.InspectCourseAiNormalizationAsync(CancellationToken.None),
            "apply-course-ai-normalization" => await runner.ApplyCourseAiNormalizationAsync(CancellationToken.None),
            "inspect-fe-questions" => await runner.InspectFeQuestionsAsync(CancellationToken.None),
            "inspect-question-bank-document-links" => await runner.InspectQuestionBankDocumentLinksAsync(CancellationToken.None),
            "apply-question-bank-document-links" => await runner.ApplyQuestionBankDocumentLinksAsync(CancellationToken.None),
            _ => HandleUnknownCommand(command)
        };
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static int HandleUnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    PrintUsage();
    return 1;
}

static string ResolveWorkspaceRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, "APE_Core.sln");
        if (File.Exists(candidate))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate the workspace root containing APE_Core.sln.");
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project .\\Tools\\SchemaMigration\\SchemaMigration.csproj -- inspect");
    Console.WriteLine("  dotnet run --project .\\Tools\\SchemaMigration\\SchemaMigration.csproj -- apply --confirm-database-update");
    Console.WriteLine("  dotnet run --project .\\Tools\\SchemaMigration\\SchemaMigration.csproj -- inspect-unused-collections");
    Console.WriteLine("  dotnet run --project .\\Tools\\SchemaMigration\\SchemaMigration.csproj -- apply-unused-collections --confirm-database-update");
    Console.WriteLine("  dotnet run --project .\\Tools\\SchemaMigration\\SchemaMigration.csproj -- inspect-course-ai-normalization");
    Console.WriteLine("  dotnet run --project .\\Tools\\SchemaMigration\\SchemaMigration.csproj -- apply-course-ai-normalization --confirm-database-update");
    Console.WriteLine("  dotnet run --project .\\Tools\\SchemaMigration\\SchemaMigration.csproj -- inspect-fe-questions");
    Console.WriteLine("  dotnet run --project .\\Tools\\SchemaMigration\\SchemaMigration.csproj -- inspect-question-bank-document-links");
    Console.WriteLine("  dotnet run --project .\\Tools\\SchemaMigration\\SchemaMigration.csproj -- apply-question-bank-document-links --confirm-database-update");
}
