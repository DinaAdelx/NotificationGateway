using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Entities;

public class DeliveryAttempt
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid DeliveryId { get; private set; }
    public int AttemptNumber { get; private set; }
    public string ProviderCode { get; private set; } = string.Empty;
    public string? ProviderMessageId { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public FailureCategory FailureCategory { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? SafeErrorMessage { get; private set; }
    public bool IsRetryable { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int? DurationMilliseconds { get; private set; }

    public NotificationDelivery Delivery { get; private set; } = null!;

    private DeliveryAttempt() { }

    public static DeliveryAttempt Create(Guid tenantId, Guid deliveryId, int attemptNumber, string providerCode)
    {
        return new DeliveryAttempt
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeliveryId = deliveryId,
            AttemptNumber = attemptNumber,
            ProviderCode = providerCode,
            Status = DeliveryStatus.Processing,
            FailureCategory = FailureCategory.None,
            StartedAt = DateTime.UtcNow
        };
    }

    public void MarkSuccess(string? providerMessageId)
    {
        Status = DeliveryStatus.Sent;
        ProviderMessageId = providerMessageId;
        CompletedAt = DateTime.UtcNow;
        DurationMilliseconds = (int)(CompletedAt.Value - StartedAt).TotalMilliseconds;
    }

    public void MarkFailure(FailureCategory category, string? errorCode, string? safeMessage, bool isRetryable)
    {
        Status = DeliveryStatus.Failed;
        FailureCategory = category;
        ErrorCode = errorCode;
        SafeErrorMessage = safeMessage;
        IsRetryable = isRetryable;
        CompletedAt = DateTime.UtcNow;
        DurationMilliseconds = (int)(CompletedAt.Value - StartedAt).TotalMilliseconds;
    }
}
