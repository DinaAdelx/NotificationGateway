using Hangfire;
using Microsoft.Extensions.Logging;
using NotificationGateway.Domain.Interfaces;
using System.Text.Json;

namespace NotificationGateway.Infrastructure.BackgroundJobs;

/// <summary>Polls the outbox and enqueues delivery jobs via Hangfire.</summary>
public class OutboxProcessor(
    IOutboxRepository outboxRepo,
    IDeliveryRepository deliveryRepo,
    INotificationRepository notifRepo,
    IUnitOfWork uow,
    IBackgroundJobClient jobClient,
    ILogger<OutboxProcessor> logger)
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [DisableConcurrentExecution(300)]
    public async Task ProcessAsync(CancellationToken ct = default)
    {
        var pending = await outboxRepo.GetPendingAsync(50, ct);
        if (pending.Count > 0)
            logger.LogInformation("OutboxProcessor: processing {Count} pending events", pending.Count);

        foreach (var entry in pending)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<OutboxPayload>(entry.PayloadJson, _jsonOpts);
                if (payload is null) { await outboxRepo.MarkProcessedAsync(entry.Id, ct); continue; }

                var notification = await notifRepo.GetByIdAsync(entry.TenantId, entry.NotificationId, ct);
                if (notification is null) { await outboxRepo.MarkProcessedAsync(entry.Id, ct); continue; }

                foreach (var channel in payload.Channels)
                {
                    var recipientAddress = channel switch
                    {
                        Domain.Enums.NotificationChannel.Email => payload.Recipient?.Email ?? "recipient@example.com",
                        Domain.Enums.NotificationChannel.Sms => payload.Recipient?.Phone ?? "+1234567890",
                        Domain.Enums.NotificationChannel.WhatsApp => payload.Recipient?.Phone ?? "+1234567890",
                        Domain.Enums.NotificationChannel.InApp => payload.Recipient?.ExternalUserId ?? "user_default",
                        Domain.Enums.NotificationChannel.SignalR => payload.Recipient?.ExternalUserId ?? "user_default",
                        Domain.Enums.NotificationChannel.Webhook => payload.Recipient?.WebhookUrl ?? "http://localhost:5022/api/webhooks/receive-test",
                        _ => payload.Recipient?.Email ?? "recipient@example.com"
                    };

                    if (string.IsNullOrWhiteSpace(recipientAddress)) continue;

                    var delivery = Domain.Entities.NotificationDelivery.Create(
                        entry.TenantId, entry.NotificationId, channel,
                        recipientAddress, payload.Recipient?.ExternalUserId);

                    await deliveryRepo.AddAsync(delivery, ct);
                    await uow.SaveChangesAsync(ct);

                    jobClient.Enqueue<DeliveryDispatcher>(d => d.DispatchDeliveryAsync(entry.TenantId, delivery.Id, CancellationToken.None));
                }

                await outboxRepo.MarkProcessedAsync(entry.Id, ct);
                await uow.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process outbox entry {Id}", entry.Id);
                entry.IncrementRetry();
            }
        }
    }

    private record OutboxPayload(
        Guid Id,
        IList<Domain.Enums.NotificationChannel> Channels,
        string Language,
        RecipientPayload? Recipient);

    private record RecipientPayload(string? ExternalUserId, string? Email, string? Phone, string? DeviceToken, string? WebhookUrl);
}
