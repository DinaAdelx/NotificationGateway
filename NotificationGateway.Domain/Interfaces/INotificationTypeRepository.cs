using NotificationGateway.Domain.Entities;

namespace NotificationGateway.Domain.Interfaces;

public interface INotificationTypeRepository
{
    Task<NotificationTypeDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<NotificationTypeDefinition?> GetByCodeAsync(Guid tenantId, Guid applicationId, string code, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationTypeDefinition>> GetAllAsync(Guid tenantId, Guid applicationId, CancellationToken ct = default);
    Task AddAsync(NotificationTypeDefinition type, CancellationToken ct = default);
    Task UpdateAsync(NotificationTypeDefinition type, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid tenantId, Guid applicationId, string code, CancellationToken ct = default);
}
