using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using System.Text.Json;

namespace NotificationGateway.Infrastructure.Providers.Email;

public class SmtpEmailProvider(
    ILogger<SmtpEmailProvider> logger,
    IConfiguration configuration) : INotificationProvider
{
    public string ProviderCode => "SMTP";
    public NotificationChannel Channel => NotificationChannel.Email;
    public ProviderCapabilities Capabilities => new(false, false, true);

    public async Task<ProviderSendResult> SendAsync(
        RenderedNotification notification,
        TenantProviderConfiguration configuration_,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use only the saved DB provider config (no appsettings.json defaults)
            var smtpConfig = ParseDbConfig(configuration_.ConfigurationJson);

            if (smtpConfig is null)
            {
                logger.LogWarning("[Email] No SMTP config found. Simulating delivery to {Recipient}.", notification.RecipientAddress);
                return new ProviderSendResult(true, $"sim_smtp-{Guid.NewGuid():N}", "simulated", false, FailureCategory.None, null, null);
            }

            // 2. Treat blank / placeholder passwords as simulation mode
            if (IsSimulated(smtpConfig))
            {
                logger.LogInformation("[Email Simulation] Subject: {Subject} | To: {Recipient}", notification.Subject, notification.RecipientAddress);
                return new ProviderSendResult(true, $"sim_smtp-{Guid.NewGuid():N}", "delivered (simulated)", false, FailureCategory.None, null, null);
            }

            // 3. Build & send via MailKit / Gmail SMTP
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtpConfig.FromName ?? "Notification Gateway", smtpConfig.FromEmail));
            message.To.Add(MailboxAddress.Parse(notification.RecipientAddress));
            message.Subject = notification.Subject ?? "(no subject)";

            var builder = new BodyBuilder();
            if (notification.Body.Contains('<'))
                builder.HtmlBody = notification.Body;
            else
                builder.TextBody = notification.Body;
            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();

            // Gmail: connect on port 587 with STARTTLS (UseSsl = false here, MailKit auto-upgrades)
            var secureSocket = smtpConfig.UseSsl
                ? MailKit.Security.SecureSocketOptions.SslOnConnect
                : MailKit.Security.SecureSocketOptions.StartTls;

            await smtp.ConnectAsync(smtpConfig.Host, smtpConfig.Port, secureSocket, cancellationToken);

            if (!string.IsNullOrWhiteSpace(smtpConfig.Username))
                await smtp.AuthenticateAsync(smtpConfig.Username, smtpConfig.Password, cancellationToken);

            var response = await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);

            logger.LogInformation("[Email] Sent OK to {Recipient}. Response: {Response}", notification.RecipientAddress, response);
            return new ProviderSendResult(true, response, "sent", false, FailureCategory.None, null, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Email] SMTP delivery failed for {Recipient}", notification.RecipientAddress);
            return new ProviderSendResult(false, null, "failed", true,
                FailureCategory.ProviderUnavailable, "SMTP_ERROR",
                ex.Message.Length > 300 ? ex.Message[..300] : ex.Message);
        }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private SmtpConfig? BuildConfigFromAppSettings()
    {
        var host = configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host)) return null;

        return new SmtpConfig(
            Host:      host,
            Port:      int.TryParse(configuration["Smtp:Port"], out var p) ? p : 587,
            UseSsl:    bool.TryParse(configuration["Smtp:UseSsl"], out var ssl) && ssl,
            FromEmail: configuration["Smtp:FromEmail"] ?? "noreply@gateway.local",
            FromName:  configuration["Smtp:FromName"] ?? "Notification Gateway",
            Username:  configuration["Smtp:Username"] ?? "",
            Password:  configuration["Smtp:Password"] ?? "");
    }

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static SmtpConfig? ParseDbConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
        try { return JsonSerializer.Deserialize<SmtpConfig>(json, _jsonOpts); }
        catch { return null; }
    }

    private static bool IsSimulated(SmtpConfig cfg) =>
        string.Equals(cfg.Host, "mock", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(cfg.Host, "test", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(cfg.Password) ||
        cfg.Password.Contains("YOUR_") ||
        cfg.Password.Contains("PASTE_") ||
        cfg.Password.Contains("APP_PASSWORD");

    private record SmtpConfig(
        string Host, int Port, bool UseSsl,
        string FromEmail, string? FromName,
        string? Username, string? Password);
}
