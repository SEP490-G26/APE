using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace Infrastructure.Persistence;

public class FEQuestionRepository : IFEQuestionRepository
{
    private readonly IMongoCollection<FEQuestion> _col;
    public FEQuestionRepository(DbContext context) => _col = context.FEQuestions;

    public async Task<FEQuestion?> GetByIdAsync(string id) =>
        await _col.Find(q => q.Id == id).FirstOrDefaultAsync();

    public async Task<List<FEQuestion>> GetByIdsAsync(List<string> ids) =>
        await _col.Find(q => ids.Contains(q.Id)).ToListAsync();
    public async Task<FEQuestion?> GetByTitleAsync(string title) =>
    await _col.Find(q => q.Title == title).FirstOrDefaultAsync();
    public async Task<FEQuestion?> GetByFingerprintAsync(string courseId, string fingerprint) =>
        await _col.Find(q => q.CourseId == courseId && q.QuestionFingerprint == fingerprint).FirstOrDefaultAsync();
    public async Task<FEQuestion?> GetByTitleInScopeAsync(string sourceScope, string? courseId, string? ownerUserId, string title) =>
        await _col.Find(BuildScopeFilter(sourceScope, courseId, ownerUserId) & Builders<FEQuestion>.Filter.Eq(q => q.Title, title)).FirstOrDefaultAsync();
    public async Task<FEQuestion?> GetByFingerprintInScopeAsync(string sourceScope, string? courseId, string? ownerUserId, string fingerprint) =>
        await _col.Find(BuildScopeFilter(sourceScope, courseId, ownerUserId) & Builders<FEQuestion>.Filter.Eq(q => q.QuestionFingerprint, fingerprint)).FirstOrDefaultAsync();

    public async Task CreateAsync(FEQuestion question) =>
        await _col.InsertOneAsync(question);

    public async Task InsertManyAsync(List<FEQuestion> questions) =>
        await _col.InsertManyAsync(questions);

    public async Task<List<string>> GetAllTitlesAsync() =>
        await _col.Find(_ => true).Project(q => q.Title).ToListAsync();

    public async Task<(List<FEQuestion> Items, long Total)> ListAsync(string? status, string? courseId, string? difficulty, int page, int limit)
    {
        var filter = Builders<FEQuestion>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(status)) filter = Builders<FEQuestion>.Filter.Eq(q => q.Status, status);
        if (!string.IsNullOrWhiteSpace(courseId)) filter = filter & Builders<FEQuestion>.Filter.Eq(q => q.CourseId, courseId);
        if (!string.IsNullOrWhiteSpace(difficulty)) filter = filter & Builders<FEQuestion>.Filter.Eq(q => q.Difficulty, difficulty);
        var sort = Builders<FEQuestion>.Sort
            .Descending(q => q.LastModifiedAt)
            .Descending(q => q.CreatedAt);

