using NotificationGateway.Domain.Entities;

namespace NotificationGateway.Domain.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid tenantId, Guid notificationId, CancellationToken ct = default);
    Task<Notification?> GetByIdempotencyKeyAsync(Guid tenantId, Guid applicationId, string key, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByTenantAsync(Guid tenantId, Guid applicationId, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
}
