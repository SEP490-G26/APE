using Microsoft.Extensions.Configuration;

namespace SchemaMigration;

internal sealed record MigrationConfiguration(
    string ApiDirectoryPath,
    string DatabaseName,
    string? EnvironmentName,
    IConfigurationRoot RootConfiguration)
{
    public string ConnectionString =>
        RootConfiguration.GetConnectionString("MongoDb")
        ?? throw new InvalidOperationException("ConnectionStrings:MongoDb is not configured.");

    public static MigrationConfiguration Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var apiDirectoryPath = Path.Combine(workspaceRoot, "API");
        if (!Directory.Exists(apiDirectoryPath))
        {
            throw new DirectoryNotFoundException($"API directory was not found at '{apiDirectoryPath}'.");
        }

        var environmentName =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectoryPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddUserSecrets<MigrationConfiguration>(optional: true, reloadOnChange: false)
            .Build();

        var databaseName = configuration["DatabaseName"]
            ?? throw new InvalidOperationException("DatabaseName is not configured.");

        return new MigrationConfiguration(apiDirectoryPath, databaseName, environmentName, configuration);
    }
}
