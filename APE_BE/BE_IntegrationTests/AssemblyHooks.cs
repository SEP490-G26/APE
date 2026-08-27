using BE_IntegrationTests.Infrastructure;

namespace BE_IntegrationTests;

[TestClass]
public sealed class AssemblyHooks
{
    [AssemblyInitialize]
    public static async Task InitializeAsync(TestContext _)
    {
        await IntegrationTestRun.InitializeAsync();
    }

    [AssemblyCleanup]
    public static async Task CleanupAsync()
    {
        await IntegrationTestRun.DisposeAsync();
    }
}
