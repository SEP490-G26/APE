using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public sealed class ExamRepository : IExamRepository
{
    private readonly IMongoCollection<Exam> _collection;

    public ExamRepository(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _collection = context.Exams;
    }

    private static FilterDefinition<Exam> NotDeletedFilter(FilterDefinitionBuilder<Exam> builder)
    {
        // Backward-compatible: older documents may not have IsDeleted field at all.
        return builder.Or(
            builder.Eq(exam => exam.IsDeleted, false),
            builder.Exists(nameof(Exam.IsDeleted), false));
    }

    public Task InsertAsync(
        Exam exam,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exam);

        return _collection.InsertOneAsync(
            exam,
            cancellationToken: cancellationToken);
    }

    public async Task<(List<Exam> Items, long Total)> ListAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        var builder = Builders<Exam>.Filter;
        var filter = builder.And(builder.Empty, NotDeletedFilter(builder));

        var total = await _collection.CountDocumentsAsync(
            filter,
            cancellationToken: cancellationToken);

        var items = await _collection
            .Find(filter)
            .SortByDescending(exam => exam.CreatedAt)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(List<Exam> Items, long Total)> ListByCourseAsync(
        string courseId,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        var builder = Builders<Exam>.Filter;
        var courseFilter = string.IsNullOrWhiteSpace(courseId)
            ? builder.Empty
            : builder.Eq(exam => exam.CourseId, courseId.Trim());

        var filter = builder.And(courseFilter, NotDeletedFilter(builder));

        var total = await _collection.CountDocumentsAsync(
            filter,
            cancellationToken: cancellationToken);

        var items = await _collection
            .Find(filter)
            .SortByDescending(exam => exam.CreatedAt)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(List<Exam> Items, long Total)> ListPublicAsync(
        string? courseId,
        string? examType,
        string? mode,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        var builder = Builders<Exam>.Filter;
        var filters = new List<FilterDefinition<Exam>>
        {
            builder.Eq(exam => exam.Visibility, ExamVisibility.Public),
            NotDeletedFilter(builder)
        };

        if (!string.IsNullOrWhiteSpace(courseId))
        {
            filters.Add(builder.Eq(exam => exam.CourseId, courseId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(examType))
        {
            filters.Add(builder.Regex(
                nameof(Exam.ExamType),
                new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(examType.Trim())}$", "i")));
        }

        if (!string.IsNullOrWhiteSpace(mode))
        {
            filters.Add(builder.Regex(
                nameof(Exam.Mode),
                new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(mode.Trim())}$", "i")));
        }

        var filter = filters.Count == 1 ? filters[0] : builder.And(filters);

        var total = await _collection.CountDocumentsAsync(
            filter,
            cancellationToken: cancellationToken);

        var sort = Builders<Exam>.Sort
            .Descending(nameof(Exam.UpdatedAt))
            .Descending(nameof(Exam.CreatedAt));

        var items = await _collection
            .Find(filter)
            .Sort(sort)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(List<Exam> Items, long Total)> ListByCreatorAsync(
        string creatorUserId,
        string? courseId,
        string? examType,
        string? mode,
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        var builder = Builders<Exam>.Filter;
        var filters = new List<FilterDefinition<Exam>>
        {
            builder.Eq(exam => exam.CreatedBy, creatorUserId.Trim()),
            NotDeletedFilter(builder)
        };

        if (!string.IsNullOrWhiteSpace(courseId))
        {
            filters.Add(builder.Eq(exam => exam.CourseId, courseId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(examType))
        {
            filters.Add(builder.Regex(
                nameof(Exam.ExamType),
                new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(examType.Trim())}$", "i")));
        }

        if (!string.IsNullOrWhiteSpace(mode))
        {
            filters.Add(builder.Regex(
                nameof(Exam.Mode),
                new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(mode.Trim())}$", "i")));
        }

        var filter = filters.Count == 1 ? filters[0] : builder.And(filters);

        var total = await _collection.CountDocumentsAsync(
            filter,
            cancellationToken: cancellationToken);

        var sort = Builders<Exam>.Sort
            .Descending(nameof(Exam.UpdatedAt))
            .Descending(nameof(Exam.CreatedAt));

        var items = await _collection
            .Find(filter)
            .Sort(sort)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task ReplaceAsync(
        Exam exam,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exam);

        return _collection.ReplaceOneAsync(
            item => item.Id == exam.Id,
            exam,
            cancellationToken: cancellationToken);
    }

    public async Task<Exam?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return await _collection
            .Find(exam => exam.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
