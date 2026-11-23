namespace NotificationGateway.Domain.Enums;

public enum DeliveryStatus
{
    Pending,
    Processing,
    Sent,
    Delivered,
    Failed,
    Retrying,
    Cancelled,
    DeadLettered,
    Unknown
}
