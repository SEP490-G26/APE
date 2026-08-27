using Microsoft.Extensions.Configuration;

namespace BE_IntegrationTests.Infrastructure;

public static class IntegrationTestRun
{
    private const string MongoOverrideEnvironmentVariable = "APE_INTEGRATION_MONGODB";
    private const string IntegrationDatabaseName = IntegrationDatabaseFixture.AllowedDatabaseName;

    private static readonly SemaphoreSlim Sync = new(1, 1);
    private static bool _initialized;

    public static APEWebApplicationFactory Factory { get; private set; } = null!;

    public static IntegrationDatabaseFixture DatabaseFixture { get; private set; } = null!;

    public static string ConnectionSource { get; private set; } = "unknown";

    public static async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await Sync.WaitAsync();

        try
        {
            if (_initialized)
            {
                return;
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "API"))
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var connectionString =
                Environment.GetEnvironmentVariable(MongoOverrideEnvironmentVariable) ??
                configuration.GetConnectionString("MongoDb") ??
                throw new InvalidOperationException("MongoDb connection string is not available for integration tests.");

            ConnectionSource = Environment.GetEnvironmentVariable(MongoOverrideEnvironmentVariable) is not null
                ? MongoOverrideEnvironmentVariable
                : "ConnectionStrings:MongoDb";

            connectionString = NormalizeMongoConnectionString(connectionString);
            var databaseName = IntegrationDatabaseName;

            DatabaseFixture = new IntegrationDatabaseFixture(connectionString, databaseName);
            await DatabaseFixture.VerifyConnectivityAsync();
            await DatabaseFixture.VerifyAuthorizationAsync();

            Factory = new APEWebApplicationFactory(connectionString, databaseName);

            using var client = Factory.CreateClient();
            using var response = await client.GetAsync("/health");
            response.EnsureSuccessStatusCode();

            _initialized = true;
        }
        finally
        {
            Sync.Release();
        }
    }

    public static async Task ResetForTestAsync()
    {
        await InitializeAsync();
        Factory.ExecutionClient.Reset();
        Factory.GoogleTokenValidator.Reset();
        Factory.PayOS.Reset();
        Factory.FileStorage.Reset();
        await DatabaseFixture.ResetAsync();
    }

    public static async Task DisposeAsync()
    {
        if (!_initialized)
        {
            return;
        }

        await DatabaseFixture.DropAsync();
        Factory.Dispose();
        _initialized = false;
    }

    private static string NormalizeMongoConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("MongoDb connection string is not available for integration tests.");
        }

        return connectionString;
    }
}
