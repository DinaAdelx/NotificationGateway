using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Application.Abstractions;

/// <summary>Rendered, channel-ready notification content passed to a provider.</summary>
public record RenderedNotification(
    Guid TenantId,
    Guid NotificationId,
    Guid DeliveryId,
    NotificationChannel Channel,
    string RecipientAddress,
    string? Subject,
    string Body,
    string? MetadataJson);

public record ProviderCapabilities(
    bool SupportsDeliveryReceipt,
    bool SupportsScheduling,
    bool SupportsHtml);

public record ProviderSendResult(
    bool IsSuccess,
    string? ProviderMessageId,
    string? ProviderStatus,
    bool IsRetryable,
    FailureCategory FailureCategory,
    string? ErrorCode,
    string? SafeErrorMessage);

public interface INotificationProvider
{
    string ProviderCode { get; }
    NotificationChannel Channel { get; }
    ProviderCapabilities Capabilities { get; }

    Task<ProviderSendResult> SendAsync(
        RenderedNotification notification,
        TenantProviderConfiguration configuration,
        CancellationToken cancellationToken);
}
