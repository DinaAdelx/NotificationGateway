namespace NotificationGateway.Domain.Enums;

public enum NotificationStatus
{
    Accepted,
    Queued,
    Processing,
    Sent,
    Delivered,
    Failed,
    Retrying,
    Cancelled,
    Unknown,
    DeadLettered
}
