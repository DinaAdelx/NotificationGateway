using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Domain.Interfaces;

public interface ITemplateRepository
{
    Task<NotificationTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<NotificationTemplate?> GetActiveAsync(Guid tenantId, Guid applicationId, Guid notificationTypeId, NotificationChannel channel, string language, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationTemplate>> GetAllAsync(Guid tenantId, Guid applicationId, Guid? userId = null, Guid? notificationTypeId = null, CancellationToken ct = default);
    Task AddAsync(NotificationTemplate template, CancellationToken ct = default);
    Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default);
}
