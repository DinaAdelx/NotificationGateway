using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NotificationGateway.Infrastructure.Providers.Webhook;

public class WebhookNotificationProvider(HttpClient httpClient, ILogger<WebhookNotificationProvider> logger) : INotificationProvider
{
    public string ProviderCode => "WEBHOOK";
    public NotificationChannel Channel => NotificationChannel.Webhook;
    public ProviderCapabilities Capabilities => new(SupportsDeliveryReceipt: true, SupportsScheduling: false, SupportsHtml: false);

    public async Task<ProviderSendResult> SendAsync(
        RenderedNotification notification,
        TenantProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var targetUrl = notification.RecipientAddress;
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return new ProviderSendResult(false, null, "failed", false, FailureCategory.InvalidRecipient, "MISSING_URL", "Webhook Target URL is required.");
        }

        try
        {
            var config = !string.IsNullOrWhiteSpace(configuration.ConfigurationJson)
                ? JsonSerializer.Deserialize<WebhookConfig>(configuration.ConfigurationJson)
                : null;

            var secretKey = config?.SecretKey ?? "whsec_default_gateway_secret_2026!";
            var eventId = notification.NotificationId.ToString("N");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            var payloadObject = new
            {
                eventId = eventId,
                eventType = "notification.delivered",
                notificationId = notification.NotificationId,
                deliveryId = notification.DeliveryId,
                tenantId = notification.TenantId,
                channel = "Webhook",
                subject = notification.Subject,
                body = notification.Body,
                metadata = notification.MetadataJson,
                timestamp = DateTime.UtcNow
            };

            var payloadJson = JsonSerializer.Serialize(payloadObject);
            var signature = ComputeHmacSha256(payloadJson, secretKey);

            using var request = new HttpRequestMessage(HttpMethod.Post, targetUrl);
            request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            // Industry standard webhook signature headers
            request.Headers.Add("X-Hub-Signature-256", signature);
            request.Headers.Add("X-Webhook-Timestamp", timestamp);
            request.Headers.Add("X-Event-ID", eventId);
            request.Headers.Add("X-Delivery-Attempt", "1");

            var sw = Stopwatch.StartNew();
            var response = await httpClient.SendAsync(request, cancellationToken);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Webhook delivered to {Url} in {Ms}ms. Status: {StatusCode}", targetUrl, sw.ElapsedMilliseconds, (int)response.StatusCode);
                return new ProviderSendResult(true, $"wh_{eventId}", $"Delivered ({(int)response.StatusCode})", false, FailureCategory.None, null, null);
            }

            var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Webhook delivery failed. Target: {Url}, Status: {StatusCode}, Error: {Error}", targetUrl, (int)response.StatusCode, errBody);

            bool isRetryable = (int)response.StatusCode >= 500 || (int)response.StatusCode == 429;
            return new ProviderSendResult(false, null, $"HTTP {(int)response.StatusCode}", isRetryable, FailureCategory.ProviderUnavailable, "HTTP_ERROR", $"Status: {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook HTTP error while dispatching to {Url}", targetUrl);
            return new ProviderSendResult(false, null, "failed", true, FailureCategory.ProviderUnavailable, "NETWORK_ERROR", ex.Message.Length > 200 ? ex.Message[..200] : ex.Message);
        }
    }

    public static string ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public record WebhookConfig(string? SecretKey);
}
