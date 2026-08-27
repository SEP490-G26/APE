using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BE_IntegrationTests.Infrastructure;

public sealed class IntegrationDatabaseFixture
{
    public const string AllowedDatabaseName = "APE_IntegrationTests";
    private const string ProbeCollectionName = "__integration_probe";

    public IntegrationDatabaseFixture(
        string connectionString,
        string databaseName)
    {
        ConnectionString = connectionString;
        DatabaseName = databaseName;
        DbContext = new DbContext(connectionString, databaseName);
    }

    public string ConnectionString { get; }

    public string DatabaseName { get; }

    public DbContext DbContext { get; }

    public async Task VerifyConnectivityAsync(CancellationToken cancellationToken = default)
    {
        await DbContext.Database.RunCommandAsync(
            (Command<BsonDocument>)"{ ping: 1 }",
            cancellationToken: cancellationToken);
    }

    public async Task VerifyAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        EnsureSafeDatabase();

        var probeCollection = DbContext.Database.GetCollection<BsonDocument>(ProbeCollectionName);
        var probeId = ObjectId.GenerateNewId();
        var probeDocument = new BsonDocument
        {
            ["_id"] = probeId,
            ["createdAtUtc"] = DateTime.UtcNow
        };

        await probeCollection.InsertOneAsync(
            probeDocument,
            cancellationToken: cancellationToken);

        var loadedDocument = await probeCollection.Find(
                Builders<BsonDocument>.Filter.Eq("_id", probeId))
            .FirstOrDefaultAsync(cancellationToken);

        if (loadedDocument is null)
        {
            throw new InvalidOperationException(
                "Integration authorization probe inserted a document but could not read it back.");
        }

        await probeCollection.DeleteOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", probeId),
            cancellationToken);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        EnsureSafeDatabase();

        await DeleteAllDocumentsAsync(DbContext.Users, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.Courses, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.Exams, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.PracticeSessions, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.FEQuestions, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.PEQuestions, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.FESubmissions, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.PE_Submissions, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.Documents, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.AIExtractionDrafts, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.AIContextPacks, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.KnowledgeChunks, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.AIAgents, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.AIRuleArtifacts, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.APIUsageLogs, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.AICreditTransactions, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.AIVndBillingTransactions, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.AIMentorFeedbacks, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.SystemSettings, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.Payments, cancellationToken);
        await DeleteAllDocumentsAsync(DbContext.Reports, cancellationToken);
        await DeleteAllDocumentsAsync(
            DbContext.Database.GetCollection<BsonDocument>(ProbeCollectionName),
            cancellationToken);
    }

    public Task DropAsync(CancellationToken cancellationToken = default)
    {
        EnsureSafeDatabase();
        return ResetAsync(cancellationToken);
    }

    private void EnsureSafeDatabase()
    {
        if (!string.Equals(
                DatabaseName,
                AllowedDatabaseName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing destructive integration-test operation because database '{DatabaseName}' is not the approved integration database '{AllowedDatabaseName}'.");
        }
    }

    private static Task DeleteAllDocumentsAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        CancellationToken cancellationToken)
    {
        return collection.DeleteManyAsync(
            FilterDefinition<TDocument>.Empty,
            cancellationToken);
    }
}