        var skip = (Math.Max(1, page) - 1) * Math.Max(1, limit);
        var items = await _col.Find(filter)
            .Sort(sort)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
        var total = await _col.CountDocumentsAsync(filter);
        return (items, (long)total);
    }

    public async Task UpdateAsync(FEQuestion question) =>
        await _col.ReplaceOneAsync(q => q.Id == question.Id, question);

    public async Task DeleteAsync(string id) =>
        await _col.DeleteOneAsync(q => q.Id == id);

    public async Task<(List<FEQuestion> Items, long Total)> ListAdminOwnedAsync(string? status, string? courseId, string? difficulty, int page, int limit)
    {
        var filter = BuildAdminOwnedFilter(status, courseId, difficulty);
        var sort = Builders<FEQuestion>.Sort
            .Descending(q => q.LastModifiedAt)
            .Descending(q => q.CreatedAt);

        var skip = (Math.Max(1, page) - 1) * Math.Max(1, limit);
        var items = await _col.Find(filter)
            .Sort(sort)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
        var total = await _col.CountDocumentsAsync(filter);
        return (items, (long)total);
    }

    public async Task<List<FEQuestion>> ListAdminOwnedAllAsync(string? status, string? courseId, string? difficulty)
    {
        var filter = BuildAdminOwnedFilter(status, courseId, difficulty);
        var sort = Builders<FEQuestion>.Sort
            .Descending(q => q.LastModifiedAt)
            .Descending(q => q.CreatedAt);

        return await _col.Find(filter).Sort(sort).ToListAsync();
    }

    public async Task<(List<FEQuestion> Items, long Total)> ListOwnedByStudentAsync(string ownerUserId, string? courseId, string? difficulty, string? status, IReadOnlyCollection<string>? topicTags, string? keyword, int page, int limit)
    {
        var filter = BuildOwnedStudentFilter(ownerUserId, courseId, difficulty, status, topicTags, keyword);
        var sort = Builders<FEQuestion>.Sort
            .Descending(q => q.LastModifiedAt)
            .Descending(q => q.CreatedAt);
        var skip = (Math.Max(1, page) - 1) * Math.Max(1, limit);
        var items = await _col.Find(filter)
            .Sort(sort)
            .Skip(skip)
            .Limit(Math.Max(1, limit))
            .ToListAsync();
        var total = await _col.CountDocumentsAsync(filter);
        return (items, (long)total);
    }

    public async Task<List<FEQuestion>> ListOwnedByStudentAllAsync(string ownerUserId, string? courseId, string? difficulty, string? status, IReadOnlyCollection<string>? topicTags, string? keyword)
    {
        var filter = BuildOwnedStudentFilter(ownerUserId, courseId, difficulty, status, topicTags, keyword);
        var sort = Builders<FEQuestion>.Sort
            .Descending(q => q.LastModifiedAt)
            .Descending(q => q.CreatedAt);

        return await _col.Find(filter).Sort(sort).ToListAsync();
    }

    private static FilterDefinition<FEQuestion> BuildAdminOwnedFilter(string? status, string? courseId, string? difficulty)
    {
        var filter = Builders<FEQuestion>.Filter.Eq(q => q.Source, "Admin");
        if (!string.IsNullOrWhiteSpace(status)) filter &= Builders<FEQuestion>.Filter.Eq(q => q.Status, status);
        if (!string.IsNullOrWhiteSpace(courseId)) filter &= Builders<FEQuestion>.Filter.Eq(q => q.CourseId, courseId);
        if (!string.IsNullOrWhiteSpace(difficulty)) filter &= Builders<FEQuestion>.Filter.Eq(q => q.Difficulty, difficulty);
        return filter;
    }

    public async Task<List<string>> GetOwnedStudentTopicTagsAsync(string ownerUserId, string? courseId)
    {
        var filter = BuildOwnedStudentFilter(ownerUserId, courseId, null, null, null, null);
        var items = await _col.Find(filter)
            .Project(q => q.TopicTags)
            .ToListAsync();

        return items
            .SelectMany(tags => tags ?? new List<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static FilterDefinition<FEQuestion> BuildScopeFilter(string sourceScope, string? courseId, string? ownerUserId)
    {
        var filter = Builders<FEQuestion>.Filter.Eq(q => q.SourceScope, sourceScope);
        if (string.Equals(sourceScope, "BYOS", StringComparison.OrdinalIgnoreCase))
        {
            return filter & Builders<FEQuestion>.Filter.Eq(q => q.OwnerUserId, ownerUserId);
        }

        return filter & Builders<FEQuestion>.Filter.Eq(q => q.CourseId, courseId ?? string.Empty);
    }

    private static FilterDefinition<FEQuestion> BuildOwnedStudentFilter(string ownerUserId, string? courseId, string? difficulty, string? status, IReadOnlyCollection<string>? topicTags, string? keyword)
    {
        var filters = new List<FilterDefinition<FEQuestion>>
        {
            Builders<FEQuestion>.Filter.Eq(q => q.OwnerUserId, ownerUserId),
            Builders<FEQuestion>.Filter.Eq(q => q.Source, "Student"),
            Builders<FEQuestion>.Filter.Ne(q => q.Status, "Deleted")
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            filters.Add(Builders<FEQuestion>.Filter.Eq(q => q.Status, status.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(courseId))
        {
            filters.Add(Builders<FEQuestion>.Filter.Eq(q => q.CourseId, courseId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            filters.Add(Builders<FEQuestion>.Filter.Eq(q => q.Difficulty, difficulty.Trim()));
        }

        var normalizedTags = (topicTags ?? Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedTags.Count > 0)
        {
            filters.Add(Builders<FEQuestion>.Filter.AnyIn(q => q.TopicTags, normalizedTags));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filters.Add(Builders<FEQuestion>.Filter.Regex(q => q.Title, new BsonRegularExpression(Regex.Escape(keyword.Trim()), "i")));
        }

        return Builders<FEQuestion>.Filter.And(filters);
    }
}
