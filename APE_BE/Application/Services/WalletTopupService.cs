using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public class WalletTopupService
{
    public const long MinTopupAmountVnd = 10_000;
    public const long MaxTopupAmountVnd = 10_000_000;
    public static readonly TimeSpan TopupTimeout = TimeSpan.FromMinutes(5);

    private readonly IUserRepository _userRepository;
    private readonly ITopupPackageRepository _topupPackageRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPayOSService _payOSService;

    public WalletTopupService(
        IUserRepository userRepository,
        ITopupPackageRepository topupPackageRepository,
        IPaymentRepository paymentRepository,
        IPayOSService payOSService)
    {
        _userRepository = userRepository;
        _topupPackageRepository = topupPackageRepository;
        _paymentRepository = paymentRepository;
        _payOSService = payOSService;
    }

    public WalletTopupConstraintsDto GetConstraints()
    {
        return new WalletTopupConstraintsDto
        {
            MinAmountVnd = MinTopupAmountVnd,
            MaxAmountVnd = MaxTopupAmountVnd,
            Provider = "PayOS"
        };
    }

    public async Task<List<TopupPackageDto>> GetPackagesAsync(CancellationToken cancellationToken = default)
    {
        var items = await _topupPackageRepository.ListAsync(includeInactive: false, cancellationToken);
        return items.Select(MapPackage).ToList();
    }

    public async Task<WalletTopupSessionDto> CreateTopupAsync(string userId, CreateWalletTopupRequestDto request, CancellationToken cancellationToken = default)
    {
        TopupPackage? package = null;
        var resolvedAmount = request.AmountVnd;

        if (!string.IsNullOrWhiteSpace(request.PackageId))
        {
            package = await _topupPackageRepository.GetByIdAsync(request.PackageId, cancellationToken);
            if (package == null || !package.IsActive)
            {
                throw new InvalidOperationException("The top-up package does not exist or is no longer active.");
            }

            resolvedAmount = package.AmountVnd;
        }

        if (resolvedAmount < MinTopupAmountVnd || resolvedAmount > MaxTopupAmountVnd)
        {
            throw new InvalidOperationException($"The top-up amount must be between {MinTopupAmountVnd:N0} VND and {MaxTopupAmountVnd:N0} VND.");
        }

        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("The account for this top-up could not be found.");

        var createdAt = DateTime.UtcNow;
        var expiresAt = createdAt.Add(TopupTimeout);
        var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L + Random.Shared.Next(100, 999);
        var description = package is null
            ? $"Nap vi APE {resolvedAmount:N0}"
            : $"{package.Name} - Nap vi APE {resolvedAmount:N0}";
        var providerDescription = BuildPayOSDescription(resolvedAmount, package?.Code);

        var providerResult = await _payOSService.CreatePaymentLinkAsync(new PayOSCreatePaymentRequest
        {
            OrderCode = orderCode,
            AmountVnd = resolvedAmount,
            Description = providerDescription,
            ExpiresAt = expiresAt
        }, cancellationToken);

        var finalExpiresAt = ResolveExpiresAt(expiresAt, providerResult.ExpiresAt);

        var payment = new Payment
        {
            UserId = user.Id,
            Provider = "PayOS",
            OrderCode = orderCode,
            AmountVnd = resolvedAmount,
            BalanceBeforeVnd = user.AiWalletBalanceVnd,
            BalanceAfterVnd = user.AiWalletBalanceVnd,
            Description = description,
            PackageId = package?.Id,
            PackageCode = package?.Code,
            PackageName = package?.Name,
            CheckoutUrl = providerResult.CheckoutUrl,
            QrCode = providerResult.QrCode,
            PaymentLinkId = providerResult.PaymentLinkId,
            Status = PaymentStatus.Pending,
            ExpiresAt = finalExpiresAt,
            CreatedAt = createdAt
        };

        await _paymentRepository.CreateAsync(payment, cancellationToken);

        return MapSession(payment);
    }

    public async Task<List<WalletPaymentHistoryItemDto>> GetHistoryAsync(string userId, string? status, CancellationToken cancellationToken = default)
    {
        await SyncPendingPaymentsAsync(userId, cancellationToken);
        await ExpirePendingPaymentsAsync(cancellationToken);

        PaymentStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<PaymentStatus>(status, true, out var statusValue))
            {
                throw new InvalidOperationException("Invalid payment status.");
            }

            parsedStatus = statusValue;
        }

        var payments = await _paymentRepository.ListByUserAsync(userId, parsedStatus, 50, cancellationToken);
        return payments.Select(MapHistoryItem).ToList();
    }

    public async Task<WalletPaymentHistoryItemDto?> GetByIdAsync(string userId, string paymentId, CancellationToken cancellationToken = default)
    {
        await SyncPendingPaymentsAsync(userId, cancellationToken);
        await ExpirePendingPaymentsAsync(cancellationToken);

        var payment = await _paymentRepository.GetByIdAsync(paymentId, cancellationToken);
        if (payment == null || payment.UserId != userId)
        {
            return null;
        }

        return MapHistoryItem(payment);
    }

    public async Task<WalletPaymentHistoryItemDto> CancelAsync(string userId, string paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepository.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException("Transaction not found.");

        if (payment.UserId != userId)
        {
            throw new InvalidOperationException("You do not have permission to cancel this transaction.");
        }

        if (payment.IsWalletCredited || payment.Status == PaymentStatus.Completed)
        {
            throw new InvalidOperationException("This transaction has already been paid successfully and cannot be cancelled.");
        }

        if (payment.Status == PaymentStatus.Cancelled)
        {
            return MapHistoryItem(payment);
        }

        if (payment.Status == PaymentStatus.Failed || payment.Status == PaymentStatus.Expired)
        {
            return MapHistoryItem(payment);
        }

        var providerResult = await _payOSService.CancelPaymentAsync(payment.OrderCode, "Student cancelled from APE checkout.", cancellationToken);

        payment.Status = PaymentStatus.Cancelled;
        payment.FailureReason = "You cancelled this transaction on PayOS.";
        payment.ProcessedAt = DateTime.UtcNow;
        payment.LastStatusCheckedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;

        if (providerResult != null)
        {
            payment.PaymentLinkId ??= providerResult.PaymentLinkId;
            payment.ProviderTransactionId ??= providerResult.Reference;
        }

        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        return MapHistoryItem(payment);
    }

    public async Task<bool> HandleWebhookAsync(string rawPayload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return false;
        }

        var verified = await _payOSService.VerifyWebhookAsync(rawPayload, cancellationToken);
        var payment = await _paymentRepository.GetByOrderCodeAsync(verified.OrderCode, cancellationToken);
        if (payment == null)
        {
            return false;
        }

        payment.WebhookPayload = rawPayload;
        payment.ProviderTransactionId ??= verified.Reference ?? verified.PaymentLinkId;
        payment.PaymentLinkId ??= verified.PaymentLinkId;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.LastStatusCheckedAt = DateTime.UtcNow;

        if (payment.IsWalletCredited)
        {
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            return true;
        }

        if (verified.AmountVnd > 0 && payment.AmountVnd != verified.AmountVnd)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = "The paid amount does not match the created transaction.";
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            return true;
        }

        if (!verified.Success || !string.Equals(verified.Code, "00", StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = ResolveFailureStatus(verified.Code);
            payment.FailureReason = verified.Description ?? "Payment was not successful.";
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            return true;
        }

        var user = await _userRepository.GetByIdAsync(payment.UserId);
        if (user == null)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = "The destination account for this top-up could not be found.";
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            return true;
        }

        payment.Status = PaymentStatus.Completed;
        payment.PaidAt = DateTime.UtcNow;
        payment.ProcessedAt = DateTime.UtcNow;
        payment.IsWalletCredited = true;
        payment.FailureReason = null;
        payment.BalanceBeforeVnd = user.AiWalletBalanceVnd;
        user.AiWalletBalanceVnd += payment.AmountVnd;
        payment.BalanceAfterVnd = user.AiWalletBalanceVnd;

        await _userRepository.UpdateAsync(user);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        return true;
    }

    public async Task<int> ExpirePendingPaymentsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredPayments = await _paymentRepository.ListExpiredPendingAsync(now, cancellationToken);
        if (expiredPayments.Count == 0)
        {
            return 0;
        }

        foreach (var payment in expiredPayments)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason ??= "The transaction expired after 5 minutes without PayOS confirmation.";
            payment.ProcessedAt ??= now;
            payment.LastStatusCheckedAt = now;
            payment.UpdatedAt = now;
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
        }

        return expiredPayments.Count;
    }

    public async Task<int> SyncPendingPaymentsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var pendingPayments = await _paymentRepository.ListByUserAsync(userId, PaymentStatus.Pending, 50, cancellationToken);
        if (pendingPayments.Count == 0)
        {
            return 0;
        }

        var updatedCount = 0;
        foreach (var payment in pendingPayments)
        {
            var providerStatus = await _payOSService.GetPaymentStatusAsync(payment.OrderCode, cancellationToken);
            if (providerStatus == null)
            {
                continue;
            }

            payment.LastStatusCheckedAt = DateTime.UtcNow;
            payment.PaymentLinkId ??= providerStatus.PaymentLinkId;
            payment.ProviderTransactionId ??= providerStatus.Reference;

            if (providerStatus.AmountVnd > 0 && providerStatus.AmountVnd != payment.AmountVnd)
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = "The paid amount does not match the created transaction.";
                payment.ProcessedAt ??= DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment, cancellationToken);
                updatedCount++;
                continue;
            }

            if (providerStatus.IsPaid)
            {
                var completed = await HandleCompletedPaymentAsync(payment, cancellationToken);
                if (completed)
                {
                    updatedCount++;
                }
                continue;
            }

            var mappedStatus = MapProviderStatus(providerStatus.Status);
            if (mappedStatus.HasValue && mappedStatus.Value != PaymentStatus.Pending && payment.Status != mappedStatus.Value)
            {
                payment.Status = mappedStatus.Value;
                payment.FailureReason = mappedStatus.Value switch
                {
                    PaymentStatus.Cancelled => "You cancelled this transaction on PayOS.",
                    PaymentStatus.Expired => "The transaction expired on PayOS.",
                    PaymentStatus.Failed => "The transaction failed on PayOS.",
                    _ => payment.FailureReason
                };
                payment.ProcessedAt ??= DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment, cancellationToken);
                updatedCount++;
            }
        }

        return updatedCount;
    }

    private static WalletTopupSessionDto MapSession(Payment payment)
    {
        return new WalletTopupSessionDto
        {
            PaymentId = payment.Id,
            OrderCode = payment.OrderCode,
            PackageId = payment.PackageId,
            PackageCode = payment.PackageCode,
            PackageName = payment.PackageName,
            AmountVnd = payment.AmountVnd,
            CheckoutUrl = payment.CheckoutUrl ?? string.Empty,
            QrCode = payment.QrCode,
            Status = payment.Status,
            CreatedAt = payment.CreatedAt,
            ExpiresAt = payment.ExpiresAt,
            MinAmountVnd = MinTopupAmountVnd,
            MaxAmountVnd = MaxTopupAmountVnd
        };
    }

    private static WalletPaymentHistoryItemDto MapHistoryItem(Payment payment)
    {
        return new WalletPaymentHistoryItemDto
        {
            Id = payment.Id,
            OrderCode = payment.OrderCode,
            PackageId = payment.PackageId,
            PackageCode = payment.PackageCode,
            PackageName = payment.PackageName,
            AmountVnd = payment.AmountVnd,
            Provider = payment.Provider,
            Status = payment.Status,
            Description = payment.Description,
            IsWalletCredited = payment.IsWalletCredited,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt,
            ProcessedAt = payment.ProcessedAt,
            ExpiresAt = payment.ExpiresAt,
            FailureReason = payment.FailureReason,
            BalanceBeforeVnd = payment.BalanceBeforeVnd,
            BalanceAfterVnd = payment.BalanceAfterVnd,
            CheckoutUrl = payment.CheckoutUrl
        };
    }

    private static PaymentStatus ResolveFailureStatus(string? code)
    {
        if (string.Equals(code, "CANCELLED", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentStatus.Cancelled;
        }

        if (string.Equals(code, "EXPIRED", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentStatus.Expired;
        }

        return PaymentStatus.Failed;
    }

    private async Task<bool> HandleCompletedPaymentAsync(Payment payment, CancellationToken cancellationToken)
    {
        payment.UpdatedAt = DateTime.UtcNow;

        if (payment.IsWalletCredited)
        {
            if (payment.Status != PaymentStatus.Completed)
            {
                payment.Status = PaymentStatus.Completed;
                payment.FailureReason = null;
                payment.ProcessedAt ??= DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment, cancellationToken);
                return true;
            }

            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            return false;
        }

        var user = await _userRepository.GetByIdAsync(payment.UserId);
        if (user == null)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = "The destination account for this top-up could not be found.";
            payment.ProcessedAt ??= DateTime.UtcNow;
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            return true;
        }

        payment.Status = PaymentStatus.Completed;
        payment.PaidAt ??= DateTime.UtcNow;
        payment.ProcessedAt = DateTime.UtcNow;
        payment.IsWalletCredited = true;
        payment.FailureReason = null;
        payment.BalanceBeforeVnd = user.AiWalletBalanceVnd;
        user.AiWalletBalanceVnd += payment.AmountVnd;
        payment.BalanceAfterVnd = user.AiWalletBalanceVnd;

        await _userRepository.UpdateAsync(user);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        return true;
    }

    private static PaymentStatus? MapProviderStatus(string? providerStatus)
    {
        if (string.IsNullOrWhiteSpace(providerStatus))
        {
            return null;
        }

        return providerStatus.Trim().ToUpperInvariant() switch
        {
            "PAID" => PaymentStatus.Completed,
            "CANCELLED" => PaymentStatus.Cancelled,
            "CANCELED" => PaymentStatus.Cancelled,
            "EXPIRED" => PaymentStatus.Expired,
            "FAILED" => PaymentStatus.Failed,
            "PENDING" => PaymentStatus.Pending,
            _ => null
        };
    }

    private static DateTime ResolveExpiresAt(DateTime localExpiresAt, DateTime? providerExpiresAt)
    {
        if (providerExpiresAt is null)
        {
            return localExpiresAt;
        }

        return providerExpiresAt.Value < localExpiresAt ? providerExpiresAt.Value : localExpiresAt;
    }

    private static TopupPackageDto MapPackage(TopupPackage item)
    {
        return new TopupPackageDto
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            Description = item.Description,
            AmountVnd = item.AmountVnd,
            SortOrder = item.SortOrder,
            IsActive = item.IsActive,
            IsFeatured = item.IsFeatured
        };
    }

    private static string BuildPayOSDescription(long amountVnd, string? packageCode)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(packageCode)
            ? "TOPUP"
            : new string(packageCode.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            normalizedCode = "TOPUP";
        }

        var description = $"APE {normalizedCode} {amountVnd}";
        return description.Length <= 25 ? description : description[..25];
    }
}
