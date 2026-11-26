using Hangfire;
using Microsoft.Extensions.Logging;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;
using NotificationGateway.Infrastructure.Services;

namespace NotificationGateway.Infrastructure.BackgroundJobs;

public class DeliveryDispatcher(
    IDeliveryRepository deliveryRepo,
    INotificationRepository notificationRepo,
    ITemplateRenderer templateRenderer,
    INotificationTypeRepository typeRepo,
    IProviderResolver providerResolver,
    ICredentialEncryptor credentialEncryptor,
    IBackgroundJobClient jobClient,
    IUnitOfWork uow,
    ILogger<DeliveryDispatcher> logger)
{
    [AutomaticRetry(Attempts = 0)] // Retry & failover managed internally
    public async Task DispatchDeliveryAsync(Guid tenantId, Guid deliveryId, CancellationToken ct = default)
    {
        var delivery = await deliveryRepo.GetByIdAsync(tenantId, deliveryId, ct);
        if (delivery is null)
        {
            logger.LogWarning("Delivery {DeliveryId} not found for tenant {TenantId}", deliveryId, tenantId);
            return;
        }

        var notification = await notificationRepo.GetByIdAsync(tenantId, delivery.NotificationId, ct);
        if (notification is null || notification.Status == NotificationStatus.Cancelled) return;

        var notifType = await typeRepo.GetByIdAsync(tenantId, notification.NotificationTypeDefinitionId, ct);
        if (notifType is null) return;

        // 1. Resolve primary provider (per-user saved settings first, then tenant routing/default)
        var providerConfig = await providerResolver.ResolveAsync(
            tenantId, notification.ApplicationId, delivery.Channel,
            notification.NotificationTypeDefinitionId, notification.Priority, ct, notification.UserId);

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

        // 2. Render template using the notification's requested Language (no hardcoded "en")
        var language = string.IsNullOrWhiteSpace(notification.Language) ? "en" : notification.Language;
        var rendered = await templateRenderer.RenderAsync(
            tenantId, notification.ApplicationId,
            notification.NotificationTypeDefinitionId,
            delivery.Channel, language,
            notification.PayloadJson, ct);

        if (rendered is null)
        {
            logger.LogWarning("No active template for delivery {DeliveryId}", deliveryId);
            delivery.MarkFailed();
            await deliveryRepo.UpdateAsync(delivery, ct);
            await uow.SaveChangesAsync(ct);
            return;
        }

        var renderedNotif = new RenderedNotification(
            tenantId, notification.Id, delivery.Id,
            delivery.Channel, delivery.RecipientAddress,
            rendered.Subject, rendered.Body, notification.MetadataJson);

        // 3. Execute primary attempt (with decrypted credentials)
        delivery.MarkProcessing();
        var result = await ExecuteAttemptAsync(tenantId, delivery, provider, DecryptConfig(providerConfig), renderedNotif, ct);

        // 4. Secondary-provider failover if primary failed
        if (!result.IsSuccess)
        {
            var fallbacks = await providerResolver.ResolveFallbacksAsync(
                tenantId, notification.ApplicationId, delivery.Channel, providerConfig.ProviderCode, ct);

            foreach (var fbConfig in fallbacks)
            {
                var fbProvider = providerResolver.GetProvider(fbConfig.ProviderCode);
                if (fbProvider is null) continue;

                logger.LogWarning("Primary provider {Primary} failed for delivery {DeliveryId}; failing over to {Fallback}",
                    providerConfig.ProviderCode, deliveryId, fbConfig.ProviderCode);

                result = await ExecuteAttemptAsync(tenantId, delivery, fbProvider, DecryptConfig(fbConfig), renderedNotif, ct);
                if (result.IsSuccess) break;
            }
        }

        // 5. Finalize or schedule real Hangfire delayed retry
        if (result.IsSuccess)
        {
            delivery.MarkSent();
        }
        else
        {
            const int MaxAttempts = 5;
            if (result.IsRetryable && delivery.AttemptCount < MaxAttempts)
            {
                var delay = GetRetryDelay(delivery.AttemptCount);
                delivery.ScheduleRetry(DateTime.UtcNow.Add(delay));
                await deliveryRepo.UpdateAsync(delivery, ct);
                await uow.SaveChangesAsync(ct);

                jobClient.Schedule<DeliveryDispatcher>(
                    d => d.DispatchDeliveryAsync(tenantId, deliveryId, CancellationToken.None),
                    delay);

                logger.LogInformation("Delivery {DeliveryId} scheduled for Hangfire retry #{Attempt} in {Delay}",
                    deliveryId, delivery.AttemptCount, delay);
                return;
            }

            delivery.MarkDeadLettered();
            logger.LogWarning("Delivery {DeliveryId} dead-lettered after {Attempts} attempt(s)", deliveryId, delivery.AttemptCount);
        }

        await deliveryRepo.UpdateAsync(delivery, ct);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<ProviderSendResult> ExecuteAttemptAsync(
        Guid tenantId,
        NotificationDelivery delivery,
        INotificationProvider provider,
        TenantProviderConfiguration effectiveConfig,
        RenderedNotification renderedNotif,
        CancellationToken ct)
    {
        delivery.RecordAttempt();
        var attempt = DeliveryAttempt.Create(tenantId, delivery.Id, delivery.AttemptCount, provider.ProviderCode);
        await deliveryRepo.AddAttemptAsync(attempt, ct);

        var result = await provider.SendAsync(renderedNotif, effectiveConfig, ct);
        if (result.IsSuccess)
            attempt.MarkSuccess(result.ProviderMessageId);
        else
            attempt.MarkFailure(result.FailureCategory, result.ErrorCode, result.SafeErrorMessage, result.IsRetryable);

        return result;
    }

    private TenantProviderConfiguration DecryptConfig(TenantProviderConfiguration source)
    {
        if (string.IsNullOrWhiteSpace(source.EncryptedCredentials))
            return source;

        try
        {
            var decryptedJson = credentialEncryptor.Decrypt(source.EncryptedCredentials);
            return TenantProviderConfiguration.Create(
                source.TenantId, source.ApplicationId, source.ProviderCode,
                source.Channel, decryptedJson, source.EncryptedCredentials,
                source.IsPrimary, source.Priority, source.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt credentials for provider {Code}", source.ProviderCode);
            return source;
        }
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
