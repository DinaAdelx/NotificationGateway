using Microsoft.Extensions.Logging;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using System.Text;
using System.Text.Json;

namespace NotificationGateway.Infrastructure.Providers.Push;

public class PushNotificationProvider(HttpClient httpClient, ILogger<PushNotificationProvider> logger) : INotificationProvider
{
    public string ProviderCode => "PUSH";
    public NotificationChannel Channel => NotificationChannel.Push;
    public ProviderCapabilities Capabilities => new(SupportsDeliveryReceipt: true, SupportsScheduling: false, SupportsHtml: false);

    public async Task<ProviderSendResult> SendAsync(
        RenderedNotification notification,
        TenantProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            var cfg = !string.IsNullOrWhiteSpace(configuration.ConfigurationJson) && configuration.ConfigurationJson != "{}"
                ? JsonSerializer.Deserialize<PushConfig>(configuration.ConfigurationJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : null;

            if (cfg is not null && !string.IsNullOrWhiteSpace(cfg.GatewayUrl) && !string.IsNullOrWhiteSpace(cfg.ServerKey))
            {
                var payload = new
                {
                    to = notification.RecipientAddress,
                    notification = new { title = notification.Subject ?? "Notification", body = notification.Body }
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, cfg.GatewayUrl);
                req.Headers.Add("Authorization", $"key={cfg.ServerKey}");
                req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var resp = await httpClient.SendAsync(req, cancellationToken);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);

                if (resp.IsSuccessStatusCode)
                    return new ProviderSendResult(true, $"fcm_{Guid.NewGuid():N}", "sent", false, FailureCategory.None, null, null);

                return new ProviderSendResult(false, null, "failed", true, FailureCategory.ProviderUnavailable, "PUSH_ERROR", body);
            }

            logger.LogInformation("[Push Simulated] To: {Device} | Title: {Title} | Body: {Body}",
                notification.RecipientAddress, notification.Subject, notification.Body);
            return new ProviderSendResult(true, $"fcm.sim_{Guid.NewGuid():N}", "delivered", false, FailureCategory.None, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Push dispatch failed for delivery {DeliveryId}", notification.DeliveryId);
            return new ProviderSendResult(false, null, "failed", true, FailureCategory.ProviderUnavailable, "PUSH_ERROR", ex.Message);
        }
    }

    public record PushConfig(string? GatewayUrl, string? ServerKey);
}
