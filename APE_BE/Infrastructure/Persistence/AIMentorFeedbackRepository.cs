using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class AIMentorFeedbackRepository : IAIMentorFeedbackRepository
{
    private readonly IMongoCollection<AIMentorFeedback> _collection;

    public AIMentorFeedbackRepository(DbContext context)
    {
        _collection = context.AIMentorFeedbacks;
    }

    public async Task CreateAsync(AIMentorFeedback feedback)
    {
        await _collection.InsertOneAsync(feedback);
    }

    public async Task<AIMentorFeedback?> GetLatestBySubmissionIdAsync(string submissionId)
    {
        return await _collection.Find(item => item.SubmissionId == submissionId)
            .SortByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<AIMentorFeedback>> GetBySubmissionIdAsync(string submissionId, int take = 20)
    {
        take = Math.Max(1, take);
        return await _collection.Find(item => item.SubmissionId == submissionId)
            .SortByDescending(item => item.CreatedAt)
            .Limit(take)
            .ToListAsync();
    }

    public async Task<long> CountBySubmissionIdsAsync(IReadOnlyCollection<string> submissionIds)
    {
        if (submissionIds.Count == 0)
        {
            return 0;
        }

        return await _collection.CountDocumentsAsync(item => submissionIds.Contains(item.SubmissionId));
    }

    public async Task<(List<AIMentorFeedback> Items, long Total)> ListAsync(
        string? submissionId,
        string? verdict,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int limit)
    {
        var filter = Builders<AIMentorFeedback>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(submissionId))
            filter &= Builders<AIMentorFeedback>.Filter.Eq(item => item.SubmissionId, submissionId);
        if (!string.IsNullOrWhiteSpace(verdict))
            filter &= Builders<AIMentorFeedback>.Filter.Eq(item => item.Verdict, verdict);
        if (fromDate.HasValue)
            filter &= Builders<AIMentorFeedback>.Filter.Gte(item => item.CreatedAt, fromDate.Value);
        if (toDate.HasValue)
            filter &= Builders<AIMentorFeedback>.Filter.Lte(item => item.CreatedAt, toDate.Value);

        page = Math.Max(1, page);
        limit = Math.Max(1, limit);
        var skip = (page - 1) * limit;
        var items = await _collection.Find(filter)
            .SortByDescending(item => item.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
        var total = await _collection.CountDocumentsAsync(filter);
        return (items, total);
    }
}
