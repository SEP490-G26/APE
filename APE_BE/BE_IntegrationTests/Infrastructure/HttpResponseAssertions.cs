using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BE_IntegrationTests.Infrastructure;

public static class HttpResponseAssertions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions);

        if (payload is null)
        {
            throw new AssertFailedException(
                $"Response body for {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} was empty.");
        }

        return payload;
    }

    public static void AssertStatusCode(
        HttpResponseMessage response,
        HttpStatusCode expected)
    {
        Assert.AreEqual(
            expected,
            response.StatusCode,
            $"Unexpected status for {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}.");
    }
}
