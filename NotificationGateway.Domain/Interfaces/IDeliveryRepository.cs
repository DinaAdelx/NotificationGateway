using NotificationGateway.Domain.Entities;

namespace NotificationGateway.Domain.Interfaces;

public interface IDeliveryRepository
{
    Task<NotificationDelivery?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDelivery>> GetByNotificationAsync(Guid tenantId, Guid notificationId, CancellationToken ct = default);
    Task AddAsync(NotificationDelivery delivery, CancellationToken ct = default);
    Task UpdateAsync(NotificationDelivery delivery, CancellationToken ct = default);
    Task AddAttemptAsync(DeliveryAttempt attempt, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDelivery>> GetPendingForRetryAsync(int batchSize, CancellationToken ct = default);
}
