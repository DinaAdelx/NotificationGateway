using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace NotificationGateway.Infrastructure.Providers.WhatsApp;

/// <summary>
/// WhatsApp provider using Green API (https://green-api.com) — free developer tier.
/// Falls back to simulation mode if no credentials are configured.
/// Green API endpoint: POST https://api.green-api.com/waInstance{IdInstance}/sendMessage/{ApiToken}
/// Phone format sent as chatId: "201234567890@c.us"  (country code + local number, no +)
/// </summary>
public class WhatsAppNotificationProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<WhatsAppNotificationProvider> logger) : INotificationProvider
{
    // ── Green API base URL (can be overridden per-tenant via ConfigurationJson)
    private const string GreenApiBase = "https://api.green-api.com";

    public string ProviderCode => "WHATSAPP";
    public NotificationChannel Channel => NotificationChannel.WhatsApp;
    public ProviderCapabilities Capabilities => new(SupportsDeliveryReceipt: true, SupportsScheduling: false, SupportsHtml: false);

    public async Task<ProviderSendResult> SendAsync(
        RenderedNotification notification,
        TenantProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Resolve config: appsettings.json first, then DB ConfigurationJson
            var cfg = ResolveConfig(configuration.ConfigurationJson);

            // 2. Simulation mode — no real credentials
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.IdInstance) || string.IsNullOrWhiteSpace(cfg.ApiToken))
            {
                logger.LogInformation("[WhatsApp Simulated] To: {Recipient} | Body: {Body}",
                    notification.RecipientAddress, notification.Body);
                return new ProviderSendResult(true, $"wamid.sim_{Guid.NewGuid():N}", "simulated",
                    false, FailureCategory.None, null, null);
            }

            // 3. Build Green API endpoint
            var url = $"{GreenApiBase}/waInstance{cfg.IdInstance}/sendMessage/{cfg.ApiToken}";

            // 4. Format phone as Green API chatId: strip +, append @c.us
            var chatId = FormatChatId(notification.RecipientAddress);

            var payload = new
            {
                chatId,
                message = notification.Body
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            logger.LogInformation("Sending WhatsApp via Green API to {ChatId}", chatId);

            var response = await httpClient.SendAsync(request, cancellationToken);
            var respBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // Green API returns: { "idMessage": "XXXXXXXX" }
                string? messageId = null;
                try
                {
                    var respDoc = JsonDocument.Parse(respBody);
                    messageId = respDoc.RootElement.TryGetProperty("idMessage", out var idProp)
                        ? idProp.GetString() : null;
                }
                catch { /* ignore parse errors */ }

                logger.LogInformation("WhatsApp message sent via Green API. MessageId={MsgId}, Recipient={Recipient}",
                    messageId, notification.RecipientAddress);

                return new ProviderSendResult(true, messageId ?? $"green_{Guid.NewGuid():N}", "sent",
                    false, FailureCategory.None, null, null);
            }

            logger.LogWarning("Green API error {StatusCode}: {Error}", response.StatusCode, respBody);
            return new ProviderSendResult(false, null, "failed", true,
                FailureCategory.ProviderUnavailable, "GREEN_API_ERROR", $"{response.StatusCode}: {respBody}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WhatsApp dispatch failed for delivery {DeliveryId}", notification.DeliveryId);
            return new ProviderSendResult(false, null, "failed", true,
                FailureCategory.ProviderUnavailable, "WHATSAPP_ERROR", ex.Message);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private GreenApiConfig? ResolveConfig(string? dbConfigJson)
    {
        // Priority 1: Per-tenant DB ConfigurationJson (editable via UI/API)
        if (!string.IsNullOrWhiteSpace(dbConfigJson) && dbConfigJson != "{}")
        {
            try
            {
                var dbCfg = JsonSerializer.Deserialize<GreenApiConfig>(dbConfigJson, _jsonOpts);
                if (dbCfg is not null && !string.IsNullOrWhiteSpace(dbCfg.IdInstance) && !string.IsNullOrWhiteSpace(dbCfg.ApiToken))
                    return dbCfg;
            }
            catch { /* fall through */ }
        }

        // Priority 2: appsettings.json "WhatsApp" section
        var appSettingsIdInstance = configuration["WhatsApp:IdInstance"];
        var appSettingsApiToken   = configuration["WhatsApp:ApiToken"];

        if (!string.IsNullOrWhiteSpace(appSettingsIdInstance) && !string.IsNullOrWhiteSpace(appSettingsApiToken))
        {
            return new GreenApiConfig(appSettingsIdInstance, appSettingsApiToken);
        }

        return null;
    }

    /// <summary>
    /// Green API requires chatId format: "201234567890@c.us"
    /// Strips leading +, spaces, dashes. If number has no country code prefix we leave it as-is.
    /// </summary>
    private static string FormatChatId(string phone)
    {
        var digits = phone.TrimStart('+').Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        return $"{digits}@c.us";
    }

    public record GreenApiConfig(string? IdInstance, string? ApiToken);
}
