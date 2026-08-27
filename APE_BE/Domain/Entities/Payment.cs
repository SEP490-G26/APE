using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Domain.Enums;

namespace Domain.Entities;

public class Payment
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    public string Provider { get; set; } = "PayOS";
    public long OrderCode { get; set; }
    public long AmountVnd { get; set; }
    public long BalanceBeforeVnd { get; set; }
    public long BalanceAfterVnd { get; set; }
    public string Description { get; set; } = null!;
    [BsonRepresentation(BsonType.ObjectId)]
    public string? PackageId { get; set; }
    public string? PackageCode { get; set; }
    public string? PackageName { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? PaymentLinkId { get; set; }
    public string? QrCode { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? WebhookPayload { get; set; }
    public string? FailureReason { get; set; }
    public bool IsWalletCredited { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? LastStatusCheckedAt { get; set; }

    [BsonRepresentation(BsonType.String)]
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
