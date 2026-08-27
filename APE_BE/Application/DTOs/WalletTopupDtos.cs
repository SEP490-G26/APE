using Domain.Enums;

namespace Application.DTOs;

public class CreateWalletTopupRequestDto
{
    public string? PackageId { get; set; }
    public long AmountVnd { get; set; }
}

public class TopupPackageDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long AmountVnd { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
}

public class UpsertTopupPackageRequestDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long AmountVnd { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
}

public class UpdateTopupPackageStatusRequestDto
{
    public bool IsActive { get; set; }
}

public class WalletTopupSessionDto
{
    public string PaymentId { get; set; } = string.Empty;
    public long OrderCode { get; set; }
    public string? PackageId { get; set; }
    public string? PackageCode { get; set; }
    public string? PackageName { get; set; }
    public long AmountVnd { get; set; }
    public string CheckoutUrl { get; set; } = string.Empty;
    public string? QrCode { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public long MinAmountVnd { get; set; }
    public long MaxAmountVnd { get; set; }
}

public class WalletPaymentHistoryItemDto
{
    public string Id { get; set; } = string.Empty;
    public long OrderCode { get; set; }
    public string? PackageId { get; set; }
    public string? PackageCode { get; set; }
    public string? PackageName { get; set; }
    public long AmountVnd { get; set; }
    public string Provider { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsWalletCredited { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? FailureReason { get; set; }
    public long BalanceBeforeVnd { get; set; }
    public long BalanceAfterVnd { get; set; }
    public string? CheckoutUrl { get; set; }
}

public class WalletTopupConstraintsDto
{
    public long MinAmountVnd { get; set; }
    public long MaxAmountVnd { get; set; }
    public string Provider { get; set; } = "PayOS";
}
