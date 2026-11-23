namespace NotificationGateway.Domain.Entities;

public class NotificationOutbox
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid NotificationId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public bool IsProcessed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int RetryCount { get; private set; }

    private NotificationOutbox() { }

    public static NotificationOutbox Create(Guid tenantId, Guid notificationId, string eventType, string payloadJson)
    {
        return new NotificationOutbox
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            NotificationId = notificationId,
            EventType = eventType,
            PayloadJson = payloadJson,
            IsProcessed = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkProcessed() { IsProcessed = true; ProcessedAt = DateTime.UtcNow; }
    public void IncrementRetry()  { RetryCount++; }
}
