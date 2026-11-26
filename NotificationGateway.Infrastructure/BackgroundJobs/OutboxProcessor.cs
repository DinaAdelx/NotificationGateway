using Hangfire;
using Microsoft.Extensions.Logging;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;
using System.Text.Json;

namespace NotificationGateway.Infrastructure.BackgroundJobs;

/// <summary>Polls the outbox, enforces user preferences & scheduling, and enqueues delivery jobs via Hangfire.</summary>
public class OutboxProcessor(
    IOutboxRepository outboxRepo,
    IDeliveryRepository deliveryRepo,
    INotificationRepository notifRepo,
    IUserPreferenceRepository preferenceRepo,
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

                // Enforce UserPreference opt-out, preferred channels & quiet hours
                var extUserId = payload.Recipient?.ExternalUserId;
                var pref = !string.IsNullOrWhiteSpace(extUserId)
                    ? await preferenceRepo.GetByExternalUserIdAsync(entry.TenantId, notification.ApplicationId, extUserId, ct)
                    : null;

                if (pref is not null && pref.DisabledNotificationTypes.Contains(notification.NotificationTypeCode, StringComparer.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Notification {NotificationId} skipped: user {User} disabled type {Type}",
                        notification.Id, extUserId, notification.NotificationTypeCode);
                    await outboxRepo.MarkProcessedAsync(entry.Id, ct);
                    await uow.SaveChangesAsync(ct);
                    continue;
                }

                // Determine dispatch delay (ScheduledAt or QuietHours)
                TimeSpan? dispatchDelay = null;
                if (notification.ScheduledAt.HasValue && notification.ScheduledAt.Value > DateTime.UtcNow)
                {
                    dispatchDelay = notification.ScheduledAt.Value - DateTime.UtcNow;
                }
                else if (pref is not null && pref.QuietHoursEnabled && pref.QuietHoursStart.HasValue && pref.QuietHoursEnd.HasValue
                         && notification.Priority != NotificationPriority.Critical)
                {
                    var nowUtc = TimeOnly.FromDateTime(DateTime.UtcNow);
                    if (IsInQuietHours(nowUtc, pref.QuietHoursStart.Value, pref.QuietHoursEnd.Value))
                    {
                        var endToday = DateTime.UtcNow.Date.Add(pref.QuietHoursEnd.Value.ToTimeSpan());
                        if (endToday <= DateTime.UtcNow) endToday = endToday.AddDays(1);
                        dispatchDelay = endToday - DateTime.UtcNow;
                    }
                }

                foreach (var channel in payload.Channels)
                {
                    if (pref is not null && pref.PreferredChannels.Count > 0 && !pref.PreferredChannels.Contains(channel))
                    {
                        logger.LogInformation("Channel {Channel} skipped by user {User} preference", channel, extUserId);
                        continue;
                    }

                    var recipientAddress = channel switch
                    {
                        NotificationChannel.Email => payload.Recipient?.Email ?? string.Empty,
                        NotificationChannel.Sms => payload.Recipient?.Phone ?? string.Empty,
                        NotificationChannel.WhatsApp => payload.Recipient?.Phone ?? string.Empty,
                        NotificationChannel.Voice => payload.Recipient?.Phone ?? string.Empty,
                        NotificationChannel.Push => payload.Recipient?.DeviceToken ?? payload.Recipient?.ExternalUserId ?? string.Empty,
                        NotificationChannel.InApp => payload.Recipient?.ExternalUserId ?? string.Empty,
                        NotificationChannel.SignalR => payload.Recipient?.ExternalUserId ?? string.Empty,
                        NotificationChannel.Webhook => payload.Recipient?.WebhookUrl ?? string.Empty,
                        _ => payload.Recipient?.Email ?? string.Empty
                    };

                    if (string.IsNullOrWhiteSpace(recipientAddress)) continue;

                    var delivery = Domain.Entities.NotificationDelivery.Create(
                        entry.TenantId, entry.NotificationId, channel,
                        recipientAddress, payload.Recipient?.ExternalUserId);

                    await deliveryRepo.AddAsync(delivery, ct);
                    await uow.SaveChangesAsync(ct);

                    if (dispatchDelay.HasValue && dispatchDelay.Value > TimeSpan.Zero)
                    {
                        delivery.ScheduleRetry(DateTime.UtcNow.Add(dispatchDelay.Value));
                        await deliveryRepo.UpdateAsync(delivery, ct);
                        await uow.SaveChangesAsync(ct);
                        jobClient.Schedule<DeliveryDispatcher>(
                            d => d.DispatchDeliveryAsync(entry.TenantId, delivery.Id, CancellationToken.None),
                            dispatchDelay.Value);
                    }
                    else
                    {
                        jobClient.Enqueue<DeliveryDispatcher>(
                            d => d.DispatchDeliveryAsync(entry.TenantId, delivery.Id, CancellationToken.None));
                    }
                }

                await outboxRepo.MarkProcessedAsync(entry.Id, ct);
                await uow.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process outbox entry {Id}", entry.Id);
                entry.IncrementRetry();
                await uow.SaveChangesAsync(ct);
            }
        }
    }

    private static bool IsInQuietHours(TimeOnly now, TimeOnly start, TimeOnly end)
        => start <= end
            ? now >= start && now <= end
            : now >= start || now <= end;

    private record OutboxPayload(
        Guid Id,
        IList<NotificationChannel> Channels,
        string Language,
        RecipientPayload? Recipient);

    private record RecipientPayload(string? ExternalUserId, string? Email, string? Phone, string? DeviceToken, string? WebhookUrl);
}
