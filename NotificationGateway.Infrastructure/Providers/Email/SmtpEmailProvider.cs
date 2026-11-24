using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using System.Text.Json;

namespace NotificationGateway.Infrastructure.Providers.Email;

public class SmtpEmailProvider(ILogger<SmtpEmailProvider> logger) : INotificationProvider
{
    public string ProviderCode => "SMTP";
    public NotificationChannel Channel => NotificationChannel.Email;
    public ProviderCapabilities Capabilities => new(false, false, true);

    public async Task<ProviderSendResult> SendAsync(
        RenderedNotification notification,
        TenantProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            var config = !string.IsNullOrWhiteSpace(configuration.ConfigurationJson)
                ? JsonSerializer.Deserialize<SmtpConfig>(configuration.ConfigurationJson)
                : null;

            if (config is null)
            {
                logger.LogInformation("[Email Simulation] ProviderConfig was empty. Sending simulated email to {Recipient}", notification.RecipientAddress);
                return new ProviderSendResult(true, $"sim_smtp-{Guid.NewGuid():N}", "sent", false, FailureCategory.None, null, null);
            }

            // Check if test / mock mode
            if (string.Equals(config.Host, "mock", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(config.Host, "test", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(config.Password) ||
                config.Password.Contains("YOUR_") ||
                config.Password.Contains("APP_PASSWORD"))
            {
                logger.LogInformation("[Email Test Simulation] Smtp host is {Host} (test/mock mode). Subject: {Subject} | To: {Recipient}",
                    config.Host, notification.Subject, notification.RecipientAddress);
                return new ProviderSendResult(true, $"sim_smtp-{Guid.NewGuid():N}", "delivered (simulated)", false, FailureCategory.None, null, null);
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(config.FromName ?? "Notification Gateway", config.FromEmail));
            message.To.Add(MailboxAddress.Parse(notification.RecipientAddress));
            message.Subject = notification.Subject ?? "(no subject)";

            var builder = new BodyBuilder();
            if (notification.Body.Contains('<'))
                builder.HtmlBody = notification.Body;
            else
                builder.TextBody = notification.Body;
            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(config.Host, config.Port, config.UseSsl, cancellationToken);
            if (!string.IsNullOrWhiteSpace(config.Username))
                await smtp.AuthenticateAsync(config.Username, config.Password, cancellationToken);
            var response = await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);

            return new ProviderSendResult(true, response, "sent", false, FailureCategory.None, null, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTP delivery failed for {Recipient}", notification.RecipientAddress);
            return new ProviderSendResult(false, null, "failed", true,
                FailureCategory.ProviderUnavailable, "SMTP_ERROR", ex.Message.Length > 200 ? ex.Message[..200] : ex.Message);
        }
    }

    private record SmtpConfig(string Host, int Port, bool UseSsl, string FromEmail, string? FromName, string? Username, string? Password);
}
