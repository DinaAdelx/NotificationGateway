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
            var group = $"{notification.TenantId}:{notification.RecipientAddress}";
            await hubContext.Clients.Group(group).SendAsync(
                "ReceiveNotification",
                new
                {
                    notification.NotificationId,
                    notification.DeliveryId,
                    notification.Subject,
                    notification.Body,
                    notification.MetadataJson
                },
                cancellationToken);

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
