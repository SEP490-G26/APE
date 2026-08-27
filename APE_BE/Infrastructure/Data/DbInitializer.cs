using Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Infrastructure.Data;

public static class DbInitializer
{
    private const string AttemptUniqueIndexName =
        "ux_pe_submission_session_question_attempt";

    private const string SessionHistoryIndexName =
        "ix_pe_submission_session_submitted_at";

    private const string WorkerQueueIndexName =
        "ix_pe_submission_status_submitted_at";

    public static async Task InitializeAsync(
        DbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        await EnsurePESubmissionIndexesAsync(
            db.PE_Submissions,
            cancellationToken);

        await NormalizeNullableUserFieldsAsync(
            db.Users,
            cancellationToken);

        await EnsureIndexAsync(
            db.Users,
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions
                {
                    Name = "UX_User_Email",
                    Unique = true
                }),
            cancellationToken);

        await DropIndexIfExistsAsync(
            db.Users,
            "UX_User_GoogleId",
            cancellationToken);

        await EnsureIndexAsync(
            db.Users,
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.GoogleId),
                new CreateIndexOptions
                {
                    Name = "UX_User_GoogleId",
                    Unique = true,
                    Sparse = true
                }),
            cancellationToken);

        await EnsureIndexAsync(
            db.Courses,
            new CreateIndexModel<Course>(
                Builders<Course>.IndexKeys.Ascending(c => c.Code),
                new CreateIndexOptions
                {
                    Name = "UX_Course_Code",
                    Unique = true
                }),
            cancellationToken);

        await EnsureIndexAsync(
            db.Documents,
            new CreateIndexModel<Document>(
                Builders<Document>.IndexKeys
                    .Ascending(d => d.CourseId)
                    .Ascending(d => d.UserId),
                new CreateIndexOptions
                {
                    Name = "IX_Document_CourseId_UserId"
                }),
            cancellationToken);

        await EnsureIndexAsync(
            db.KnowledgeChunks,
            new CreateIndexModel<KnowledgeChunk>(
                Builders<KnowledgeChunk>.IndexKeys.Ascending(k => k.DocumentId),
                new CreateIndexOptions
                {
                    Name = "IX_KnowledgeChunk_DocumentId"
                }),
            cancellationToken);

        await EnsureIndexAsync(
            db.PEQuestions,
            new CreateIndexModel<PEQuestion>(
                Builders<PEQuestion>.IndexKeys.Ascending(q => q.CourseId),
                new CreateIndexOptions
                {
                    Name = "IX_PEQuestion_CourseId"
                }),
            cancellationToken);

        await EnsureIndexAsync(
            db.FEQuestions,
            new CreateIndexModel<FEQuestion>(
                Builders<FEQuestion>.IndexKeys.Ascending(q => q.CourseId),
                new CreateIndexOptions
                {
                    Name = "IX_FEQuestion_CourseId"
                }),
            cancellationToken);

        await EnsureIndexAsync(
            db.Exams,
            new CreateIndexModel<Exam>(
                Builders<Exam>.IndexKeys.Ascending(e => e.CourseId),
                new CreateIndexOptions
                {
                    Name = "IX_Exam_CourseId"
                }),
            cancellationToken);

        await DropIndexIfExistsAsync(
            db.PracticeSessions,
            "StudentId_1_ExamId_1",
            cancellationToken);

        await EnsureIndexAsync(
            db.PracticeSessions,
            new CreateIndexModel<PracticeSession>(
                Builders<PracticeSession>.IndexKeys
                    .Ascending(s => s.StudentId)
                    .Ascending(s => s.ExamId),
                new CreateIndexOptions
                {
                    Name = "IX_PracticeSession_StudentId_ExamId"
                }),
            cancellationToken);

        await EnsureIndexAsync(
            db.FESubmissions,
            new CreateIndexModel<FE_Submission>(
                Builders<FE_Submission>.IndexKeys
                    .Ascending(s => s.SessionId)
                    .Ascending(s => s.FeQuestionId),
                new CreateIndexOptions
                {
                    Name = "IX_FESubmission_SessionId_FeQuestionId"
                }),
            cancellationToken);

        await EnsureIndexAsync(
            db.Payments,
            new CreateIndexModel<Payment>(
                Builders<Payment>.IndexKeys.Ascending(p => p.UserId),
                new CreateIndexOptions
                {
                    Name = "IX_Payment_UserId"
                }),
            cancellationToken);

        await EnsureIndexAsync(
            db.Payments,
            new CreateIndexModel<Payment>(
                Builders<Payment>.IndexKeys.Ascending(p => p.OrderCode),
                new CreateIndexOptions
                {
                    Name = "UX_Payment_OrderCode",
                    Unique = true
                }),
            cancellationToken);

        await EnsureIndexAsync(
            db.TopupPackages,
            new CreateIndexModel<TopupPackage>(
                Builders<TopupPackage>.IndexKeys.Ascending(item => item.Code),
                new CreateIndexOptions { Name = "UX_TopupPackage_Code", Unique = true }));

        await EnsureIndexAsync(
            db.SystemSettings,
            new CreateIndexModel<SystemSetting>(
                Builders<SystemSetting>.IndexKeys.Ascending(s => s.SettingName),
                new CreateIndexOptions
                {
                    Name = "UX_SystemSetting_SettingName",
                    Unique = true
                }),
            cancellationToken);

        await EnsureIndexAsync(
            db.AIRuleArtifacts,
            new CreateIndexModel<AIRuleArtifact>(
                Builders<AIRuleArtifact>.IndexKeys
                    .Ascending(x => x.ArtifactType)
                    .Ascending(x => x.ArtifactKey)
                    .Ascending("Scope.SubjectCode")
                    .Ascending("Scope.QuestionType")
                    .Ascending("Scope.Language")
                    .Ascending(x => x.Version)
                    .Ascending(x => x.ContentHash),
                new CreateIndexOptions
                {
                    Name = "UX_AIRuleArtifact_Type_Key_Scope_Version_ContentHash",
                    Unique = true
                }));

        await EnsureIndexAsync(
            db.AIRuleArtifacts,
            new CreateIndexModel<AIRuleArtifact>(
                Builders<AIRuleArtifact>.IndexKeys
                    .Ascending(x => x.ArtifactType)
                    .Ascending(x => x.ArtifactKey)
                    .Ascending("Scope.SubjectCode")
                    .Ascending("Scope.QuestionType")
                    .Ascending("Scope.Language")
                    .Ascending(x => x.IsActive),
                new CreateIndexOptions<AIRuleArtifact>
                {
                    Name = "UX_AIRuleArtifact_Type_Key_Scope_Active",
                    Unique = true,
                    PartialFilterExpression = Builders<AIRuleArtifact>.Filter.Eq(x => x.IsActive, true)
                }));

        await EnsureIndexAsync(
            db.AIRuleArtifacts,
            new CreateIndexModel<AIRuleArtifact>(
                Builders<AIRuleArtifact>.IndexKeys
                    .Ascending(x => x.ArtifactType)
                    .Ascending(x => x.ArtifactKey)
                    .Descending(x => x.UpdatedAt),
                new CreateIndexOptions { Name = "IX_AIRuleArtifact_Type_Key_UpdatedAt" }));

        await SeedTopupPackagesAsync(db.TopupPackages, cancellationToken);

    }

    private static async Task EnsurePESubmissionIndexesAsync(
        IMongoCollection<PE_Submission> collection,
        CancellationToken cancellationToken)
    {
        await EnsureNoDuplicateAttemptKeysAsync(
            collection,
            cancellationToken);

        foreach (var index in BuildPESubmissionIndexes())
        {
            await EnsureIndexAsync(
                collection,
                index,
                cancellationToken);
        }
    }

    private static async Task DropIndexIfExistsAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        string indexName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cursor = await collection.Indexes.ListAsync(
                cancellationToken);

            var existingIndexes = await cursor.ToListAsync(
                cancellationToken);

            if (existingIndexes.Any(item =>
                    item["name"].AsString == indexName))
            {
                await collection.Indexes.DropOneAsync(
                    indexName,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Ignore when the collection does not exist yet.
        }
    }

    private static async Task EnsureIndexAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        CreateIndexModel<TDocument> model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(model);

        var indexName = model.Options?.Name;

        if (string.IsNullOrWhiteSpace(indexName))
        {
            throw new InvalidOperationException(
                "Mongo index definitions must use an explicit stable name.");
        }

        var expectedKeys = RenderKeys(model.Keys);
        var expectedUnique = model.Options?.Unique == true;
        var expectedSparse = model.Options?.Sparse == true;
        var expectedPartialFilter = NormalizePartialFilter(
            RenderFilter(collection, model.Options?.PartialFilterExpression));
        var expectedCollation =
            NormalizeCollation(model.Options?.Collation?.ToBsonDocument());

        using var cursor = await collection.Indexes.ListAsync(
            cancellationToken);

        var existingIndexes = await cursor.ToListAsync(cancellationToken);

        foreach (var existingIndex in existingIndexes)
        {
            var existingName = existingIndex["name"].AsString;
            var equivalence = EvaluateIndexEquivalence(
                existingIndex,
                expectedKeys,
                expectedUnique,
                expectedSparse,
                expectedPartialFilter,
                expectedCollation);

            if (string.Equals(
                    existingName,
                    indexName,
                    StringComparison.Ordinal))
            {
                if (!equivalence.IsEquivalent)
                {
                    throw new InvalidOperationException(
                        $"Existing Mongo index '{existingName}' on collection '{collection.CollectionNamespace.CollectionName}' does not match the required definition: {equivalence.MismatchDescription}.");
                }

                return;
            }

            if (equivalence.IsEquivalent)
            {
                return;
            }
        }

        try
        {
            await collection.Indexes.CreateOneAsync(
                model,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MongoCommandException)
        {
            if (await EquivalentIndexExistsAsync(
                    collection,
                    expectedKeys,
                    expectedUnique,
                    expectedSparse,
                    expectedPartialFilter,
                    expectedCollation,
                    cancellationToken))
            {
                return;
            }

            throw;
        }
        catch (MongoWriteException)
        {
            if (await EquivalentIndexExistsAsync(
                    collection,
                    expectedKeys,
                    expectedUnique,
                    expectedSparse,
                    expectedPartialFilter,
                    expectedCollation,
                    cancellationToken))
            {
                return;
            }

            throw;
        }
    }

    private static async Task NormalizeNullableUserFieldsAsync(
        IMongoCollection<User> collection,
        CancellationToken cancellationToken)
    {
        var nullGoogleIdFilter = Builders<User>.Filter.Or(
            Builders<User>.Filter.Type(
                nameof(User.GoogleId),
                BsonType.Null),
            Builders<User>.Filter.Eq(
                user => user.GoogleId,
                string.Empty));

        var unsetGoogleId = Builders<User>.Update.Unset(
            nameof(User.GoogleId));

        await collection.UpdateManyAsync(
            nullGoogleIdFilter,
            unsetGoogleId,
            cancellationToken: cancellationToken);
    }

    private static IReadOnlyList<CreateIndexModel<PE_Submission>>
        BuildPESubmissionIndexes()
    {
        return
        [
            new CreateIndexModel<PE_Submission>(
                Builders<PE_Submission>.IndexKeys
                    .Ascending(item => item.SessionId)
                    .Ascending(item => item.QuestionId)
                    .Ascending(item => item.AttemptCount),
                new CreateIndexOptions
                {
                    Name = AttemptUniqueIndexName,
                    Unique = true
                }),
            new CreateIndexModel<PE_Submission>(
                Builders<PE_Submission>.IndexKeys
                    .Ascending(item => item.SessionId)
                    .Descending(item => item.SubmittedAt),
                new CreateIndexOptions
                {
                    Name = SessionHistoryIndexName
                }),
            new CreateIndexModel<PE_Submission>(
                Builders<PE_Submission>.IndexKeys
                    .Ascending(item => item.Status)
                    .Ascending(item => item.SubmittedAt),
                new CreateIndexOptions
                {
                    Name = WorkerQueueIndexName
                })
        ];
    }

    private static async Task EnsureNoDuplicateAttemptKeysAsync(
        IMongoCollection<PE_Submission> collection,
        CancellationToken cancellationToken)
    {
        var duplicateKeys = await collection.Aggregate()
            .Group(
                item => new
                {
                    item.SessionId,
                    item.QuestionId,
                    item.AttemptCount
                },
                group => new DuplicateAttemptKey
                {
                    SessionId = group.Key.SessionId,
                    QuestionId = group.Key.QuestionId,
                    AttemptCount = group.Key.AttemptCount,
                    Count = group.Count()
                })
            .Match(item => item.Count > 1)
            .SortByDescending(item => item.Count)
            .Limit(5)
            .ToListAsync(cancellationToken);

        if (duplicateKeys.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            BuildDuplicateAttemptMessage(
                collection.CollectionNamespace.CollectionName,
                duplicateKeys));
    }

    private static string BuildDuplicateAttemptMessage(
        string collectionName,
        IReadOnlyCollection<DuplicateAttemptKey> duplicateKeys)
    {
        var samples = string.Join(
            "; ",
            duplicateKeys.Select(item =>
                $"(SessionId={item.SessionId}, QuestionId={item.QuestionId}, AttemptCount={item.AttemptCount}, Count={item.Count})"));

        return
            $"Cannot create unique PE submission attempt index '{AttemptUniqueIndexName}' on collection '{collectionName}' because duplicate logical attempt keys already exist. " +
            $"A one-time migration is required before startup can continue. Sample duplicates: {samples}";
    }

    private static async Task<bool> EquivalentIndexExistsAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        BsonDocument expectedKeys,
        bool expectedUnique,
        bool expectedSparse,
        BsonDocument? expectedPartialFilter,
        BsonDocument? expectedCollation,
        CancellationToken cancellationToken)
    {
        using var cursor = await collection.Indexes.ListAsync(
            cancellationToken);

        var existingIndexes = await cursor.ToListAsync(cancellationToken);

        return existingIndexes.Any(existingIndex =>
            EvaluateIndexEquivalence(
                existingIndex,
                expectedKeys,
                expectedUnique,
                expectedSparse,
                expectedPartialFilter,
                expectedCollation).IsEquivalent);
    }

    private static BsonDocument RenderKeys<TDocument>(
        IndexKeysDefinition<TDocument> keys)
    {
        var serializer = BsonSerializer.SerializerRegistry
            .GetSerializer<TDocument>();

        return keys.Render(
            new RenderArgs<TDocument>(
                serializer,
                BsonSerializer.SerializerRegistry));
    }

    private static BsonDocument? RenderFilter<TDocument>(
        IMongoCollection<TDocument> collection,
        FilterDefinition<TDocument>? filter)
    {
        if (filter is null)
        {
            return null;
        }

        return filter.Render(
            new RenderArgs<TDocument>(
                collection.DocumentSerializer,
                collection.Settings.SerializerRegistry));
    }

    private static IndexEquivalenceResult EvaluateIndexEquivalence(
        BsonDocument existingIndex,
        BsonDocument expectedKeys,
        bool expectedUnique,
        bool expectedSparse,
        BsonDocument? expectedPartialFilter,
        BsonDocument? expectedCollation)
    {
        var existingKeys = existingIndex["key"].AsBsonDocument;
        if (!existingKeys.Equals(expectedKeys))
        {
            return new IndexEquivalenceResult(
                false,
                "key pattern");
        }

        var existingUnique = GetBooleanOption(
            existingIndex,
            "unique");
        if (existingUnique != expectedUnique)
        {
            return new IndexEquivalenceResult(
                false,
                "unique option");
        }

        var existingSparse = GetBooleanOption(
            existingIndex,
            "sparse");
        if (existingSparse != expectedSparse)
        {
            return new IndexEquivalenceResult(
                false,
                "sparse option");
        }

        var existingPartialFilter = NormalizePartialFilter(
            GetOptionalDocument(
                existingIndex,
                "partialFilterExpression"));
        if (!BsonDocumentEquals(
                existingPartialFilter,
                expectedPartialFilter))
        {
            return new IndexEquivalenceResult(
                false,
                "partial filter");
        }

        var existingCollation = NormalizeCollation(
            GetOptionalDocument(
                existingIndex,
                "collation"));
        if (!BsonDocumentEquals(
                existingCollation,
                expectedCollation))
        {
            return new IndexEquivalenceResult(
                false,
                "collation");
        }

        return new IndexEquivalenceResult(
            true,
            null);
    }

    private static bool GetBooleanOption(
        BsonDocument document,
        string elementName)
    {
        return document.TryGetValue(elementName, out var value) &&
               value.ToBoolean();
    }

    private static BsonDocument? GetOptionalDocument(
        BsonDocument document,
        string elementName)
    {
        if (!document.TryGetValue(elementName, out var value) ||
            value.IsBsonNull)
        {
            return null;
        }

        return value.AsBsonDocument;
    }

    private static BsonDocument? NormalizePartialFilter(
        BsonDocument? partialFilter)
    {
        if (partialFilter is null || partialFilter.ElementCount == 0)
        {
            return null;
        }

        return partialFilter;
    }

    private static BsonDocument? NormalizeCollation(
        BsonDocument? collation)
    {
        if (collation is null || collation.ElementCount == 0)
        {
            return null;
        }

        var normalized = new BsonDocument(collation);

        if (normalized.ElementCount == 1 &&
            normalized.TryGetValue("locale", out var localeValue) &&
            localeValue.IsString &&
            localeValue.AsString.Equals(
                "simple",
                StringComparison.Ordinal))
        {
            return null;
        }

        return normalized;
    }

    private static bool BsonDocumentEquals(
        BsonDocument? left,
        BsonDocument? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Equals(right);
    }

    private readonly record struct IndexEquivalenceResult(
        bool IsEquivalent,
        string? MismatchDescription);

    private sealed class DuplicateAttemptKey
    {
        public string SessionId { get; init; } = string.Empty;

        public string QuestionId { get; init; } = string.Empty;

        public int AttemptCount { get; init; }

        public int Count { get; init; }
    }

    private static async Task SeedTopupPackagesAsync(
        IMongoCollection<TopupPackage> collection,
        CancellationToken cancellationToken)
    {
        var hasAny = await collection.Find(Builders<TopupPackage>.Filter.Empty)
            .Limit(1)
            .AnyAsync(cancellationToken);

        if (hasAny)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var items = new List<TopupPackage>
        {
            new()
            {
                Code = "STARTER_10K",
                Name = "Starter 10K",
                Description = "Quick refill for a few AI study sessions.",
                AmountVnd = 10_000,
                SortOrder = 1,
                IsActive = true,
                IsFeatured = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                Code = "FOCUS_50K",
                Name = "Focus 50K",
                Description = "Balanced package for regular generation and mentor usage.",
                AmountVnd = 50_000,
                SortOrder = 2,
                IsActive = true,
                IsFeatured = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                Code = "INTENSIVE_100K",
                Name = "Intensive 100K",
                Description = "For students working across multiple AI-heavy workflows.",
                AmountVnd = 100_000,
                SortOrder = 3,
                IsActive = true,
                IsFeatured = true,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        await collection.InsertManyAsync(items, cancellationToken: cancellationToken);
    }
}
