using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Entities;

public class NotificationDelivery
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid NotificationId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string? RecipientExternalUserId { get; private set; }
    public string RecipientAddress { get; private set; } = string.Empty;
    public Guid? TemplateVersionId { get; private set; }
    public Guid? ProviderConfigurationId { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public Notification Notification { get; private set; } = null!;
    public ICollection<DeliveryAttempt> Attempts { get; private set; } = new List<DeliveryAttempt>();

    private NotificationDelivery() { }

    public static NotificationDelivery Create(
        Guid tenantId, Guid notificationId, NotificationChannel channel,
        string recipientAddress, string? externalUserId = null,
        Guid? templateVersionId = null, Guid? providerConfigurationId = null)
    {
        return new NotificationDelivery
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            NotificationId = notificationId,
            Channel = channel,
            RecipientAddress = recipientAddress,
            RecipientExternalUserId = externalUserId,
            TemplateVersionId = templateVersionId,
            ProviderConfigurationId = providerConfigurationId,
            Status = DeliveryStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void RecordAttempt() => AttemptCount++;
    public void MarkSent()          { Status = DeliveryStatus.Sent;        CompletedAt = DateTime.UtcNow; }
    public void MarkDelivered()     { Status = DeliveryStatus.Delivered;   CompletedAt = DateTime.UtcNow; }
    public void MarkFailed()        { Status = DeliveryStatus.Failed;      CompletedAt = DateTime.UtcNow; }
    public void MarkDeadLettered()  { Status = DeliveryStatus.DeadLettered;CompletedAt = DateTime.UtcNow; }
    public void ScheduleRetry(DateTime nextAt) { Status = DeliveryStatus.Retrying; NextAttemptAt = nextAt; }
    public void MarkProcessing()    { Status = DeliveryStatus.Processing; }
}
