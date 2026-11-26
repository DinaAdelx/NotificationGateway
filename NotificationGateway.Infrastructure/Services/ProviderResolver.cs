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
        NotificationPriority priority, CancellationToken ct,
        Guid? userId = null)
    {
        var configs = await configRepo.GetEnabledByChannelAsync(tenantId, applicationId, channel, ct);

        // 1. Per-user saved provider settings take highest priority
        if (userId.HasValue)
        {
            var userConfig = configs.FirstOrDefault(c => c.UserId == userId.Value);
            if (userConfig is not null) return userConfig;
        }

        // 2. Tenant-wide routing rules & configurations (UserId == null)
        var tenantConfigs = configs.Where(c => c.UserId == null).ToList();
        var rules = await routingRepo.GetActiveRulesAsync(tenantId, applicationId, channel, notificationTypeId, ct);
        var rule = rules.FirstOrDefault(r => r.Priority == null || r.Priority == priority) ?? rules.FirstOrDefault();

        string? primaryCode = rule?.PrimaryProviderCode;

        if (primaryCode is not null)
        {
            var match = tenantConfigs.FirstOrDefault(c => c.ProviderCode.Equals(primaryCode, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        var selected = tenantConfigs.FirstOrDefault(c => c.IsPrimary) ?? tenantConfigs.FirstOrDefault() ?? configs.FirstOrDefault();
        if (selected != null) return selected;

        // Auto-fallback default configuration for registered channels
        var defaultCode = channel switch
        {
            NotificationChannel.SignalR => "SIGNALR",
            NotificationChannel.Webhook => "WEBHOOK",
            NotificationChannel.Sms => "SMS",
            NotificationChannel.WhatsApp => "WHATSAPP",
            NotificationChannel.Email => "SMTP",
            NotificationChannel.InApp => "INAPP",
            NotificationChannel.Push => "PUSH",
            NotificationChannel.Voice => "VOICE",
            _ => "MOCK"
        };

        return TenantProviderConfiguration.Create(tenantId, applicationId, defaultCode, channel, "{}", null, true, 0);
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
