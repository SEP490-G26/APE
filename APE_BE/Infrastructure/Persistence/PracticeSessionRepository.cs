using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public sealed class PracticeSessionRepository
    : IPracticeSessionRepository
{
    private readonly IMongoCollection<PracticeSession> _col;

    public PracticeSessionRepository(DbContext context)
    {
        _col = context.PracticeSessions;
    }

    public Task CreateAsync(
        PracticeSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        return _col.InsertOneAsync(
            session,
            cancellationToken: cancellationToken);
    }

    public async Task<PracticeSession?> GetByIdAsync(
     string id,
     CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var normalizedId = id.Trim();

        return await _col
            .Find(session => session.Id == normalizedId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PracticeSession?> GetActiveSessionAsync(
    string userId,
    string examId,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(examId))
        {
            return null;
        }

        var normalizedUserId = userId.Trim();
        var normalizedExamId = examId.Trim();

        return await _col
            .Find(session =>
                session.StudentId == normalizedUserId &&
                session.ExamId == normalizedExamId &&
                session.Status == SessionStatus.InProgress)
            .SortByDescending(session => session.StartTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        PracticeSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var result = await _col.ReplaceOneAsync(
            item => item.Id == session.Id,
            session,
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            throw new InvalidOperationException(
                $"Practice session '{session.Id}' was not found.");
        }
    }

    public async Task<List<PracticeSession>> GetByUserIdAsync(
        string userId,
        int page = 1,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        return await _col
            .Find(session => session.StudentId == userId)
            .SortByDescending(session => session.StartTime)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        var count = await _col.CountDocumentsAsync(
            session => session.StudentId == userId,
            cancellationToken: cancellationToken);

        return checked((int)count);
    }

    public async Task<PracticeSession?> EndSessionAsync(
        string sessionId,
        DateTime endTime,
        double? totalScore = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        var update = Builders<PracticeSession>.Update
            .Set(session => session.Status, SessionStatus.Submitted)
            .Set(session => session.EndTime, endTime);

        if (totalScore.HasValue)
        {
            update = update.Set(
                session => session.TotalScore,
                totalScore.Value);
        }

        var options =
            new FindOneAndUpdateOptions<PracticeSession>
            {
                ReturnDocument = ReturnDocument.After
            };

        return await _col.FindOneAndUpdateAsync(
            session => session.Id == sessionId,
            update,
            options,
            cancellationToken);
    }

    public Task<PracticeSession?>
        GetInProgressByUserAndExamAsync(
            string userId,
            string examId,
            CancellationToken cancellationToken = default)
    {
        return GetActiveSessionAsync(
            userId,
            examId,
            cancellationToken);
    }

    public async Task<List<PracticeSession>> GetByExamIdAsync(
        string examId,
        int page = 1,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(examId))
            return [];

        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        return await _col
            .Find(session => session.ExamId == examId)
            .SortByDescending(session => session.StartTime)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }
}
