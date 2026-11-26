using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Application.Abstractions;

/// <summary>Resolves the best provider configuration for a delivery.</summary>
public interface IProviderResolver
{
    Task<TenantProviderConfiguration?> ResolveAsync(
        Guid tenantId, Guid applicationId,
        NotificationChannel channel,
        Guid? notificationTypeId,
        NotificationPriority priority,
        CancellationToken ct = default,
        Guid? userId = null);

    Task<IReadOnlyList<TenantProviderConfiguration>> ResolveFallbacksAsync(
        Guid tenantId, Guid applicationId,
        NotificationChannel channel,
        string primaryProviderCode,
        CancellationToken ct = default);

    INotificationProvider? GetProvider(string providerCode);
}
