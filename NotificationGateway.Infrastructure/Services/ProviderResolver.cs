using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.Infrastructure.Services;

/// <summary>DI-registry-based provider resolver. Providers register themselves at startup.</summary>
public class ProviderResolver(
    IEnumerable<INotificationProvider> providers,
    IProviderConfigurationRepository configRepo,
    IRoutingRepository routingRepo) : IProviderResolver
{
    private readonly Dictionary<string, INotificationProvider> _providers =
        providers.ToDictionary(p => p.ProviderCode, StringComparer.OrdinalIgnoreCase);

    public async Task<TenantProviderConfiguration?> ResolveAsync(
        Guid tenantId, Guid applicationId,
        NotificationChannel channel, Guid? notificationTypeId,
        NotificationPriority priority, CancellationToken ct)
    {
        // Check routing rules first
        var rules = await routingRepo.GetActiveRulesAsync(tenantId, applicationId, channel, notificationTypeId, ct);
        var rule = rules.FirstOrDefault(r => r.Priority == null || r.Priority == priority) ?? rules.FirstOrDefault();

        string? primaryCode = rule?.PrimaryProviderCode;

        var configs = await configRepo.GetEnabledByChannelAsync(tenantId, applicationId, channel, ct);

        if (primaryCode is not null)
            return configs.FirstOrDefault(c => c.ProviderCode.Equals(primaryCode, StringComparison.OrdinalIgnoreCase));

        return configs.FirstOrDefault(c => c.IsPrimary) ?? configs.FirstOrDefault();
    }

    public async Task<IReadOnlyList<TenantProviderConfiguration>> ResolveFallbacksAsync(
        Guid tenantId, Guid applicationId,
        NotificationChannel channel, string primaryProviderCode, CancellationToken ct)
    {
        var rules = await routingRepo.GetActiveRulesAsync(tenantId, applicationId, channel, null, ct);
        var rule = rules.FirstOrDefault(r => r.PrimaryProviderCode.Equals(primaryProviderCode, StringComparison.OrdinalIgnoreCase));
        if (rule is null || rule.FallbackProviderCodes.Count == 0) return [];

        var configs = await configRepo.GetEnabledByChannelAsync(tenantId, applicationId, channel, ct);
        return configs
            .Where(c => rule.FallbackProviderCodes.Contains(c.ProviderCode, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public INotificationProvider? GetProvider(string providerCode)
        => _providers.TryGetValue(providerCode, out var p) ? p : null;
}
