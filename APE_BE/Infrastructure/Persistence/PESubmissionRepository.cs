using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public sealed class PESubmissionRepository
    : IPESubmissionRepository
{
    private const string UniqueAttemptIndexName =
        "ux_pe_submission_session_question_attempt";

    private const string LegacyUniqueAttemptIndexName =
        "UX_PE_Submission_Attempt";

    private static readonly string[] AttemptKeyFieldNames =
    [
        "SessionId",
        "QuestionId",
        "AttemptCount"
    ];

    private readonly IMongoCollection<PE_Submission> _collection;

    public PESubmissionRepository(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _collection = context.PE_Submissions;
    }

    public Task CreateAsync(
        PE_Submission submission,
        CancellationToken cancellationToken = default)
    {
        return _collection.InsertOneAsync(
            submission,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> TryCreateAsync(
        PE_Submission submission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        try
        {
            await _collection.InsertOneAsync(
                submission,
                cancellationToken: cancellationToken);

            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MongoWriteException exception)
            when (IsAttemptAllocationConflict(exception))
        {
            return false;
        }
    }

    public async Task<PE_Submission?> GetByIdAsync(
      string id,
      CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var normalizedId = id.Trim();

        return await _collection
            .Find(item => item.Id == normalizedId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PE_Submission>>
        GetBySessionIdAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(item => item.SessionId == sessionId)
            .SortByDescending(item => item.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetLatestAttemptNumberAsync(
        string sessionId,
        string questionId,
        CancellationToken cancellationToken = default)
    {
        var latest = await _collection
            .Find(item =>
                item.SessionId == sessionId &&
                item.QuestionId == questionId)
            .SortByDescending(item => item.AttemptCount)
            .Project(item => item.AttemptCount)
            .FirstOrDefaultAsync(cancellationToken);

        return latest;
    }

    public async Task<IReadOnlyCollection<PE_Submission>>
        GetByStatusesAsync(
            IReadOnlyCollection<SubmissionProcessingStatus> statuses,
            int limit,
            CancellationToken cancellationToken = default)
    {
        if (statuses.Count == 0 || limit <= 0)
            return [];

        var filter =
            Builders<PE_Submission>.Filter.In(
                item => item.Status,
                statuses);

        return await _collection
            .Find(filter)
            .SortBy(item => item.SubmittedAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        PE_Submission submission,
        CancellationToken cancellationToken = default)
    {
        // Keep this generic replacement for unrelated legacy callers only.
        // Lease-protected grading transitions must use the CAS methods below.
        var result = await _collection.ReplaceOneAsync(
            item => item.Id == submission.Id,
            submission,
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            throw new InvalidOperationException(
                $"PE submission '{submission.Id}' was not found.");
        }
    }

    public async Task<bool> TryPersistExecutionStateAsync(
        PE_Submission submission,
        string leaseOwner,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        ValidateLeasePersistenceArguments(
            submission,
            leaseOwner,
            utcNow);

        var result = await _collection.UpdateOneAsync(
            BuildActiveLeaseFilter(
                submission.Id,
                leaseOwner,
                utcNow),
            BuildExecutionStateUpdate(submission),
            cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    public async Task<bool> TryPersistTerminalStateAsync(
        PE_Submission submission,
        string leaseOwner,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        ValidateLeasePersistenceArguments(
            submission,
            leaseOwner,
            utcNow);

        var result = await _collection.UpdateOneAsync(
            BuildActiveLeaseFilter(
                submission.Id,
                leaseOwner,
                utcNow),
            BuildTerminalStateUpdate(submission),
            cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    public async Task<PE_Submission?> TryAcquirePendingAsync(
        string submissionId,
        string leaseOwner,
        DateTime leaseAcquiredAt,
        DateTime leaseExpiresAt,
        CancellationToken cancellationToken = default)
    {
        ValidateLeaseArguments(
            submissionId,
            leaseOwner,
            leaseAcquiredAt,
            leaseExpiresAt);

        var filter = Builders<PE_Submission>.Filter.And(
            Builders<PE_Submission>.Filter.Eq(
                item => item.Id,
                submissionId),
            Builders<PE_Submission>.Filter.Eq(
                item => item.Status,
                SubmissionProcessingStatus.Pending));

        var update = Builders<PE_Submission>.Update
            .Set(
                item => item.Status,
                SubmissionProcessingStatus.Processing)
            .Set(
                item => item.ProcessingStartedAt,
                (DateTime?)leaseAcquiredAt)
            .Set(
                item => item.LeaseOwner,
                leaseOwner.Trim())
            .Set(
                item => item.LeaseAcquiredAt,
                (DateTime?)leaseAcquiredAt)
            .Set(
                item => item.LeaseExpiresAt,
                (DateTime?)leaseExpiresAt)
            .Set(
                item => item.CompletedAt,
                (DateTime?)null)
            .Set(
                item => item.FinalVerdict,
                (SubmissionVerdict?)null)
            .Set(
                item => item.ProcessingError,
                (string?)null)
            .Inc(
                "Execution.AttemptCount",
                1)
            .Set(
                "Execution.LastAttemptAt",
                leaseAcquiredAt)
            .Unset(
                "Execution.LastError");

        return await _collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<PE_Submission>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<PE_Submission?> TryReclaimExpiredProcessingAsync(
        string submissionId,
        string leaseOwner,
        DateTime leaseAcquiredAt,
        DateTime leaseExpiresAt,
        CancellationToken cancellationToken = default)
    {
        ValidateLeaseArguments(
            submissionId,
            leaseOwner,
            leaseAcquiredAt,
            leaseExpiresAt);

        var trimmedOwner = leaseOwner.Trim();

        var filter = Builders<PE_Submission>.Filter.And(
            Builders<PE_Submission>.Filter.Eq(
                item => item.Id,
                submissionId.Trim()),
            Builders<PE_Submission>.Filter.Eq(
                item => item.Status,
                SubmissionProcessingStatus.Processing),
            Builders<PE_Submission>.Filter.Or(
                Builders<PE_Submission>.Filter.Lt(
                    item => item.LeaseExpiresAt,
                    (DateTime?)leaseAcquiredAt),
                Builders<PE_Submission>.Filter.Eq(
                    item => item.LeaseExpiresAt,
                    (DateTime?)null),
                Builders<PE_Submission>.Filter.Eq(
                    item => item.LeaseOwner,
                    null),
                Builders<PE_Submission>.Filter.Eq(
                    item => item.LeaseOwner,
                    string.Empty)));

        var update = Builders<PE_Submission>.Update
            .Set(
                item => item.LeaseOwner,
                trimmedOwner)
            .Set(
                item => item.LeaseAcquiredAt,
                (DateTime?)leaseAcquiredAt)
            .Set(
                item => item.LeaseExpiresAt,
                (DateTime?)leaseExpiresAt);

        return await _collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<PE_Submission>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<bool> RenewLeaseAsync(
        string submissionId,
        string leaseOwner,
        DateTime utcNow,
        DateTime newLeaseExpiresAt,
        CancellationToken cancellationToken = default)
    {
        ValidateRenewalArguments(
            submissionId,
            leaseOwner,
            utcNow,
            newLeaseExpiresAt);

        var filter = Builders<PE_Submission>.Filter.And(
            Builders<PE_Submission>.Filter.Eq(
                item => item.Id,
                submissionId.Trim()),
            Builders<PE_Submission>.Filter.Eq(
                item => item.Status,
                SubmissionProcessingStatus.Processing),
            Builders<PE_Submission>.Filter.Eq(
                item => item.LeaseOwner,
                leaseOwner.Trim()),
            Builders<PE_Submission>.Filter.Gt(
                item => item.LeaseExpiresAt,
                (DateTime?)utcNow));

        var update = Builders<PE_Submission>.Update.Set(
            item => item.LeaseExpiresAt,
            (DateTime?)newLeaseExpiresAt);

        var result = await _collection.UpdateOneAsync(
            filter,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    private static FilterDefinition<PE_Submission> BuildActiveLeaseFilter(
        string submissionId,
        string leaseOwner,
        DateTime utcNow)
    {
        return Builders<PE_Submission>.Filter.And(
            Builders<PE_Submission>.Filter.Eq(
                item => item.Id,
                submissionId.Trim()),
            Builders<PE_Submission>.Filter.Eq(
                item => item.Status,
                SubmissionProcessingStatus.Processing),
            Builders<PE_Submission>.Filter.Eq(
                item => item.LeaseOwner,
                leaseOwner.Trim()),
            Builders<PE_Submission>.Filter.Gt(
                item => item.LeaseExpiresAt,
                (DateTime?)utcNow));
    }

    private static UpdateDefinition<PE_Submission> BuildExecutionStateUpdate(
        PE_Submission submission)
    {
        var document = submission.ToBsonDocument();

        return Builders<PE_Submission>.Update
            .Set(
                "Execution",
                (BsonDocument)document["Execution"].DeepClone())
            .Set(
                item => item.ProcessingError,
                submission.ProcessingError);
    }

    private static UpdateDefinition<PE_Submission> BuildTerminalStateUpdate(
        PE_Submission submission)
    {
        var document = submission.ToBsonDocument();

        return Builders<PE_Submission>.Update
            .Set(
                item => item.Status,
                submission.Status)
            .Set(
                item => item.FinalVerdict,
                submission.FinalVerdict)
            .Set(
                item => item.TestCasesPassed,
                submission.TestCasesPassed)
            .Set(
                item => item.TotalTestCases,
                submission.TotalTestCases)
            .Set(
                item => item.QuestionScore,
                submission.QuestionScore)
            .Set(
                item => item.RuntimeMs,
                submission.RuntimeMs)
            .Set(
                item => item.MemoryKb,
                submission.MemoryKb)
            .Set(
                "TestResultItems",
                (BsonArray)document["TestResultItems"].DeepClone())
            .Set(
                item => item.CompletedAt,
                submission.CompletedAt)
            .Set(
                item => item.ProcessingError,
                submission.ProcessingError)
            .Set(
                "Execution",
                (BsonDocument)document["Execution"].DeepClone())
            .Unset(
                item => item.LeaseOwner)
            .Unset(
                item => item.LeaseAcquiredAt)
            .Unset(
                item => item.LeaseExpiresAt);
    }

    private static void ValidateLeaseArguments(
        string submissionId,
        string leaseOwner,
        DateTime leaseAcquiredAt,
        DateTime leaseExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(submissionId))
        {
            throw new ArgumentException(
                "SubmissionId is required.",
                nameof(submissionId));
        }

        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            throw new ArgumentException(
                "LeaseOwner is required.",
                nameof(leaseOwner));
        }

        if (leaseAcquiredAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "LeaseAcquiredAt must use UTC.",
                nameof(leaseAcquiredAt));
        }

        if (leaseExpiresAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "LeaseExpiresAt must use UTC.",
                nameof(leaseExpiresAt));
        }

        if (leaseExpiresAt <= leaseAcquiredAt)
        {
            throw new ArgumentException(
                "LeaseExpiresAt must be later than LeaseAcquiredAt.",
                nameof(leaseExpiresAt));
        }
    }

    private static void ValidateRenewalArguments(
        string submissionId,
        string leaseOwner,
        DateTime utcNow,
        DateTime newLeaseExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(submissionId))
        {
            throw new ArgumentException(
                "SubmissionId is required.",
                nameof(submissionId));
        }

        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            throw new ArgumentException(
                "LeaseOwner is required.",
                nameof(leaseOwner));
        }

        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "UtcNow must use UTC.",
                nameof(utcNow));
        }

        if (newLeaseExpiresAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "NewLeaseExpiresAt must use UTC.",
                nameof(newLeaseExpiresAt));
        }

        if (newLeaseExpiresAt <= utcNow)
        {
            throw new ArgumentException(
                "NewLeaseExpiresAt must be later than UtcNow.",
                nameof(newLeaseExpiresAt));
        }
    }

    private static void ValidateLeasePersistenceArguments(
        PE_Submission submission,
        string leaseOwner,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (string.IsNullOrWhiteSpace(submission.Id))
        {
            throw new ArgumentException(
                "Submission.Id is required.",
                nameof(submission));
        }

        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            throw new ArgumentException(
                "LeaseOwner is required.",
                nameof(leaseOwner));
        }

        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "UtcNow must use UTC.",
                nameof(utcNow));
        }
    }

    private static bool IsAttemptAllocationConflict(
        MongoWriteException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return IsAttemptAllocationConflict(
            exception.WriteError?.Category,
            exception.WriteError?.Code,
            exception.WriteError?.Details,
            exception.WriteError?.Message,
            exception.Message);
    }

    private static bool IsAttemptAllocationConflict(
        ServerErrorCategory? category,
        int? code,
        BsonDocument? details,
        string? writeErrorMessage,
        string? exceptionMessage)
    {
        if (!IsDuplicateKeyError(category, code))
        {
            return false;
        }

        if (HasAttemptConflictDetails(details))
        {
            return true;
        }

        return MessageSuggestsAttemptConflict(writeErrorMessage) ||
               MessageSuggestsAttemptConflict(exceptionMessage);
    }

    private static bool IsDuplicateKeyError(
        ServerErrorCategory? category,
        int? code)
    {
        return category == ServerErrorCategory.DuplicateKey &&
               (code == 11000 ||
                code == 11001 ||
                code == 12582);
    }

    private static bool HasAttemptConflictDetails(BsonDocument? details)
    {
        if (details is null || details.ElementCount == 0)
        {
            return false;
        }

        if (TryGetIndexName(details, out var indexName) &&
            IsKnownAttemptIndexName(indexName))
        {
            return true;
        }

        return ContainsAttemptKeyDocument(details);
    }

    private static bool TryGetIndexName(
        BsonDocument document,
        out string? indexName)
    {
        foreach (var element in document.Elements)
        {
            if (element.Value is BsonString bsonString &&
                IsIndexNameField(element.Name))
            {
                indexName = bsonString.Value;
                return true;
            }

            if (element.Value is BsonDocument nestedDocument &&
                TryGetIndexName(nestedDocument, out indexName))
            {
                return true;
            }

            if (element.Value is BsonArray array)
            {
                foreach (var item in array)
                {
                    if (item is BsonDocument itemDocument &&
                        TryGetIndexName(itemDocument, out indexName))
                    {
                        return true;
                    }
                }
            }
        }

        indexName = null;
        return false;
    }

    private static bool ContainsAttemptKeyDocument(BsonDocument document)
    {
        if (MatchesAttemptKeyDocument(document))
        {
            return true;
        }

        foreach (var element in document.Elements)
        {
            if (element.Value is BsonDocument nestedDocument &&
                ContainsAttemptKeyDocument(nestedDocument))
            {
                return true;
            }

            if (element.Value is BsonArray array)
            {
                foreach (var item in array)
                {
                    if (item is BsonDocument itemDocument &&
                        ContainsAttemptKeyDocument(itemDocument))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool MatchesAttemptKeyDocument(BsonDocument document)
    {
        if (document.ElementCount != AttemptKeyFieldNames.Length)
        {
            return false;
        }

        var names = document.Names.ToArray();
        return names.SequenceEqual(
            AttemptKeyFieldNames,
            StringComparer.Ordinal);
    }

    private static bool MessageSuggestsAttemptConflict(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (IsKnownAttemptIndexName(message))
        {
            return true;
        }

        return AttemptKeyFieldNames.All(fieldName =>
            message.Contains(
                fieldName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsKnownAttemptIndexName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains(
                   UniqueAttemptIndexName,
                   StringComparison.OrdinalIgnoreCase) ||
               value.Contains(
                   LegacyUniqueAttemptIndexName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIndexNameField(string fieldName)
    {
        return fieldName.Equals(
                   "index",
                   StringComparison.OrdinalIgnoreCase) ||
               fieldName.Equals(
                   "indexName",
                   StringComparison.OrdinalIgnoreCase) ||
               fieldName.Equals(
                   "name",
                   StringComparison.OrdinalIgnoreCase);
    }
}
