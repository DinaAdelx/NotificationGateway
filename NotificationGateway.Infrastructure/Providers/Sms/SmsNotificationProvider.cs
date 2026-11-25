using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace NotificationGateway.Infrastructure.Providers.Sms;

/// <summary>
/// SMS provider using Vonage REST API (https://rest.nexmo.com/sms/json).
/// Falls back to simulation mode if no credentials are configured.
/// </summary>
public class SmsNotificationProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<SmsNotificationProvider> logger) : INotificationProvider
{
    private const string VonageSmsUrl = "https://rest.nexmo.com/sms/json";

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
            var cfg = ResolveConfig(configuration.ConfigurationJson);

            // Simulation mode — no real credentials
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.ApiKey) || string.IsNullOrWhiteSpace(cfg.ApiSecret))
            {
                logger.LogInformation("[SMS Simulated] To: {Recipient} | Text: {Text}",
                    notification.RecipientAddress, notification.Body);
                return new ProviderSendResult(true, $"SM{Guid.NewGuid():N}", "simulated", false, FailureCategory.None, null, null);
            }

            // Vonage expects international format without '+' (e.g. 201012345678)
            var toNumber = FormatPhoneNumber(notification.RecipientAddress);
            var from = string.IsNullOrWhiteSpace(cfg.SenderId) ? "NotifGate" : cfg.SenderId;

            var payload = new
            {
                api_key = cfg.ApiKey,
                api_secret = cfg.ApiSecret,
                to = toNumber,
                from,
                text = notification.Body
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, VonageSmsUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            logger.LogInformation("Sending SMS via Vonage to {Recipient}", toNumber);

            var response = await httpClient.SendAsync(request, cancellationToken);
            var respBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Vonage HTTP error {StatusCode}: {Error}", response.StatusCode, respBody);
                return new ProviderSendResult(false, null, "failed", true,
                    FailureCategory.ProviderUnavailable, "VONAGE_HTTP_ERROR", $"{response.StatusCode}: {respBody}");
            }

            // Vonage returns HTTP 200 even on delivery rejection — inspect messages[0].status ("0" == OK)
            using var doc = JsonDocument.Parse(respBody);
            if (doc.RootElement.TryGetProperty("messages", out var messagesEl) && messagesEl.GetArrayLength() > 0)
            {
                var first = messagesEl[0];
                var status = first.TryGetProperty("status", out var s) ? s.GetString() : null;
                var msgId = first.TryGetProperty("message-id", out var id) ? id.GetString() : null;

                if (status == "0")
                {
                    logger.LogInformation("SMS sent via Vonage. MessageId={MsgId}, To={Recipient}", msgId, toNumber);
                    return new ProviderSendResult(true, msgId ?? $"SM{Guid.NewGuid():N}", "sent",
                        false, FailureCategory.None, null, null);
                }

                var errorText = first.TryGetProperty("error-text", out var err) ? err.GetString() : respBody;
                logger.LogWarning("Vonage rejected SMS (status {Status}): {Error}", status, errorText);
                return new ProviderSendResult(false, msgId, "failed", true,
                    FailureCategory.ProviderUnavailable, $"VONAGE_STATUS_{status}", errorText);
            }

            return new ProviderSendResult(true, $"SM{Guid.NewGuid():N}", "sent", false, FailureCategory.None, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMS dispatch failed for delivery {DeliveryId}", notification.DeliveryId);
            return new ProviderSendResult(false, null, "failed", true, FailureCategory.ProviderUnavailable, "SMS_ERROR", ex.Message);
        }
    }

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private VonageSmsConfig? ResolveConfig(string? dbConfigJson)
    {
        // Priority 1: Per-tenant DB ConfigurationJson (editable via UI/API)
        if (!string.IsNullOrWhiteSpace(dbConfigJson) && dbConfigJson != "{}")
        {
            try
            {
                var dbCfg = JsonSerializer.Deserialize<VonageSmsConfig>(dbConfigJson, _jsonOpts);
                if (dbCfg is not null && !string.IsNullOrWhiteSpace(dbCfg.ApiKey) && !string.IsNullOrWhiteSpace(dbCfg.ApiSecret))
                    return dbCfg;
            }
            catch { /* fall through */ }
        }

        // Priority 2: appsettings.json "Sms" section
        var apiKey = configuration["Sms:ApiKey"];
        var apiSecret = configuration["Sms:ApiSecret"];
        var senderId = configuration["Sms:SenderId"];

        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
        {
            return new VonageSmsConfig(apiKey, apiSecret, senderId);
        }

        return null;
    }

    private static string FormatPhoneNumber(string phone) =>
        phone.TrimStart('+').Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

    public record VonageSmsConfig(string? ApiKey, string? ApiSecret, string? SenderId);
}
