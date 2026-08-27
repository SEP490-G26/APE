using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class CourseRepository : ICourseRepository
{
    private readonly IMongoCollection<Course> _collection;

    public CourseRepository(DbContext context)
    {
        _collection = context.Courses;
    }

    public async Task<Course> CreateAsync(Course course)
    {
        await _collection.InsertOneAsync(course);
        return course;
    }

    public async Task<Course?> GetByIdAsync(string id)
    {
        return await _collection
            .Find(x => x.Id == id && x.Status != "Deleted")
            .FirstOrDefaultAsync();
    }

    public async Task<Course?> GetByCodeAsync(string code)
    {
        return await _collection
            .Find(x => x.Code == code && x.Status != "Deleted")
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ExistsByCodeAsync(
        string code,
        string? excludeId = null)
    {
        var filter = Builders<Course>.Filter.And(
            Builders<Course>.Filter.Eq(x => x.Code, code),
            Builders<Course>.Filter.Ne(x => x.Status, "Deleted"));

        if (!string.IsNullOrWhiteSpace(excludeId))
        {
            filter &= Builders<Course>.Filter.Ne(x => x.Id, excludeId);
        }

        return await _collection.Find(filter).AnyAsync();
    }

    public async Task<(IReadOnlyList<Course> Items, long Total)> ListAsync(
        int page,
        int pageSize,
        string? keyword = null,
        string? status = null)
    {
        var builder = Builders<Course>.Filter;

        var filter = builder.Ne(x => x.Status, "Deleted");

        if (!string.IsNullOrWhiteSpace(status))
        {

            filter &= builder.Eq(x => x.Status, status);

            filter = Builders<Course>.Filter.Regex(
                c => c.Status,
                new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(status)}$", "i"));

        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filter &= builder.Or(
                builder.Regex(x => x.Name, new MongoDB.Bson.BsonRegularExpression(keyword, "i")),
                builder.Regex(x => x.Code, new MongoDB.Bson.BsonRegularExpression(keyword, "i")));
        }

        var total = await _collection.CountDocumentsAsync(filter);

        var items = await _collection
            .Find(filter)
            .SortBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return (items, total);
    }


    public async Task<IReadOnlyList<Course>> GetAllAsync(
        bool includeDeleted = false)
    {
        FilterDefinition<Course> filter = FilterDefinition<Course>.Empty;

        if (!includeDeleted)
        {
            filter = Builders<Course>.Filter.Ne(x => x.Status, "Deleted");
        }

        return await _collection
            .Find(filter)
            .SortBy(x => x.Name)
            .ToListAsync();
    }

    public async Task UpdateAsync(Course course)
    {
        var update = Builders<Course>.Update
            .Set(x => x.Name, course.Name)
            .Set(x => x.Description, course.Description)
            .Set(x => x.ExamMatrix, course.ExamMatrix)
            .Set(x => x.LastModifiedBy, course.LastModifiedBy)
            .Set(x => x.LastModifiedAt, course.LastModifiedAt);

        await _collection.UpdateOneAsync(
            x => x.Id == course.Id,
            update);
    }

    public async Task<bool> SoftDeleteAsync(string id, string? deletedBy)
    {
        var update = Builders<Course>.Update
            .Set(x => x.Status, "Deleted")
            .Set(x => x.DeletedBy, deletedBy)
            .Set(x => x.DeletedAt, DateTime.UtcNow);

       
        var result = await _collection.UpdateOneAsync(
            x => x.Id == id && x.Status != "Deleted",
            update);

        return result.MatchedCount > 0;
    }

    public async Task<bool> UpdateStatusAsync(string id, string status, string? modifiedBy)
    {
        var update = Builders<Course>.Update
            .Set(x => x.Status, status)
            .Set(x => x.LastModifiedBy, modifiedBy)
            .Set(x => x.LastModifiedAt, DateTime.UtcNow);

        var result = await _collection.UpdateOneAsync(
            x => x.Id == id && x.Status != "Deleted",
            update);

        return result.MatchedCount > 0;
    }




  
}

