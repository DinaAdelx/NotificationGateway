using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Infrastructure.Providers.InApp;

/// <summary>Database-backed In-App provider. The delivery record IS the in-app notification.</summary>
public class InAppProvider : INotificationProvider
{
    public string ProviderCode => "DATABASE_INAPP";
    public NotificationChannel Channel => NotificationChannel.InApp;
    public ProviderCapabilities Capabilities => new(false, false, false);

    public Task<ProviderSendResult> SendAsync(
        RenderedNotification notification,
        TenantProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        // Delivery is persisted by the dispatcher; just confirm success here
        return Task.FromResult(new ProviderSendResult(
            true, $"inapp-{notification.DeliveryId}",
            "stored", false, FailureCategory.None, null, null));
    }
}
