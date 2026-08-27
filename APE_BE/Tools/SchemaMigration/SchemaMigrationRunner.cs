using Application.Common;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace SchemaMigration;

internal sealed class SchemaMigrationRunner
{
    private static readonly string[] UnusedCollectionCandidates =
    [
        "AIConfigs",
        "AIKnowledgeAssessments",
        "ExamAttemptSnapshots",
        "Prompts",
        "UserAnalytics",
        "KnowledgeChunkQuestionBanks",
        "QuestionBankExams"
    ];

    private readonly DbContext _context;

    public SchemaMigrationRunner(DbContext context)
    {
        _context = context;
    }

    public async Task<int> InspectAsync(CancellationToken cancellationToken)
    {
        var report = await BuildReportAsync(cancellationToken);
        PrintReport(report, isDryRun: true);
        return 0;
    }

    public async Task<int> ApplyAsync(CancellationToken cancellationToken)
    {
        var report = await BuildReportAsync(cancellationToken);
        PrintReport(report, isDryRun: true);

        if (!report.HasWork)
        {
            Console.WriteLine("No schema migration changes are required.");
            return 0;
        }

        var appliedWallet = await ApplyMissingWalletBalancesAsync(cancellationToken);
        var appliedChunks = await ApplyKnowledgeChunkBackfillAsync(cancellationToken);
        var appliedFeSubmissions = await ApplyFESubmissionBackfillAsync(cancellationToken);

        Console.WriteLine();
        Console.WriteLine("Applied migration changes:");
        Console.WriteLine($"- Users backfilled with AiWalletBalanceVnd = 0: {appliedWallet}");
        Console.WriteLine($"- KnowledgeChunks backfilled with CourseId/UserId: {appliedChunks}");
        Console.WriteLine($"- FESubmissions backfilled with SubmittedAt: {appliedFeSubmissions}");
        return 0;
    }

    public async Task<int> InspectUnusedCollectionsAsync(CancellationToken cancellationToken)
    {
        var report = await BuildUnusedCollectionsReportAsync(cancellationToken);
        PrintUnusedCollectionsReport(report, isDryRun: true);
        return 0;
    }

    public async Task<int> ApplyUnusedCollectionsCleanupAsync(CancellationToken cancellationToken)
    {
        var report = await BuildUnusedCollectionsReportAsync(cancellationToken);
        PrintUnusedCollectionsReport(report, isDryRun: true);

        if (report.CollectionsToDrop.Count == 0)
        {
            Console.WriteLine("No unused collections were found.");
            return 0;
        }

        foreach (var collectionName in report.CollectionsToDrop)
        {
            await _context.Database.DropCollectionAsync(collectionName, cancellationToken);
        }

        Console.WriteLine();
        Console.WriteLine("Dropped unused collections:");
        foreach (var collectionName in report.CollectionsToDrop)
        {
            Console.WriteLine($"- {collectionName}");
        }

        return 0;
    }

    public async Task<int> InspectCourseAiNormalizationAsync(CancellationToken cancellationToken)
    {
        var report = await BuildCourseAiNormalizationReportAsync(cancellationToken);
        PrintCourseAiNormalizationReport(report, isDryRun: true);
        return report.HasConflicts ? 2 : 0;
    }

