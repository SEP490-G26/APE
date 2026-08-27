namespace Application.Interfaces;

public interface IPayOSService
{
    Task<PayOSCreatePaymentResult> CreatePaymentLinkAsync(PayOSCreatePaymentRequest request, CancellationToken cancellationToken = default);
    Task<PayOSWebhookVerificationResult> VerifyWebhookAsync(string rawPayload, CancellationToken cancellationToken = default);
    Task<PayOSPaymentStatusResult?> GetPaymentStatusAsync(long orderCode, CancellationToken cancellationToken = default);
    Task<PayOSPaymentStatusResult?> CancelPaymentAsync(long orderCode, string? reason, CancellationToken cancellationToken = default);
}

public class PayOSCreatePaymentRequest
{
    public long OrderCode { get; set; }
    public long AmountVnd { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}

public class PayOSCreatePaymentResult
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public string? QrCode { get; set; }
    public string? PaymentLinkId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class PayOSWebhookVerificationResult
{
    public long OrderCode { get; set; }
    public long AmountVnd { get; set; }
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public string? PaymentLinkId { get; set; }
}

public class PayOSPaymentStatusResult
{
    public long OrderCode { get; set; }
    public long AmountVnd { get; set; }
    public string? Status { get; set; }
    public string? PaymentLinkId { get; set; }
    public string? Reference { get; set; }
    public bool IsPaid { get; set; }
}
