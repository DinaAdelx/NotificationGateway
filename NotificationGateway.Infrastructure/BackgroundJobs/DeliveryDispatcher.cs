using Hangfire;
using Microsoft.Extensions.Logging;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;
using NotificationGateway.Infrastructure.Services;
using System.Text.Json;

namespace NotificationGateway.Infrastructure.BackgroundJobs;

public class DeliveryDispatcher(
    IDeliveryRepository deliveryRepo,
    INotificationRepository notificationRepo,
    ITemplateRenderer templateRenderer,
    INotificationTypeRepository typeRepo,
    ProviderResolver providerResolver,
    IUnitOfWork uow,
    ILogger<DeliveryDispatcher> logger)
{
    [AutomaticRetry(Attempts = 0)] // Retry logic managed internally
    public async Task DispatchDeliveryAsync(Guid tenantId, Guid deliveryId, CancellationToken ct = default)
    {
        var delivery = await deliveryRepo.GetByIdAsync(tenantId, deliveryId, ct);
        if (delivery is null)
        {
            logger.LogWarning("Delivery {DeliveryId} not found for tenant {TenantId}", deliveryId, tenantId);
            return;
        }

        var notification = await notificationRepo.GetByIdAsync(tenantId, delivery.NotificationId, ct);
        if (notification is null) return;

        var notifType = await typeRepo.GetByIdAsync(tenantId, notification.NotificationTypeDefinitionId, ct);
        if (notifType is null) return;

        // Resolve provider
        var providerConfig = await providerResolver.ResolveAsync(
            tenantId, notification.ApplicationId, delivery.Channel,
            notification.NotificationTypeDefinitionId, notification.Priority, ct);

        if (providerConfig is null)
        {
            logger.LogWarning("No provider found for channel {Channel} in tenant {TenantId}", delivery.Channel, tenantId);
            delivery.MarkFailed();
            await deliveryRepo.UpdateAsync(delivery, ct);
            await uow.SaveChangesAsync(ct);
            return;
        }

        var provider = providerResolver.GetProvider(providerConfig.ProviderCode);
        if (provider is null)
        {
            logger.LogError("Provider implementation not registered for code {Code}", providerConfig.ProviderCode);
            delivery.MarkFailed();
            await deliveryRepo.UpdateAsync(delivery, ct);
            await uow.SaveChangesAsync(ct);
            return;
        }

        // Render template
        var rendered = await templateRenderer.RenderAsync(
            tenantId, notification.ApplicationId,
            notification.NotificationTypeDefinitionId,
            delivery.Channel, "en",
            notification.PayloadJson, ct);

        if (rendered is null)
        {
            logger.LogWarning("No active template for delivery {DeliveryId}", deliveryId);
            delivery.MarkFailed();
            await deliveryRepo.UpdateAsync(delivery, ct);
            await uow.SaveChangesAsync(ct);
            return;
        }

        // Record attempt
        delivery.MarkProcessing();
        delivery.RecordAttempt();
        var attempt = Domain.Entities.DeliveryAttempt.Create(
            tenantId, deliveryId, delivery.AttemptCount, provider.ProviderCode);
        await deliveryRepo.AddAttemptAsync(attempt, ct);

        var renderedNotif = new RenderedNotification(
            tenantId, notification.Id, delivery.Id,
            delivery.Channel, delivery.RecipientAddress,
            rendered.Subject, rendered.Body, notification.MetadataJson);

        var result = await provider.SendAsync(renderedNotif, providerConfig, ct);

        if (result.IsSuccess)
        {
            attempt.MarkSuccess(result.ProviderMessageId);
            delivery.MarkSent();
        }
        else
        {
            attempt.MarkFailure(result.FailureCategory, result.ErrorCode, result.SafeErrorMessage, result.IsRetryable);

            const int MaxAttempts = 5;
            if (result.IsRetryable && delivery.AttemptCount < MaxAttempts)
            {
                var delay = GetRetryDelay(delivery.AttemptCount);
                delivery.ScheduleRetry(DateTime.UtcNow.Add(delay));
                logger.LogInformation("Delivery {DeliveryId} scheduled for retry #{Attempt} at {RetryAt}", deliveryId, delivery.AttemptCount, DateTime.UtcNow.Add(delay));
            }
            else
            {
                delivery.MarkDeadLettered();
                logger.LogWarning("Delivery {DeliveryId} dead-lettered after {Attempts} attempt(s)", deliveryId, delivery.AttemptCount);
            }
        }

        await deliveryRepo.UpdateAsync(delivery, ct);
        await uow.SaveChangesAsync(ct);
    }

    private static TimeSpan GetRetryDelay(int attemptCount) => attemptCount switch
    {
        1 => TimeSpan.FromSeconds(30),
        2 => TimeSpan.FromMinutes(2),
        3 => TimeSpan.FromMinutes(10),
        4 => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromHours(1)
    };
}
