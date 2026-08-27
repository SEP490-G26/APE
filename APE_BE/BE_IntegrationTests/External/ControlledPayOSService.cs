using Application.Interfaces;

namespace BE_IntegrationTests.External;

public sealed class ControlledPayOSService : IPayOSService
{
    private readonly object _gate = new();

    public PayOSCreatePaymentResult CreatePaymentLinkResult { get; set; } = new()
    {
        CheckoutUrl = "https://payos.test/checkout/integration",
        QrCode = "integration-qr",
        PaymentLinkId = "integration-payment-link"
    };

    public PayOSWebhookVerificationResult VerifyWebhookResult { get; set; } = new()
    {
        OrderCode = 0,
        AmountVnd = 0,
        Success = true,
        Code = "00",
        Description = "Success",
        Reference = "integration-webhook-reference",
        PaymentLinkId = "integration-payment-link"
    };

    public PayOSPaymentStatusResult? CancelPaymentResult { get; set; } = new()
    {
        Status = "CANCELLED",
        PaymentLinkId = "integration-payment-link",
        Reference = "integration-cancel-reference"
    };

    public Dictionary<long, PayOSPaymentStatusResult?> PaymentStatuses { get; } = new();

    public Task<PayOSCreatePaymentResult> CreatePaymentLinkAsync(PayOSCreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PayOSCreatePaymentResult
        {
            CheckoutUrl = CreatePaymentLinkResult.CheckoutUrl,
            QrCode = CreatePaymentLinkResult.QrCode,
            PaymentLinkId = CreatePaymentLinkResult.PaymentLinkId,
            ExpiresAt = CreatePaymentLinkResult.ExpiresAt ?? request.ExpiresAt
        });
    }

    public Task<PayOSWebhookVerificationResult> VerifyWebhookAsync(string rawPayload, CancellationToken cancellationToken = default)
        => Task.FromResult(VerifyWebhookResult);

    public Task<PayOSPaymentStatusResult?> GetPaymentStatusAsync(long orderCode, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(PaymentStatuses.TryGetValue(orderCode, out var result) ? result : null);
        }
    }

    public Task<PayOSPaymentStatusResult?> CancelPaymentAsync(long orderCode, string? reason, CancellationToken cancellationToken = default)
    {
        if (CancelPaymentResult is null)
        {
            return Task.FromResult<PayOSPaymentStatusResult?>(null);
        }

        return Task.FromResult<PayOSPaymentStatusResult?>(new PayOSPaymentStatusResult
        {
            OrderCode = orderCode,
            AmountVnd = CancelPaymentResult.AmountVnd,
            Status = CancelPaymentResult.Status,
            PaymentLinkId = CancelPaymentResult.PaymentLinkId,
            Reference = CancelPaymentResult.Reference,
            IsPaid = CancelPaymentResult.IsPaid
        });
    }

    public void Reset()
    {
        lock (_gate)
        {
            PaymentStatuses.Clear();
        }

        CreatePaymentLinkResult = new PayOSCreatePaymentResult
        {
            CheckoutUrl = "https://payos.test/checkout/integration",
            QrCode = "integration-qr",
            PaymentLinkId = "integration-payment-link"
        };

        VerifyWebhookResult = new PayOSWebhookVerificationResult
        {
            OrderCode = 0,
            AmountVnd = 0,
            Success = true,
            Code = "00",
            Description = "Success",
            Reference = "integration-webhook-reference",
            PaymentLinkId = "integration-payment-link"
        };

        CancelPaymentResult = new PayOSPaymentStatusResult
        {
            Status = "CANCELLED",
            PaymentLinkId = "integration-payment-link",
            Reference = "integration-cancel-reference"
        };
    }
}
