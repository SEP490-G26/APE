using Application.Interfaces;

namespace Application.Services;

public class AIClientFactory : IAIClientFactory
{
    private readonly IReadOnlyDictionary<string, IAITextClient> _textClients;
    private readonly IReadOnlyDictionary<string, IAIEmbeddingClient> _embeddingClients;

    public AIClientFactory(IEnumerable<IAITextClient> textClients, IEnumerable<IAIEmbeddingClient> embeddingClients)
    {
        _textClients = BuildClientMap(textClients);
        _embeddingClients = BuildClientMap(embeddingClients);
    }

    public IAITextClient? GetTextClient(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }

        return _textClients.TryGetValue(providerName, out var client) ? client : null;
    }

    public IAIEmbeddingClient? GetEmbeddingClient(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }

        return _embeddingClients.TryGetValue(providerName, out var client) ? client : null;
    }

    private static IReadOnlyDictionary<string, IAITextClient> BuildClientMap(IEnumerable<IAITextClient> clients)
    {
        var map = new Dictionary<string, IAITextClient>(StringComparer.OrdinalIgnoreCase);
        foreach (var client in clients)
        {
            if (string.IsNullOrWhiteSpace(client.ProviderName))
            {
                continue;
            }

            map[client.ProviderName] = client;
        }

        return map;
    }

    private static IReadOnlyDictionary<string, IAIEmbeddingClient> BuildClientMap(IEnumerable<IAIEmbeddingClient> clients)
    {
        var map = new Dictionary<string, IAIEmbeddingClient>(StringComparer.OrdinalIgnoreCase);
        foreach (var client in clients)
        {
            if (string.IsNullOrWhiteSpace(client.ProviderName))
            {
                continue;
            }

            map[client.ProviderName] = client;
        }

        return map;
    }
}
