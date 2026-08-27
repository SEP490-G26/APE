using DevDatabaseBootstrap;
using Infrastructure.Data;

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
    var arguments = args.Skip(1).ToArray();
    var workspaceRoot = ResolveWorkspaceRoot();

    BootstrapConfiguration configuration;

    try
    {
        configuration = BootstrapConfiguration.Load(workspaceRoot);
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }

    var safety = BootstrapSafetyGuard.Evaluate(
        command,
        arguments,
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
        configuration.DatabaseName);

    if (!safety.IsAllowed)
    {
        Console.Error.WriteLine(safety.ErrorMessage);
        return 2;
    }

    try
    {
        var dbContext = new DbContext(
            configuration.ConnectionString,
            configuration.DatabaseName);

        var runner = new BootstrapRunner(dbContext);

        return command switch
        {
            "inspect" => await runner.InspectAsync(CancellationToken.None),
            "initialize" => await runner.InitializeAsync(CancellationToken.None),
            "seed-worker-e2e" => await runner.SeedWorkerE2EAsync(CancellationToken.None),
            "verify-worker-e2e" => await runner.VerifyWorkerE2EAsync(CancellationToken.None),
            "clean-worker-e2e" => await runner.CleanWorkerE2EAsync(CancellationToken.None),
            "create-c-worker-submission" => await runner.CreateCWorkerSubmissionAsync(configuration, CancellationToken.None),
            "create-java-worker-submission" => await runner.CreateJavaWorkerSubmissionAsync(configuration, CancellationToken.None),
            "run-java-http-e2e" => await runner.RunJavaHttpE2EAsync(configuration, workspaceRoot, CancellationToken.None),
            "run-java-http-wrong-answer-e2e" => await runner.RunJavaHttpWrongAnswerE2EAsync(configuration, workspaceRoot, CancellationToken.None),
            "run-java-http-runtime-error-e2e" => await runner.RunJavaHttpRuntimeErrorE2EAsync(configuration, workspaceRoot, CancellationToken.None),
            "run-java-http-time-limit-e2e" => await runner.RunJavaHttpTimeLimitE2EAsync(configuration, workspaceRoot, CancellationToken.None),
            "run-java-http-compilation-error-e2e" => await runner.RunJavaHttpCompilationErrorE2EAsync(configuration, workspaceRoot, CancellationToken.None),
            "verify-c-worker-result" => await runner.VerifyCWorkerResultAsync(CancellationToken.None),
            "verify-java-worker-result" => await runner.VerifyJavaWorkerResultAsync(CancellationToken.None),
            "clean-worker-submissions" => await runner.CleanWorkerSubmissionsAsync(CancellationToken.None),
            "seed-ai-artifacts" => await runner.SeedAiArtifactsAsync(configuration, workspaceRoot, CancellationToken.None),
            "seed-ai-agents" => await runner.SeedAiAgentsAsync(configuration, workspaceRoot, CancellationToken.None),
            "test-gatekeeper-dbfirst" => await runner.TestGatekeeperDbFirstAsync(configuration, workspaceRoot, CancellationToken.None),
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

    throw new InvalidOperationException(
        "Could not locate the workspace root containing APE_Core.sln.");
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- inspect --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- initialize --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- seed-worker-e2e --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- verify-worker-e2e --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- clean-worker-e2e --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- create-c-worker-submission --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- create-java-worker-submission --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- run-java-http-e2e --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- run-java-http-wrong-answer-e2e --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- run-java-http-runtime-error-e2e --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- run-java-http-time-limit-e2e --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- run-java-http-compilation-error-e2e --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- verify-c-worker-result --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- verify-java-worker-result --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- clean-worker-submissions --confirm-development-database");
    Console.WriteLine("  dotnet run --project .\\Tools\\DevDatabaseBootstrap\\DevDatabaseBootstrap.csproj -- seed-ai-artifacts --confirm-development-database");
}
