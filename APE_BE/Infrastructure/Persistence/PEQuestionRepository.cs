using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace Infrastructure.Persistence;

public sealed class PEQuestionRepository
    : IPEQuestionRepository
{
    private readonly IMongoCollection<PEQuestion> _collection;

    public PEQuestionRepository(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _collection = context.PEQuestions;
    }

    public async Task<PEQuestion?> GetByIdAsync(
    string id,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var normalizedId = id.Trim();

        return await _collection
            .Find(question => question.Id == normalizedId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<PEQuestion>> GetByIdsAsync(
        List<string> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return [];
        }

        var normalizedIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<PEQuestion>.Filter.In(
            question => question.Id,
            normalizedIds);

        return await _collection
            .Find(filter)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<PEQuestion> Items, long Total)> ListAsync(
        string? status,
        string? courseId,
        string? difficulty,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        var filters =
            new List<FilterDefinition<PEQuestion>>();

        if (!string.IsNullOrWhiteSpace(status))
        {
            filters.Add(
                Builders<PEQuestion>.Filter.Eq(
                    question => question.Status,
                    status.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(courseId))
        {
            filters.Add(
                Builders<PEQuestion>.Filter.Eq(
                    question => question.CourseId,
                    courseId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            filters.Add(
                Builders<PEQuestion>.Filter.Eq(
                    question => question.Difficulty,
                    difficulty.Trim()));
        }

        var filter = filters.Count == 0
            ? Builders<PEQuestion>.Filter.Empty
            : Builders<PEQuestion>.Filter.And(filters);
        var sort = Builders<PEQuestion>.Sort
            .Descending(question => question.LastModifiedAt)
            .Descending(question => question.CreatedAt);

        var total = await _collection.CountDocumentsAsync(
            filter,
            cancellationToken: cancellationToken);

        var items = await _collection
            .Find(filter)
            .Sort(sort)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<PEQuestion?> GetByTitleAsync(
     string title,
     CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalizedTitle = title.Trim();

        return await _collection
            .Find(question => question.Title == normalizedTitle)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PEQuestion?> GetByFingerprintAsync(
    string courseId,
    string fingerprint,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(courseId) ||
            string.IsNullOrWhiteSpace(fingerprint))
        {
            return null;
        }

        var normalizedCourseId = courseId.Trim();
        var normalizedFingerprint = fingerprint.Trim();

        return await _collection
            .Find(question =>
                question.CourseId == normalizedCourseId &&
                question.QuestionFingerprint == normalizedFingerprint)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PEQuestion?> GetByTitleInScopeAsync(
    string sourceScope,
    string? courseId,
    string? ownerUserId,
    string title,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalizedTitle = title.Trim();

        var filter =
            BuildScopeFilter(
                sourceScope,
                courseId,
                ownerUserId) &
            Builders<PEQuestion>.Filter.Eq(
                question => question.Title,
                normalizedTitle);

        return await _collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PEQuestion?> GetByFingerprintInScopeAsync(
     string sourceScope,
     string? courseId,
     string? ownerUserId,
     string fingerprint,
     CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return null;
        }

        var normalizedFingerprint = fingerprint.Trim();

        var filter =
            BuildScopeFilter(
                sourceScope,
                courseId,
                ownerUserId) &
            Builders<PEQuestion>.Filter.Eq(
                question => question.QuestionFingerprint,
                normalizedFingerprint);

        return await _collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task CreateAsync(
        PEQuestion question,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);

        return _collection.InsertOneAsync(
            question,
            cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(
        PEQuestion question,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);

        var result = await _collection.ReplaceOneAsync(
            item => item.Id == question.Id,
            question,
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            throw new InvalidOperationException(
                $"PE question '{question.Id}' was not found.");
        }
    }

    public Task DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.CompletedTask;
        }

        return _collection.DeleteOneAsync(
            item => item.Id == id.Trim(),
            cancellationToken);
    }

    public async Task<(List<PEQuestion> Items, long Total)> ListAdminOwnedAsync(
        string? status,
        string? courseId,
        string? difficulty,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        var filters = new List<FilterDefinition<PEQuestion>>
        {
            Builders<PEQuestion>.Filter.Eq(question => question.Source, "Admin")
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            filters.Add(Builders<PEQuestion>.Filter.Eq(question => question.Status, status.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(courseId))
        {
            filters.Add(Builders<PEQuestion>.Filter.Eq(question => question.CourseId, courseId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            filters.Add(Builders<PEQuestion>.Filter.Eq(question => question.Difficulty, difficulty.Trim()));
        }

        var filter = Builders<PEQuestion>.Filter.And(filters);
        var sort = Builders<PEQuestion>.Sort
            .Descending(question => question.LastModifiedAt)
            .Descending(question => question.CreatedAt);

        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var items = await _collection
            .Find(filter)
            .Sort(sort)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<List<PEQuestion>> ListAdminOwnedAllAsync(
        string? status,
        string? courseId,
        string? difficulty,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildAdminOwnedFilter(status, courseId, difficulty);
        var sort = Builders<PEQuestion>.Sort
            .Descending(question => question.LastModifiedAt)
            .Descending(question => question.CreatedAt);

        return await _collection
            .Find(filter)
            .Sort(sort)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<PEQuestion> Items, long Total)> ListOwnedByStudentAsync(
        string ownerUserId,
        string? courseId,
        string? difficulty,
        string? status,
        IReadOnlyCollection<string>? topicTags,
        string? keyword,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        var filter = BuildOwnedStudentFilter(ownerUserId, courseId, difficulty, status, topicTags, keyword);
        var sort = Builders<PEQuestion>.Sort
            .Descending(question => question.LastModifiedAt)
            .Descending(question => question.CreatedAt);

        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var items = await _collection
            .Find(filter)
            .Sort(sort)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<List<PEQuestion>> ListOwnedByStudentAllAsync(
        string ownerUserId,
        string? courseId,
        string? difficulty,
        string? status,
        IReadOnlyCollection<string>? topicTags,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildOwnedStudentFilter(ownerUserId, courseId, difficulty, status, topicTags, keyword);
        var sort = Builders<PEQuestion>.Sort
            .Descending(question => question.LastModifiedAt)
            .Descending(question => question.CreatedAt);

        return await _collection
            .Find(filter)
            .Sort(sort)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<string>> GetOwnedStudentTopicTagsAsync(
        string ownerUserId,
        string? courseId,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildOwnedStudentFilter(ownerUserId, courseId, null, null, null, null);
        var items = await _collection
            .Find(filter)
            .Project(question => question.TopicTags)
            .ToListAsync(cancellationToken);

        return items
            .SelectMany(tags => tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static FilterDefinition<PEQuestion>
        BuildScopeFilter(
            string sourceScope,
            string? courseId,
            string? ownerUserId)
    {
        var normalizedScope =
            string.IsNullOrWhiteSpace(sourceScope)
                ? "SYSTEM"
                : sourceScope.Trim().ToUpperInvariant();

        var filter =
            Builders<PEQuestion>.Filter.Eq(
                question => question.SourceScope,
                normalizedScope);

        if (normalizedScope == "BYOS")
        {
            return filter &
                Builders<PEQuestion>.Filter.Eq(
                    question => question.OwnerUserId,
                    ownerUserId);
        }

        return filter &
            Builders<PEQuestion>.Filter.Eq(
                question => question.CourseId,
                courseId ?? string.Empty);
    }

    private static FilterDefinition<PEQuestion> BuildAdminOwnedFilter(
        string? status,
        string? courseId,
        string? difficulty)
    {
        var filters = new List<FilterDefinition<PEQuestion>>
        {
            Builders<PEQuestion>.Filter.Eq(question => question.Source, "Admin")
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            filters.Add(Builders<PEQuestion>.Filter.Eq(question => question.Status, status.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(courseId))
        {
            filters.Add(Builders<PEQuestion>.Filter.Eq(question => question.CourseId, courseId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            filters.Add(Builders<PEQuestion>.Filter.Eq(question => question.Difficulty, difficulty.Trim()));
        }

        return Builders<PEQuestion>.Filter.And(filters);
    }

    private static FilterDefinition<PEQuestion> BuildOwnedStudentFilter(
        string ownerUserId,
        string? courseId,
        string? difficulty,
        string? status,
        IReadOnlyCollection<string>? topicTags,
        string? keyword)
    {
        var filters = new List<FilterDefinition<PEQuestion>>
        {
            Builders<PEQuestion>.Filter.Eq(question => question.OwnerUserId, ownerUserId),
            Builders<PEQuestion>.Filter.Eq(question => question.Source, "Student"),
            Builders<PEQuestion>.Filter.Ne(question => question.Status, "Deleted")
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            filters.Add(Builders<PEQuestion>.Filter.Eq(question => question.Status, status.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(courseId))
        {
            filters.Add(Builders<PEQuestion>.Filter.Eq(question => question.CourseId, courseId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            filters.Add(Builders<PEQuestion>.Filter.Eq(question => question.Difficulty, difficulty.Trim()));
        }

        var normalizedTags = (topicTags ?? Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedTags.Count > 0)
        {
            filters.Add(Builders<PEQuestion>.Filter.AnyIn(question => question.TopicTags, normalizedTags));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filters.Add(
                Builders<PEQuestion>.Filter.Regex(
                    question => question.Title,
                    new BsonRegularExpression(Regex.Escape(keyword.Trim()), "i")));
        }

        return Builders<PEQuestion>.Filter.And(filters);
    }
}
