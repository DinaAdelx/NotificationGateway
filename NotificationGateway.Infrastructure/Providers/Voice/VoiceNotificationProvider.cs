using Microsoft.Extensions.Logging;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using System.Text;
using System.Text.Json;

namespace NotificationGateway.Infrastructure.Providers.Voice;

public class VoiceNotificationProvider(HttpClient httpClient, ILogger<VoiceNotificationProvider> logger) : INotificationProvider
{
    public string ProviderCode => "VOICE";
    public NotificationChannel Channel => NotificationChannel.Voice;
    public ProviderCapabilities Capabilities => new(SupportsDeliveryReceipt: true, SupportsScheduling: false, SupportsHtml: false);

    public async Task<ProviderSendResult> SendAsync(
        RenderedNotification notification,
        TenantProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            var cfg = !string.IsNullOrWhiteSpace(configuration.ConfigurationJson) && configuration.ConfigurationJson != "{}"
                ? JsonSerializer.Deserialize<VoiceConfig>(configuration.ConfigurationJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : null;

            if (cfg is not null && !string.IsNullOrWhiteSpace(cfg.GatewayUrl) && !string.IsNullOrWhiteSpace(cfg.ApiKey))
            {
                var payload = new { to = notification.RecipientAddress, text = notification.Body, from = cfg.CallerId ?? "NotifGate" };
                using var req = new HttpRequestMessage(HttpMethod.Post, cfg.GatewayUrl);
                req.Headers.Add("Authorization", $"Bearer {cfg.ApiKey}");
                req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var resp = await httpClient.SendAsync(req, cancellationToken);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);

                if (resp.IsSuccessStatusCode)
                    return new ProviderSendResult(true, $"call_{Guid.NewGuid():N}", "initiated", false, FailureCategory.None, null, null);

                return new ProviderSendResult(false, null, "failed", true, FailureCategory.ProviderUnavailable, "VOICE_ERROR", body);
            }

            logger.LogInformation("[Voice Simulated] To: {Phone} | TTS: {Body}",
                notification.RecipientAddress, notification.Body);
            return new ProviderSendResult(true, $"call.sim_{Guid.NewGuid():N}", "delivered", false, FailureCategory.None, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Voice dispatch failed for delivery {DeliveryId}", notification.DeliveryId);
            return new ProviderSendResult(false, null, "failed", true, FailureCategory.ProviderUnavailable, "VOICE_ERROR", ex.Message);
        }
    }

    public record VoiceConfig(string? GatewayUrl, string? ApiKey, string? CallerId);
}
