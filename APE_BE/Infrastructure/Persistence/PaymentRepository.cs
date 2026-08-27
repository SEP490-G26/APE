using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class PaymentRepository : IPaymentRepository
{
    private readonly IMongoCollection<Payment> _payments;

    public PaymentRepository(DbContext dbContext)
    {
        _payments = dbContext.Payments;
    }

    public Task CreateAsync(Payment payment, CancellationToken cancellationToken = default)
        => _payments.InsertOneAsync(payment, cancellationToken: cancellationToken);

    public Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
        => _payments.ReplaceOneAsync(item => item.Id == payment.Id, payment, cancellationToken: cancellationToken);

    public async Task<Payment?> GetByIdAsync(string paymentId, CancellationToken cancellationToken = default)
        => await _payments.Find(item => item.Id == paymentId).FirstOrDefaultAsync(cancellationToken);

    public async Task<Payment?> GetByOrderCodeAsync(long orderCode, CancellationToken cancellationToken = default)
        => await _payments.Find(item => item.OrderCode == orderCode).FirstOrDefaultAsync(cancellationToken);

    public Task<List<Payment>> ListByUserAsync(string userId, int limit, CancellationToken cancellationToken = default)
        => ListByUserAsync(userId, null, limit, cancellationToken);

    public Task<List<Payment>> ListByUserAsync(string userId, PaymentStatus? status, int limit, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Payment>.Filter.Eq(item => item.UserId, userId);
        if (status.HasValue)
        {
            filter &= Builders<Payment>.Filter.Eq(item => item.Status, status.Value);
        }

        return _payments.Find(filter)
            .SortByDescending(item => item.CreatedAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Payment>> ListExpiredPendingAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Payment>.Filter.Eq(item => item.Status, PaymentStatus.Pending) &
                     Builders<Payment>.Filter.Eq(item => item.IsWalletCredited, false) &
                     Builders<Payment>.Filter.Lte(item => item.ExpiresAt, utcNow);

        return _payments.Find(filter).ToListAsync(cancellationToken);
    }
}
