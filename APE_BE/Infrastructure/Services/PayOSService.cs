using Application.Interfaces;
using Application.Options;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using AppPayOSOptions = Application.Options.PayOSOptions;
using System.Text.Json;

namespace Infrastructure.Services;

public class PayOSService : IPayOSService
{
    private readonly AppPayOSOptions _options;
    private readonly PayOSClient _client;

    public PayOSService(IOptions<AppPayOSOptions> options)
    {
        _options = options.Value;
        ValidateConfiguration();
        _client = new PayOSClient(new PayOS.PayOSOptions
        {
            ClientId = _options.ClientId,
            ApiKey = _options.ApiKey,
            ChecksumKey = _options.ChecksumKey
        });
    }

    public async Task<PayOSCreatePaymentResult> CreatePaymentLinkAsync(PayOSCreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var paymentRequest = new CreatePaymentLinkRequest
        {
            OrderCode = request.OrderCode,
            Amount = request.AmountVnd,
            Description = request.Description,
            ReturnUrl = _options.ReturnUrl,
            CancelUrl = _options.CancelUrl,
            ExpiredAt = request.ExpiresAt is null ? null : new DateTimeOffset(request.ExpiresAt.Value).ToUnixTimeSeconds()
        };

        try
        {
            var paymentLink = await _client.PaymentRequests.CreateAsync(
                paymentRequest,
                new RequestOptions<CreatePaymentLinkRequest>
                {
                    CancellationToken = cancellationToken
                });

            return new PayOSCreatePaymentResult
            {
                CheckoutUrl = paymentLink.CheckoutUrl,
                QrCode = paymentLink.QrCode,
                PaymentLinkId = paymentLink.PaymentLinkId,
                ExpiresAt = paymentLink.ExpiredAt is null
                    ? null
                    : DateTimeOffset.FromUnixTimeSeconds(paymentLink.ExpiredAt.Value).UtcDateTime
            };
        }
        catch (ApiException ex)
        {
            throw new InvalidOperationException(
                $"PayOS khong tao duoc giao dich. {ExtractApiErrorMessage(ex)}",
                ex);
        }
    }

    public async Task<PayOSWebhookVerificationResult> VerifyWebhookAsync(string rawPayload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            throw new InvalidOperationException("Webhook payload is empty.");
        }

        var webhook = System.Text.Json.JsonSerializer.Deserialize<Webhook>(rawPayload, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Webhook payload is invalid.");

        try
        {
            var verified = await _client.Webhooks.VerifyAsync(webhook);
            return new PayOSWebhookVerificationResult
            {
                OrderCode = verified.OrderCode,
                AmountVnd = verified.Amount,
                Success = webhook.Success,
                Code = webhook.Code,
                Description = webhook.Description,
                Reference = verified.Reference,
                PaymentLinkId = verified.PaymentLinkId
            };
        }
        catch (InvalidSignatureException ex)
        {
            throw new InvalidOperationException("Chu ky webhook PayOS khong hop le.", ex);
        }
        catch (WebhookException ex)
        {
            throw new InvalidOperationException("Webhook PayOS khong hop le.", ex);
        }
    }

    public async Task<PayOSPaymentStatusResult?> GetPaymentStatusAsync(long orderCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var paymentLink = await _client.PaymentRequests.GetAsync(orderCode, new RequestOptions
            {
                CancellationToken = cancellationToken
            });

            var payload = JsonSerializer.SerializeToElement(paymentLink);
            var status = TryReadString(payload, "status");
            var paymentLinkId = TryReadString(payload, "paymentLinkId");
            var reference = TryReadString(payload, "reference");
            var amount = TryReadLong(payload, "amount");
            var amountPaid = TryReadLong(payload, "amountPaid");
            var statusText = status?.Trim();

            return new PayOSPaymentStatusResult
            {
                OrderCode = orderCode,
                AmountVnd = amount,
                Status = statusText,
                PaymentLinkId = paymentLinkId,
                Reference = reference,
                IsPaid = amountPaid > 0 || string.Equals(statusText, "PAID", StringComparison.OrdinalIgnoreCase)
            };
        }
        catch (ApiException)
        {
            return null;
        }
    }

    public async Task<PayOSPaymentStatusResult?> CancelPaymentAsync(long orderCode, string? reason, CancellationToken cancellationToken = default)
    {
        try
        {
            var paymentLink = await _client.PaymentRequests.CancelAsync(orderCode, reason ?? "User cancelled payment.", new RequestOptions<CancelPaymentLinkRequest>
            {
                CancellationToken = cancellationToken
            });

            var payload = JsonSerializer.SerializeToElement(paymentLink);
            var status = TryReadString(payload, "status");
            var paymentLinkId = TryReadString(payload, "paymentLinkId");
            var reference = TryReadString(payload, "reference");
            var amount = TryReadLong(payload, "amount");
            var amountPaid = TryReadLong(payload, "amountPaid");
            var statusText = status?.Trim();

            return new PayOSPaymentStatusResult
            {
                OrderCode = orderCode,
                AmountVnd = amount,
                Status = statusText,
                PaymentLinkId = paymentLinkId,
                Reference = reference,
                IsPaid = amountPaid > 0 || string.Equals(statusText, "PAID", StringComparison.OrdinalIgnoreCase)
            };
        }
        catch (ApiException)
        {
            return null;
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.ChecksumKey))
        {
            throw new InvalidOperationException("PayOS chua duoc cau hinh day du ClientId, ApiKey va ChecksumKey.");
        }

        if (string.IsNullOrWhiteSpace(_options.ReturnUrl) || string.IsNullOrWhiteSpace(_options.CancelUrl))
        {
            throw new InvalidOperationException("PayOS chua duoc cau hinh ReturnUrl hoac CancelUrl.");
        }
    }

    private static string? TryReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static long TryReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return long.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static string ExtractApiErrorMessage(ApiException ex)
    {
        var response = ex.Message?.Trim();
        if (string.IsNullOrWhiteSpace(response))
        {
            return "Vui long kiem tra cau hinh hoac du lieu gui sang PayOS.";
        }

        return response;
    }
}
