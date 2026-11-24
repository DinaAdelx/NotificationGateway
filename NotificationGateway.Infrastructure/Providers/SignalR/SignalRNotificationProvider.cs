using Microsoft.AspNetCore.SignalR;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Infrastructure.Providers.SignalR;

public class NotificationHub : Hub
{
    public async Task JoinTenantGroup(string tenantId, string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"{tenantId}:{userId}");
        if (!string.IsNullOrWhiteSpace(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }

    public async Task AcknowledgeNotification(string notificationId)
    {
        await Clients.Others.SendAsync("OnNotificationAcknowledged", notificationId);
    }
}

public class SignalRNotificationProvider(IHubContext<NotificationHub> hubContext) : INotificationProvider
{
    public string ProviderCode => "SIGNALR";
    public NotificationChannel Channel => NotificationChannel.SignalR;
    public ProviderCapabilities Capabilities => new(false, false, false);

    public async Task<ProviderSendResult> SendAsync(
        RenderedNotification notification,
        TenantProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            var eventPayload = new
            {
                id = notification.DeliveryId.ToString(),
                notificationId = notification.NotificationId.ToString(),
                deliveryId = notification.DeliveryId.ToString(),
                title = notification.Subject ?? "Gateway Notification",
                subject = notification.Subject ?? "Gateway Notification",
                message = notification.Body,
                body = notification.Body,
                channel = "SignalR",
                recipientAddress = notification.RecipientAddress,
                timestamp = DateTime.UtcNow,
                type = 1,
                category = "Gateway",
                isRead = false,
                metadata = notification.MetadataJson
            };

            // 1. Broadcast to tenant:user group
            var group = $"{notification.TenantId}:{notification.RecipientAddress}";
            await hubContext.Clients.Group(group).SendAsync("OnNotificationReceived", eventPayload, cancellationToken);
            await hubContext.Clients.Group(group).SendAsync("ReceiveNotification", eventPayload, cancellationToken);

            // 2. Also broadcast to direct userId group
            await hubContext.Clients.Group(notification.RecipientAddress).SendAsync("OnNotificationReceived", eventPayload, cancellationToken);

            // 3. Broadcast to all connected clients for testing dashboard
            await hubContext.Clients.All.SendAsync("OnNotificationReceived", eventPayload, cancellationToken);
            await hubContext.Clients.All.SendAsync("ReceiveNotification", eventPayload, cancellationToken);

            return new ProviderSendResult(
                true,
                $"signalr-{notification.DeliveryId}",
                "Delivered",
                false,
                FailureCategory.None,
                null,
                null);
        }
        catch (Exception ex)
        {
            return new ProviderSendResult(
                false,
                null,
                "Failed",
                true,
                FailureCategory.ProviderUnavailable,
                "SIGNALR_ERROR",
                ex.Message);
        }
    }
}
