using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace NotificationGateway.Infrastructure.Providers.Sms;

public class SmsNotificationProvider(HttpClient httpClient, ILogger<SmsNotificationProvider> logger) : INotificationProvider
{
    public string ProviderCode => "SMS";
    public NotificationChannel Channel => NotificationChannel.Sms;
    public ProviderCapabilities Capabilities => new(SupportsDeliveryReceipt: true, SupportsScheduling: false, SupportsHtml: false);

    public async Task<ProviderSendResult> SendAsync(
        RenderedNotification notification,
        TenantProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            var config = !string.IsNullOrWhiteSpace(configuration.ConfigurationJson)
                ? JsonSerializer.Deserialize<SmsConfig>(configuration.ConfigurationJson)
                : null;

            // If real Twilio / Generic SMS Gateway configured
            if (config != null && !string.IsNullOrWhiteSpace(config.GatewayUrl) && !string.IsNullOrWhiteSpace(config.AuthToken))
            {
                var payload = new
                {
                    from = config.SenderId ?? "NotifGateway",
                    to = notification.RecipientAddress,
                    body = notification.Body
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, config.GatewayUrl);
                request.Headers.Add("Authorization", $"Bearer {config.AuthToken}");
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request, cancellationToken);
                var respBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("SMS dispatched successfully to {Recipient}", notification.RecipientAddress);
                    return new ProviderSendResult(true, $"SM{Guid.NewGuid():N}", "sent", false, FailureCategory.None, null, null);
                }

                logger.LogWarning("SMS gateway returned error {StatusCode}: {Error}", response.StatusCode, respBody);
                return new ProviderSendResult(false, null, "failed", true, FailureCategory.ProviderUnavailable, "SMS_GATEWAY_ERROR", respBody);
            }

            // High-throughput simulated / direct test dispatch
            logger.LogInformation("[SMS Test Delivery] To: {Recipient} | Text: {Text}", notification.RecipientAddress, notification.Body);
            return new ProviderSendResult(true, $"SM{Guid.NewGuid():N}", "delivered", false, FailureCategory.None, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMS dispatch failed for delivery {DeliveryId}", notification.DeliveryId);
            return new ProviderSendResult(false, null, "failed", true, FailureCategory.ProviderUnavailable, "SMS_ERROR", ex.Message);
        }
    }

    public record SmsConfig(string? GatewayUrl, string? AuthToken, string? SenderId);
}
