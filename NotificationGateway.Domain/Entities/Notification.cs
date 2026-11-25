using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Entities;

public class Notification
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid NotificationTypeDefinitionId { get; private set; }
    public string NotificationTypeCode { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public NotificationPriority Priority { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ScheduledAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public ICollection<NotificationDelivery> Deliveries { get; private set; } = new List<NotificationDelivery>();

    private Notification() { }

    public static Notification Create(
        Guid tenantId, Guid applicationId, Guid notificationTypeId, string typeCode,
        int schemaVersion, string payloadJson, NotificationPriority priority,
        string? idempotencyKey = null, string? correlationId = null,
        string? metadataJson = null, DateTime? scheduledAt = null, Guid? userId = null)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            UserId = userId,
            NotificationTypeDefinitionId = notificationTypeId,
            NotificationTypeCode = typeCode,
            SchemaVersion = schemaVersion,
            PayloadJson = payloadJson,
            Priority = priority,
            Status = NotificationStatus.Accepted,
            IdempotencyKey = idempotencyKey,
            CorrelationId = correlationId,
            MetadataJson = metadataJson,
            CreatedAt = DateTime.UtcNow,
            ScheduledAt = scheduledAt
        };
    }

    public void MarkQueued()      { Status = NotificationStatus.Queued;     }
    public void MarkProcessing()  { Status = NotificationStatus.Processing; }
    public void MarkSent()        { Status = NotificationStatus.Sent;       CompletedAt = DateTime.UtcNow; }
    public void MarkFailed()      { Status = NotificationStatus.Failed;     CompletedAt = DateTime.UtcNow; }
    public void MarkDelivered()   { Status = NotificationStatus.Delivered;  CompletedAt = DateTime.UtcNow; }
    public void MarkDeadLettered(){ Status = NotificationStatus.DeadLettered; CompletedAt = DateTime.UtcNow; }

    public void Cancel()
    {
        if (Status is NotificationStatus.Accepted or NotificationStatus.Queued)
        {
            Status = NotificationStatus.Cancelled;
            CancelledAt = DateTime.UtcNow;
        }
    }
}
