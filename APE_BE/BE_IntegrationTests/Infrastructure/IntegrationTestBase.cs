using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infrastructure.Data;

namespace BE_IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    protected static DbContext DbContext => IntegrationTestRun.DatabaseFixture.DbContext;

    protected static IntegrationSeedFactory CreateSeedFactory()
        => new(DbContext);

    [TestInitialize]
    public async Task ResetDatabaseAsync()
    {
        await IntegrationTestRun.ResetForTestAsync();
    }

    protected HttpClient CreateClient(TestIdentity identity)
        => IntegrationTestRun.Factory.CreateAuthenticatedClient(identity);

    protected HttpClient CreateAnonymousClient()
        => IntegrationTestRun.Factory.CreateClient(new() { AllowAutoRedirect = false });

    protected static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.IsNotNull(payload);
        return payload;
    }
}
