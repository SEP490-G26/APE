using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Reflection;

namespace DevDatabaseBootstrap;

internal sealed record ProductionCollectionInfo(
    string ContextPropertyName,
    string CollectionName,
    Type DocumentType);

internal static class BootstrapIntrospection
{
    public static IReadOnlyList<ProductionCollectionInfo>
        GetProductionCollections(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return typeof(DbContext)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                property.PropertyType.IsGenericType &&
                property.PropertyType.GetGenericTypeDefinition() == typeof(IMongoCollection<>))
            .Select(property =>
            {
                var collection = property.GetValue(context)
                    ?? throw new InvalidOperationException(
                        $"DbContext property '{property.Name}' returned null.");

                var collectionNamespaceProperty =
                    collection.GetType().GetProperty("CollectionNamespace")
                    ?? throw new InvalidOperationException(
                        $"Collection '{property.Name}' does not expose CollectionNamespace.");

                var collectionNamespace =
                    collectionNamespaceProperty.GetValue(collection)
                    ?? throw new InvalidOperationException(
                        $"Collection '{property.Name}' returned a null CollectionNamespace.");

                var collectionNameProperty =
                    collectionNamespace.GetType().GetProperty("CollectionName")
                    ?? throw new InvalidOperationException(
                        $"CollectionNamespace for '{property.Name}' does not expose CollectionName.");

                var name = (string?)collectionNameProperty.GetValue(collectionNamespace)
                    ?? throw new InvalidOperationException(
                        $"Collection '{property.Name}' returned a null collection name.");

                return new ProductionCollectionInfo(
                    property.Name,
                    name,
                    property.PropertyType.GetGenericArguments()[0]);
            })
            .OrderBy(item => item.CollectionName, StringComparer.Ordinal)
            .ToList();
    }

    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<BsonDocument>>>
        ReadIndexesAsync(
            DbContext context,
            IReadOnlyCollection<ProductionCollectionInfo> collections,
            CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, IReadOnlyList<BsonDocument>>(
            StringComparer.Ordinal);

        foreach (var collection in collections)
        {
            var documents = await ListIndexesAsync(
                context.Database.GetCollection<BsonDocument>(collection.CollectionName),
                cancellationToken);

            result[collection.CollectionName] = documents;
        }

        return result;
    }

    public static async Task<IReadOnlyList<BsonDocument>> ListIndexesAsync(
        IMongoCollection<BsonDocument> collection,
        CancellationToken cancellationToken)
    {
        using var cursor = await collection.Indexes.ListAsync(cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
