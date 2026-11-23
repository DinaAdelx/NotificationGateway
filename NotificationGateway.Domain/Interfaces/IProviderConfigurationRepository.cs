using NotificationGateway.Domain.Entities;

namespace NotificationGateway.Domain.Interfaces;

public interface IProviderConfigurationRepository
{
    Task<TenantProviderConfiguration?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TenantProviderConfiguration>> GetEnabledByChannelAsync(Guid tenantId, Guid applicationId, Domain.Enums.NotificationChannel channel, CancellationToken ct = default);
    Task AddAsync(TenantProviderConfiguration config, CancellationToken ct = default);
    Task UpdateAsync(TenantProviderConfiguration config, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
