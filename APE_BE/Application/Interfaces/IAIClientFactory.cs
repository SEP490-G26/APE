namespace Application.Interfaces;

public interface IAIClientFactory
{
    IAITextClient? GetTextClient(string providerName);
    IAIEmbeddingClient? GetEmbeddingClient(string providerName);
}
