using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Interfaces;

public interface IRoutingRepository
{
    Task<IReadOnlyList<RoutingRule>> GetActiveRulesAsync(
        Guid tenantId, Guid applicationId, NotificationChannel channel,
        Guid? notificationTypeId = null, CancellationToken ct = default);
    Task AddAsync(RoutingRule rule, CancellationToken ct = default);
    Task UpdateAsync(RoutingRule rule, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
