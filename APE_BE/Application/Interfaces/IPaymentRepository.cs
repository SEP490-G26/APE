using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces;

public interface IPaymentRepository
{
    Task CreateAsync(Payment payment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdAsync(string paymentId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByOrderCodeAsync(long orderCode, CancellationToken cancellationToken = default);
    Task<List<Payment>> ListByUserAsync(string userId, int limit, CancellationToken cancellationToken = default);
    Task<List<Payment>> ListByUserAsync(string userId, PaymentStatus? status, int limit, CancellationToken cancellationToken = default);
    Task<List<Payment>> ListExpiredPendingAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