    public async Task<int> ApplyCourseAiNormalizationAsync(CancellationToken cancellationToken)
    {
        var report = await BuildCourseAiNormalizationReportAsync(cancellationToken);
        PrintCourseAiNormalizationReport(report, isDryRun: true);

        if (report.HasConflicts)
        {
            Console.WriteLine("Aborted because at least one target course code conflicts with another existing course.");
            return 2;
        }

        if (report.Items.Count == 0)
        {
            Console.WriteLine("No course or AI normalization changes are required.");
            return 0;
        }

        foreach (var item in report.Items)
        {
            if (!string.Equals(item.CurrentCode, item.TargetCode, StringComparison.Ordinal))
            {
                var courseUpdate = Builders<Domain.Entities.Course>.Update
                    .Set(course => course.Code, item.TargetCode)
                    .Set(course => course.LastModifiedAt, DateTime.UtcNow);

                await _context.Courses.UpdateOneAsync(
                    course => course.Id == item.CourseId,
                    courseUpdate,
                    cancellationToken: cancellationToken);
            }

            await _context.Documents.UpdateManyAsync(
                document => document.CourseId == item.CourseId,
                Builders<Domain.Entities.Document>.Update.Set(document => document.SubjectCode, item.TargetSubject),
                cancellationToken: cancellationToken);

            await _context.KnowledgeChunks.UpdateManyAsync(
                chunk => chunk.CourseId == item.CourseId,
                Builders<Domain.Entities.KnowledgeChunk>.Update
                    .Set(chunk => chunk.SubjectCode, item.TargetSubject)
                    .Set(chunk => chunk.UpdatedAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);

            await _context.AIExtractionDrafts.UpdateManyAsync(
                draft => draft.CourseId == item.CourseId,
                Builders<Domain.Entities.AIExtractionDraft>.Update
                    .Set(draft => draft.SubjectCode, item.TargetSubject)
                    .Set(draft => draft.UpdatedAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);

            await _context.AIContextPacks.UpdateManyAsync(
                pack => pack.CourseId == item.CourseId,
                Builders<Domain.Entities.AIContextPack>.Update
                    .Set(pack => pack.Subject, item.TargetSubject)
                    .Set(pack => pack.UpdatedAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);

            await _context.PEQuestions.UpdateManyAsync(
                question => question.CourseId == item.CourseId,
                Builders<Domain.Entities.PEQuestion>.Update
                    .Set(question => question.AllowedLanguageIds, new List<int> { item.TargetLanguageId })
                    .Set(question => question.DefaultLanguageId, item.TargetLanguageId)
                    .Set(question => question.LastModifiedAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);
        }

        Console.WriteLine();
        Console.WriteLine("Applied course and AI normalization:");
        foreach (var item in report.Items)
        {
            Console.WriteLine($"- {item.CourseName}: {item.CurrentCode} -> {item.TargetCode}, subject={item.TargetSubject}, peLanguage={item.TargetLanguageId}");
        }

        return 0;
    }

    public async Task<int> InspectFeQuestionsAsync(CancellationToken cancellationToken)
    {
        var feQuestions = await _context.FEQuestions
            .Find(FilterDefinition<Domain.Entities.FEQuestion>.Empty)
            .ToListAsync(cancellationToken);

        var courses = await _context.Courses
            .Find(FilterDefinition<Domain.Entities.Course>.Empty)
            .ToListAsync(cancellationToken);

        var coursesById = courses.ToDictionary(item => item.Id, StringComparer.Ordinal);

        Console.WriteLine("FE question inspection:");
        Console.WriteLine($"- Total FE questions in DB: {feQuestions.Count}");

        foreach (var group in feQuestions
                     .OrderBy(item => item.CourseId, StringComparer.Ordinal)
                     .GroupBy(item => item.CourseId, StringComparer.Ordinal))
        {
            coursesById.TryGetValue(group.Key, out var course);
            Console.WriteLine($"- CourseId: {group.Key}");
            Console.WriteLine($"  Course: {(course is null ? "<missing course>" : $"{course.Code} - {course.Name}")}");
            Console.WriteLine($"  Count: {group.Count()}");

            foreach (var item in group.OrderBy(question => question.Title, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"    - {item.Title} | Status={item.Status} | Id={item.Id}");
            }
        }

        return 0;
    }

    public async Task<int> InspectQuestionBankDocumentLinksAsync(CancellationToken cancellationToken)
    {
        var report = await BuildQuestionBankDocumentLinksReportAsync(cancellationToken);
        PrintQuestionBankDocumentLinksReport(report, isDryRun: true);
        return 0;
    }

    public async Task<int> ApplyQuestionBankDocumentLinksAsync(CancellationToken cancellationToken)
    {
        var report = await BuildQuestionBankDocumentLinksReportAsync(cancellationToken);
        PrintQuestionBankDocumentLinksReport(report, isDryRun: true);

        if (!report.HasWork)
        {
            Console.WriteLine("No question-bank document link migration changes are required.");
            return 0;
        }

        var feCollection = _context.Database.GetCollection<BsonDocument>("FEQuestions");
        var peCollection = _context.Database.GetCollection<BsonDocument>("PEQuestions");
        var modifiedFe = await BackfillQuestionSourceDocumentsAsync(
            feCollection,
            legacyFieldName: "SourceChunkIds",
            cancellationToken);
        var modifiedPe = await BackfillQuestionSourceDocumentsAsync(
            peCollection,
            legacyFieldName: "ChunkIds",
            cancellationToken);

        Console.WriteLine();
        Console.WriteLine("Applied question-bank document link migration:");
        Console.WriteLine($"- FEQuestions updated: {modifiedFe}");
        Console.WriteLine($"- PEQuestions updated: {modifiedPe}");
        Console.WriteLine("- Legacy chunk link fields removed: SourceChunkIds, ChunkIds");
        return 0;
    }

    private async Task<SchemaMigrationReport> BuildReportAsync(CancellationToken cancellationToken)
    {
        var missingWalletBalance = await _context.Users.CountDocumentsAsync(
            Builders<Domain.Entities.User>.Filter.Exists(nameof(Domain.Entities.User.AiWalletBalanceVnd), false),
            cancellationToken: cancellationToken);

        var missingChunkCourseOrUser = await _context.KnowledgeChunks.CountDocumentsAsync(
            Builders<Domain.Entities.KnowledgeChunk>.Filter.Or(
                Builders<Domain.Entities.KnowledgeChunk>.Filter.Exists(nameof(Domain.Entities.KnowledgeChunk.CourseId), false),
                Builders<Domain.Entities.KnowledgeChunk>.Filter.Exists(nameof(Domain.Entities.KnowledgeChunk.UserId), false),
                Builders<Domain.Entities.KnowledgeChunk>.Filter.Eq(item => item.CourseId, string.Empty),
                Builders<Domain.Entities.KnowledgeChunk>.Filter.Eq(item => item.UserId, string.Empty)),
            cancellationToken: cancellationToken);

        var missingFeSubmittedAt = await _context.FESubmissions.CountDocumentsAsync(
            Builders<Domain.Entities.FE_Submission>.Filter.Exists(nameof(Domain.Entities.FE_Submission.SubmittedAt), false),
            cancellationToken: cancellationToken);

        return new SchemaMigrationReport(
            missingWalletBalance,
            missingChunkCourseOrUser,
            missingFeSubmittedAt);
    }

    private async Task<long> ApplyMissingWalletBalancesAsync(CancellationToken cancellationToken)
    {
        var filter = Builders<Domain.Entities.User>.Filter.Exists(nameof(Domain.Entities.User.AiWalletBalanceVnd), false);
        var update = Builders<Domain.Entities.User>.Update.Set(nameof(Domain.Entities.User.AiWalletBalanceVnd), 0L);
        var result = await _context.Users.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    private async Task<int> ApplyKnowledgeChunkBackfillAsync(CancellationToken cancellationToken)
    {
        var filter = Builders<Domain.Entities.KnowledgeChunk>.Filter.Or(
            Builders<Domain.Entities.KnowledgeChunk>.Filter.Exists(nameof(Domain.Entities.KnowledgeChunk.CourseId), false),
            Builders<Domain.Entities.KnowledgeChunk>.Filter.Exists(nameof(Domain.Entities.KnowledgeChunk.UserId), false),
            Builders<Domain.Entities.KnowledgeChunk>.Filter.Eq(item => item.CourseId, string.Empty),
            Builders<Domain.Entities.KnowledgeChunk>.Filter.Eq(item => item.UserId, string.Empty));

        var chunks = await _context.KnowledgeChunks.Find(filter).ToListAsync(cancellationToken);
        if (chunks.Count == 0)
        {
            return 0;
        }

        var documentIds = chunks
            .Where(item => !string.IsNullOrWhiteSpace(item.DocumentId))
            .Select(item => item.DocumentId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var documents = await _context.Documents
            .Find(Builders<Domain.Entities.Document>.Filter.In(item => item.Id, documentIds))
            .ToListAsync(cancellationToken);

        var documentsById = documents.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var modified = 0;

        foreach (var chunk in chunks)
        {
            if (!documentsById.TryGetValue(chunk.DocumentId, out var document))
            {
                continue;
            }

            var changed = false;
            if (string.IsNullOrWhiteSpace(chunk.CourseId))
            {
                chunk.CourseId = document.CourseId;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(chunk.UserId))
            {
                chunk.UserId = document.UserId;
                changed = true;
            }

            if (!changed)
            {
                continue;
            }

            chunk.UpdatedAt = DateTime.UtcNow;
            await _context.KnowledgeChunks.ReplaceOneAsync(
                item => item.Id == chunk.Id,
                chunk,
                cancellationToken: cancellationToken);
            modified++;
        }

        return modified;
    }

    private async Task<int> ApplyFESubmissionBackfillAsync(CancellationToken cancellationToken)
    {
        var filter = Builders<Domain.Entities.FE_Submission>.Filter.Exists(nameof(Domain.Entities.FE_Submission.SubmittedAt), false);
        var docs = await _context.FESubmissions.Find(filter).ToListAsync(cancellationToken);
        if (docs.Count == 0)
        {
            return 0;
        }

        var modified = 0;
        foreach (var item in docs)
        {
            item.SubmittedAt = ExtractUtcFromObjectId(item.Id) ?? DateTime.UtcNow;
            await _context.FESubmissions.ReplaceOneAsync(
                sub => sub.Id == item.Id,
                item,
                cancellationToken: cancellationToken);
            modified++;
        }

        return modified;
    }

    private static DateTime? ExtractUtcFromObjectId(string id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return null;
        }

        return objectId.CreationTime.ToUniversalTime();
    }

    private async Task<CourseAiNormalizationReport> BuildCourseAiNormalizationReportAsync(CancellationToken cancellationToken)
    {
        var courses = await _context.Courses.Find(FilterDefinition<Domain.Entities.Course>.Empty).ToListAsync(cancellationToken);
        var items = new List<CourseAiNormalizationItem>();

        foreach (var course in courses)
        {
            var targetCode = ResolveTargetCourseCode(course);
            if (string.IsNullOrWhiteSpace(targetCode))
            {
                continue;
            }

            var targetSubject = AISubjectDomainMapper.NormalizeSubjectOrCourseCode(targetCode);
            var targetLanguageId = string.Equals(targetSubject, AISubjectDomainMapper.C, StringComparison.Ordinal)
                ? 50
                : 62;

            var conflictingCourse = courses.FirstOrDefault(other =>
                !string.Equals(other.Id, course.Id, StringComparison.Ordinal) &&
                string.Equals(other.Code, targetCode, StringComparison.OrdinalIgnoreCase));

            var documentCount = await _context.Documents.CountDocumentsAsync(
                document => document.CourseId == course.Id,
                cancellationToken: cancellationToken);

            var chunkCount = await _context.KnowledgeChunks.CountDocumentsAsync(
                chunk => chunk.CourseId == course.Id,
                cancellationToken: cancellationToken);

            var draftCount = await _context.AIExtractionDrafts.CountDocumentsAsync(
                draft => draft.CourseId == course.Id,
                cancellationToken: cancellationToken);

            var contextPackCount = await _context.AIContextPacks.CountDocumentsAsync(
                pack => pack.CourseId == course.Id,
                cancellationToken: cancellationToken);

            var peQuestionCount = await _context.PEQuestions.CountDocumentsAsync(
                question => question.CourseId == course.Id,
                cancellationToken: cancellationToken);

            items.Add(new CourseAiNormalizationItem(
                course.Id,
                course.Name,
                course.Code,
                targetCode,
                targetSubject,
                targetLanguageId,
                documentCount,
                chunkCount,
                draftCount,
                contextPackCount,
                peQuestionCount,
                conflictingCourse?.Id,
                conflictingCourse?.Name,
                conflictingCourse?.Code));
        }

        return new CourseAiNormalizationReport(items.OrderBy(item => item.CourseName, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private async Task<UnusedCollectionsReport> BuildUnusedCollectionsReportAsync(CancellationToken cancellationToken)
    {
        var allCollectionNames = await _context.Database.ListCollectionNames().ToListAsync(cancellationToken);
        var collectionsToDrop = allCollectionNames
            .Where(name => UnusedCollectionCandidates.Contains(name, StringComparer.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return new UnusedCollectionsReport(allCollectionNames.OrderBy(name => name, StringComparer.Ordinal).ToList(), collectionsToDrop);
    }

    private async Task<QuestionBankDocumentLinksReport> BuildQuestionBankDocumentLinksReportAsync(CancellationToken cancellationToken)
    {
        var feCollection = _context.Database.GetCollection<BsonDocument>("FEQuestions");
        var peCollection = _context.Database.GetCollection<BsonDocument>("PEQuestions");

        var feDocs = await feCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        var peDocs = await peCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        var chunkToDocumentMap = await BuildChunkToDocumentMapAsync(cancellationToken);

        var feStats = BuildQuestionBankCollectionStats(feDocs, "SourceChunkIds", chunkToDocumentMap);
        var peStats = BuildQuestionBankCollectionStats(peDocs, "ChunkIds", chunkToDocumentMap);

        return new QuestionBankDocumentLinksReport(feStats, peStats);
    }

    private async Task<Dictionary<string, string>> BuildChunkToDocumentMapAsync(CancellationToken cancellationToken)
    {
        var chunks = await _context.Database
            .GetCollection<BsonDocument>("KnowledgeChunks")
            .Find(FilterDefinition<BsonDocument>.Empty)
            .Project(Builders<BsonDocument>.Projection.Include("_id").Include("DocumentId"))
            .ToListAsync(cancellationToken);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in chunks)
        {
            var chunkId = ReadId(chunk, "_id");
            var documentId = ReadId(chunk, "DocumentId");
            if (string.IsNullOrWhiteSpace(chunkId) || string.IsNullOrWhiteSpace(documentId))
            {
                continue;
            }

            map[chunkId] = documentId;
        }

        return map;
    }

    private static QuestionBankCollectionStats BuildQuestionBankCollectionStats(
        IReadOnlyList<BsonDocument> docs,
        string legacyFieldName,
        IReadOnlyDictionary<string, string> chunkToDocumentMap)
    {
        var total = docs.Count;
        var withLegacyField = 0;
        var withDocumentLinks = 0;
        var needsBackfill = 0;
        var unresolvedLegacyReferences = 0;

        foreach (var doc in docs)
        {
            var sourceDocumentIds = ReadIdArray(doc, "SourceDocumentIds");
            var legacyChunkIds = ReadIdArray(doc, legacyFieldName);

            if (sourceDocumentIds.Count > 0)
            {
                withDocumentLinks++;
            }

            if (legacyChunkIds.Count > 0)
            {
                withLegacyField++;
            }

            var resolvedDocumentIds = legacyChunkIds
                .Where(chunkToDocumentMap.ContainsKey)
                .Select(chunkId => chunkToDocumentMap[chunkId])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (legacyChunkIds.Any(chunkId => !chunkToDocumentMap.ContainsKey(chunkId)))
            {
                unresolvedLegacyReferences++;
            }

            var merged = sourceDocumentIds
                .Concat(resolvedDocumentIds)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if ((legacyChunkIds.Count > 0 || sourceDocumentIds.Count == 0) &&
                merged.Count > sourceDocumentIds.Count)
            {
                needsBackfill++;
            }
        }

        return new QuestionBankCollectionStats(
            total,
            withLegacyField,
            withDocumentLinks,
            needsBackfill,
            unresolvedLegacyReferences);
    }

    private async Task<int> BackfillQuestionSourceDocumentsAsync(
        IMongoCollection<BsonDocument> collection,
        string legacyFieldName,
        CancellationToken cancellationToken)
    {
        var docs = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        if (docs.Count == 0)
        {
            return 0;
        }

        var chunkToDocumentMap = await BuildChunkToDocumentMapAsync(cancellationToken);
        var modified = 0;

        foreach (var doc in docs)
        {
            var id = doc.GetValue("_id", BsonNull.Value);
            var sourceDocumentIds = ReadIdArray(doc, "SourceDocumentIds");
            var legacyChunkIds = ReadIdArray(doc, legacyFieldName);
            var resolvedDocumentIds = legacyChunkIds
                .Where(chunkToDocumentMap.ContainsKey)
                .Select(chunkId => chunkToDocumentMap[chunkId])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var merged = sourceDocumentIds
                .Concat(resolvedDocumentIds)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var needsDocumentUpdate = !sourceDocumentIds.SequenceEqual(merged, StringComparer.OrdinalIgnoreCase);
            var hasLegacyField = doc.Contains(legacyFieldName);

            if (!needsDocumentUpdate && !hasLegacyField)
            {
                continue;
            }

            var updates = new List<UpdateDefinition<BsonDocument>>
            {
                Builders<BsonDocument>.Update.Set(
                    "SourceDocumentIds",
                    new BsonArray(merged.Select(item => new ObjectId(item))))
            };

            if (hasLegacyField)
            {
                updates.Add(Builders<BsonDocument>.Update.Unset(legacyFieldName));
            }

            await collection.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Update.Combine(updates),
                cancellationToken: cancellationToken);

            modified++;
        }

        return modified;
    }

    private static void PrintReport(SchemaMigrationReport report, bool isDryRun)
    {
        Console.WriteLine(isDryRun ? "Schema migration inspection:" : "Schema migration report:");
        Console.WriteLine($"- Users missing AiWalletBalanceVnd: {report.UsersMissingWalletBalance}");
        Console.WriteLine($"- KnowledgeChunks missing CourseId/UserId: {report.KnowledgeChunksMissingCourseOrUser}");
        Console.WriteLine($"- FESubmissions missing SubmittedAt: {report.FESubmissionsMissingSubmittedAt}");
    }

    private static void PrintUnusedCollectionsReport(UnusedCollectionsReport report, bool isDryRun)
    {
        Console.WriteLine(isDryRun ? "Unused collection inspection:" : "Unused collection cleanup report:");
        Console.WriteLine($"- Existing collections in database: {report.AllCollections.Count}");
        Console.WriteLine($"- Unused collections detected: {report.CollectionsToDrop.Count}");

        if (report.CollectionsToDrop.Count == 0)
        {
            return;
        }

        foreach (var collectionName in report.CollectionsToDrop)
        {
            Console.WriteLine($"  - {collectionName}");
        }
    }

    private static void PrintCourseAiNormalizationReport(CourseAiNormalizationReport report, bool isDryRun)
    {
        Console.WriteLine(isDryRun ? "Course and AI normalization inspection:" : "Course and AI normalization report:");
        Console.WriteLine($"- Courses requiring normalization: {report.Items.Count}");

        if (report.Items.Count == 0)
        {
            return;
        }

        foreach (var item in report.Items)
        {
            Console.WriteLine($"  - {item.CourseName} [{item.CourseId}]");
            Console.WriteLine($"    code: {item.CurrentCode} -> {item.TargetCode}");
            Console.WriteLine($"    subject: {item.TargetSubject}");
            Console.WriteLine($"    PE language id: {item.TargetLanguageId}");
            Console.WriteLine($"    related docs/chunks/drafts/packs/peQuestions: {item.DocumentCount}/{item.ChunkCount}/{item.DraftCount}/{item.ContextPackCount}/{item.PeQuestionCount}");

            if (!string.IsNullOrWhiteSpace(item.ConflictingCourseId))
            {
                Console.WriteLine($"    conflict: target code already used by {item.ConflictingCourseName} [{item.ConflictingCourseId}] with code {item.ConflictingCourseCode}");
            }
        }
    }

    private static void PrintQuestionBankDocumentLinksReport(QuestionBankDocumentLinksReport report, bool isDryRun)
    {
        Console.WriteLine(isDryRun
            ? "Question-bank document link inspection:"
            : "Question-bank document link migration report:");
        PrintQuestionBankCollectionStats("FEQuestions", report.FeQuestions);
        PrintQuestionBankCollectionStats("PEQuestions", report.PeQuestions);
    }

    private static void PrintQuestionBankCollectionStats(string collectionName, QuestionBankCollectionStats stats)
    {
        Console.WriteLine($"- {collectionName}:");
        Console.WriteLine($"  total: {stats.Total}");
        Console.WriteLine($"  with legacy chunk links: {stats.WithLegacyField}");
        Console.WriteLine($"  with source document links: {stats.WithDocumentLinks}");
        Console.WriteLine($"  needing backfill: {stats.NeedsBackfill}");
        Console.WriteLine($"  unresolved legacy chunk refs: {stats.UnresolvedLegacyReferences}");
    }

    private static string ReadId(BsonDocument bson, string fieldName)
    {
        if (!bson.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return string.Empty;
        }

        return value.BsonType switch
        {
            BsonType.ObjectId => value.AsObjectId.ToString(),
            BsonType.String => value.AsString,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static List<string> ReadIdArray(BsonDocument bson, string fieldName)
    {
        if (!bson.TryGetValue(fieldName, out var value) || value.IsBsonNull || value.BsonType != BsonType.Array)
        {
            return [];
        }

        return value.AsBsonArray
            .Select(item => item.BsonType switch
            {
                BsonType.ObjectId => item.AsObjectId.ToString(),
                BsonType.String => item.AsString,
                _ => string.Empty
            })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ResolveTargetCourseCode(Domain.Entities.Course course)
    {
        var codeNormalized = AISubjectDomainMapper.NormalizeSubjectOrCourseCode(course.Code);
        var nameNormalized = (course.Name ?? string.Empty).Trim().ToUpperInvariant();

        if (codeNormalized == AISubjectDomainMapper.C || nameNormalized.Contains("PROGRAMMING FUNDAMENTALS", StringComparison.Ordinal))
        {
            return "PRF192";
        }

        if (codeNormalized == AISubjectDomainMapper.C || nameNormalized.Contains("PROGRAMMING FUNDAMENTAL", StringComparison.Ordinal))
        {
            return "PRF192";
        }

        if (codeNormalized == AISubjectDomainMapper.JavaOop ||
            nameNormalized.Contains("OBJECT-ORIENTED PROGRAMMING", StringComparison.Ordinal) ||
            (nameNormalized.Contains("OBJECT", StringComparison.Ordinal) &&
             nameNormalized.Contains("ORIENTED", StringComparison.Ordinal) &&
             nameNormalized.Contains("PROGRAM", StringComparison.Ordinal)))
        {
            return "PRO192";
        }

        if (codeNormalized == AISubjectDomainMapper.DsaJava ||
            ((nameNormalized.Contains("DATA STRUCTURE", StringComparison.Ordinal) ||
              nameNormalized.Contains("DATA STRUCTURES", StringComparison.Ordinal)) &&
             (nameNormalized.Contains("ALGORITHM", StringComparison.Ordinal) ||
              nameNormalized.Contains("ALGORITHMS", StringComparison.Ordinal))))
        {
            return "CSD201";
        }

        return null;
    }

    private sealed record SchemaMigrationReport(
        long UsersMissingWalletBalance,
        long KnowledgeChunksMissingCourseOrUser,
        long FESubmissionsMissingSubmittedAt)
    {
        public bool HasWork =>
            UsersMissingWalletBalance > 0 ||
            KnowledgeChunksMissingCourseOrUser > 0 ||
            FESubmissionsMissingSubmittedAt > 0;
    }

    private sealed record UnusedCollectionsReport(
        IReadOnlyList<string> AllCollections,
        IReadOnlyList<string> CollectionsToDrop);

    private sealed record CourseAiNormalizationReport(
        IReadOnlyList<CourseAiNormalizationItem> Items)
    {
        public bool HasConflicts => Items.Any(item => !string.IsNullOrWhiteSpace(item.ConflictingCourseId));
    }

    private sealed record QuestionBankDocumentLinksReport(
        QuestionBankCollectionStats FeQuestions,
        QuestionBankCollectionStats PeQuestions)
    {
        public bool HasWork =>
            FeQuestions.WithLegacyField > 0 ||
            PeQuestions.WithLegacyField > 0 ||
            FeQuestions.NeedsBackfill > 0 ||
            PeQuestions.NeedsBackfill > 0;
    }

    private sealed record QuestionBankCollectionStats(
        int Total,
        int WithLegacyField,
        int WithDocumentLinks,
        int NeedsBackfill,
        int UnresolvedLegacyReferences);

    private sealed record CourseAiNormalizationItem(
        string CourseId,
        string CourseName,
        string CurrentCode,
        string TargetCode,
        string TargetSubject,
        int TargetLanguageId,
        long DocumentCount,
        long ChunkCount,
        long DraftCount,
        long ContextPackCount,
        long PeQuestionCount,
        string? ConflictingCourseId,
        string? ConflictingCourseName,
        string? ConflictingCourseCode);
}
