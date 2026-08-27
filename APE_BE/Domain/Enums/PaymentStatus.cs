namespace Domain.Enums;

public enum PaymentStatus
{
    Pending,
    Completed,
    Confirmed = Completed,
    Failed,
    Cancelled,
    Expired
}
