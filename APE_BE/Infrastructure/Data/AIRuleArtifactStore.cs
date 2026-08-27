using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Data;

public static class AIRuleArtifactStore
{
    public static Task UpsertAsync(
        DbContext db,
        AIRuleArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(artifact);

        Normalize(artifact);

        var filter = Builders<AIRuleArtifact>.Filter.Eq(x => x.ArtifactType, artifact.ArtifactType) &
                     Builders<AIRuleArtifact>.Filter.Eq(x => x.ArtifactKey, artifact.ArtifactKey) &
                     Builders<AIRuleArtifact>.Filter.Eq("Scope.SubjectCode", artifact.Scope.SubjectCode) &
                     Builders<AIRuleArtifact>.Filter.Eq("Scope.QuestionType", artifact.Scope.QuestionType) &
                     Builders<AIRuleArtifact>.Filter.Eq("Scope.Language", artifact.Scope.Language) &
                     Builders<AIRuleArtifact>.Filter.Eq(x => x.Version, artifact.Version) &
                     Builders<AIRuleArtifact>.Filter.Eq(x => x.ContentHash, artifact.ContentHash);

        return UpsertInternalAsync(db, artifact, filter, cancellationToken);
    }

    public static async Task ActivateAsync(
        DbContext db,
        AIRuleArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        Normalize(artifact);
        artifact.IsActive = true;
        artifact.ActivatedAt ??= artifact.UpdatedAt;

        var filter = Builders<AIRuleArtifact>.Filter.Eq(x => x.ArtifactType, artifact.ArtifactType) &
                     Builders<AIRuleArtifact>.Filter.Eq(x => x.ArtifactKey, artifact.ArtifactKey) &
                     Builders<AIRuleArtifact>.Filter.Eq("Scope.SubjectCode", artifact.Scope.SubjectCode) &
                     Builders<AIRuleArtifact>.Filter.Eq("Scope.QuestionType", artifact.Scope.QuestionType) &
                     Builders<AIRuleArtifact>.Filter.Eq("Scope.Language", artifact.Scope.Language) &
                     Builders<AIRuleArtifact>.Filter.Eq(x => x.Version, artifact.Version) &
                     Builders<AIRuleArtifact>.Filter.Eq(x => x.ContentHash, artifact.ContentHash);

        var existing = await db.AIRuleArtifacts.Find(filter).FirstOrDefaultAsync(cancellationToken);
        artifact.Id = existing?.Id ?? artifact.Id;
        artifact.CreatedAt = existing?.CreatedAt ?? artifact.CreatedAt;
        if (existing is not null && existing.IsActive)
        {
            artifact.ActivatedAt = existing.ActivatedAt ?? artifact.ActivatedAt;
            artifact.ActivatedBy = existing.ActivatedBy ?? artifact.ActivatedBy;
        }

        await db.AIRuleArtifacts.UpdateManyAsync(
            Builders<AIRuleArtifact>.Filter.Eq(x => x.ArtifactType, artifact.ArtifactType) &
            Builders<AIRuleArtifact>.Filter.Eq(x => x.ArtifactKey, artifact.ArtifactKey) &
            Builders<AIRuleArtifact>.Filter.Eq("Scope.SubjectCode", artifact.Scope.SubjectCode) &
            Builders<AIRuleArtifact>.Filter.Eq("Scope.QuestionType", artifact.Scope.QuestionType) &
            Builders<AIRuleArtifact>.Filter.Eq("Scope.Language", artifact.Scope.Language) &
            Builders<AIRuleArtifact>.Filter.Ne(x => x.Id, artifact.Id) &
            Builders<AIRuleArtifact>.Filter.Eq(x => x.IsActive, true),
            Builders<AIRuleArtifact>.Update
                .Set(x => x.IsActive, false)
                .Set(x => x.UpdatedAt, artifact.UpdatedAt),
            cancellationToken: cancellationToken);

        await db.AIRuleArtifacts.ReplaceOneAsync(
            Builders<AIRuleArtifact>.Filter.Eq(x => x.Id, artifact.Id),
            artifact,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public static string ComputeContentHash(string contentJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(contentJson ?? string.Empty));
        return Convert.ToHexString(bytes);
    }

    private static async Task UpsertInternalAsync(
        DbContext db,
        AIRuleArtifact artifact,
        FilterDefinition<AIRuleArtifact> filter,
        CancellationToken cancellationToken)
    {
        var existing = await db.AIRuleArtifacts.Find(filter).FirstOrDefaultAsync(cancellationToken);
        artifact.Id = existing?.Id ?? artifact.Id;
        artifact.CreatedAt = existing?.CreatedAt ?? artifact.CreatedAt;
        if (existing is not null && existing.IsActive && !artifact.IsActive)
        {
            artifact.IsActive = true;
            artifact.ActivatedAt = existing.ActivatedAt ?? artifact.ActivatedAt;
            artifact.ActivatedBy = existing.ActivatedBy ?? artifact.ActivatedBy;
        }

        await db.AIRuleArtifacts.ReplaceOneAsync(
            Builders<AIRuleArtifact>.Filter.Eq(x => x.Id, artifact.Id),
            artifact,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    private static void Normalize(AIRuleArtifact artifact)
    {
        artifact.ArtifactType = NormalizeString(artifact.ArtifactType);
        artifact.ArtifactKey = NormalizeString(artifact.ArtifactKey);
        artifact.Version = string.IsNullOrWhiteSpace(artifact.Version) ? "v1" : artifact.Version.Trim();
        artifact.ContentJson ??= "{}";
        artifact.ContentHash = string.IsNullOrWhiteSpace(artifact.ContentHash)
            ? ComputeContentHash(artifact.ContentJson)
            : artifact.ContentHash.Trim().ToUpperInvariant();
        artifact.Scope ??= new AIRuleArtifactScope();
        artifact.Scope.SubjectCode = NormalizeNullableString(artifact.Scope.SubjectCode);
        artifact.Scope.QuestionType = NormalizeNullableString(artifact.Scope.QuestionType);
        artifact.Scope.Language = NormalizeNullableString(artifact.Scope.Language);
        artifact.Source = string.IsNullOrWhiteSpace(artifact.Source) ? "db" : artifact.Source.Trim();
        artifact.CreatedBy = NormalizeObjectIdOrNull(artifact.CreatedBy);
        artifact.UpdatedBy = NormalizeObjectIdOrNull(artifact.UpdatedBy);
        artifact.ActivatedBy = NormalizeObjectIdOrNull(artifact.ActivatedBy);
    }

    private static string NormalizeString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? NormalizeNullableString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeObjectIdOrNull(string? value)
    {
        return ObjectId.TryParse(value, out _) ? value : null;
    }
}
