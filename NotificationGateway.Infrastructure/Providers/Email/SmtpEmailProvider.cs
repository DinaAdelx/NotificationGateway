using MailKit.Net.Smtp;
using MimeKit;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using System.Text.Json;

namespace NotificationGateway.Infrastructure.Providers.Email;

public class SmtpEmailProvider : INotificationProvider
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
            var config = JsonSerializer.Deserialize<SmtpConfig>(configuration.ConfigurationJson)
                ?? throw new InvalidOperationException("Invalid SMTP configuration.");

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
            return new ProviderSendResult(false, null, "failed", true,
                FailureCategory.ProviderUnavailable, "SMTP_ERROR", ex.Message.Length > 200 ? ex.Message[..200] : ex.Message);
        }
    }

    private record SmtpConfig(string Host, int Port, bool UseSsl, string FromEmail, string? FromName, string? Username, string? Password);
}
