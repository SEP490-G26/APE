using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class FESubmissionRepository : IFESubmissionRepository
{
    private readonly IMongoCollection<FE_Submission> _col;
    public FESubmissionRepository(DbContext context) => _col = context.FESubmissions;

    public async Task CreateAsync(FE_Submission sub) => await _col.InsertOneAsync(sub);
    public async Task<FE_Submission?> GetByIdAsync(string id) =>
        await _col.Find(s => s.Id == id).FirstOrDefaultAsync();
    public async Task<List<FE_Submission>> GetBySessionIdAsync(string sessionId) =>
        await _col.Find(s => s.SessionId == sessionId).ToListAsync();
    public async Task<int> CountByUserIdAsync(string userId) =>
    (int)await _col.CountDocumentsAsync(FilterDefinition<FE_Submission>.Empty);
    public async Task<List<FE_Submission>> GetByUserIdAsync(string userId) =>
        await _col.Find(FilterDefinition<FE_Submission>.Empty).ToListAsync();

}