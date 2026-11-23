using Microsoft.EntityFrameworkCore;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Interfaces;
using NotificationGateway.Infrastructure.Persistence;

namespace NotificationGateway.Infrastructure.Persistence.Repositories;

public class NotificationRepository(NotificationGatewayDbContext db) : INotificationRepository
{
    public Task<Notification?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Notifications.FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id, ct);

    public Task<Notification?> GetByIdempotencyKeyAsync(Guid tenantId, Guid applicationId, string key, CancellationToken ct) =>
        db.Notifications.FirstOrDefaultAsync(n => n.TenantId == tenantId && n.ApplicationId == applicationId && n.IdempotencyKey == key, ct);

    public async Task<IReadOnlyList<Notification>> GetByTenantAsync(Guid tenantId, Guid applicationId, int page, int pageSize, CancellationToken ct) =>
        await db.Notifications
            .Where(n => n.TenantId == tenantId && n.ApplicationId == applicationId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

    public async Task AddAsync(Notification notification, CancellationToken ct) =>
        await db.Notifications.AddAsync(notification, ct);

    public Task UpdateAsync(Notification notification, CancellationToken ct)
    {
        db.Notifications.Update(notification);
        return Task.CompletedTask;
    }
}

public class NotificationTypeRepository(NotificationGatewayDbContext db) : INotificationTypeRepository
{
    public Task<NotificationTypeDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.NotificationTypeDefinitions.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

    public Task<NotificationTypeDefinition?> GetByCodeAsync(Guid tenantId, Guid applicationId, string code, CancellationToken ct) =>
        db.NotificationTypeDefinitions.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ApplicationId == applicationId && t.Code == code.ToUpperInvariant(), ct);

    public async Task<IReadOnlyList<NotificationTypeDefinition>> GetAllAsync(Guid tenantId, Guid applicationId, CancellationToken ct) =>
        await db.NotificationTypeDefinitions.Where(t => t.TenantId == tenantId && t.ApplicationId == applicationId).ToListAsync(ct);

    public async Task AddAsync(NotificationTypeDefinition type, CancellationToken ct) =>
        await db.NotificationTypeDefinitions.AddAsync(type, ct);

    public Task UpdateAsync(NotificationTypeDefinition type, CancellationToken ct)
    {
        db.NotificationTypeDefinitions.Update(type);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var entity = await GetByIdAsync(tenantId, id, ct);
        if (entity is not null) db.NotificationTypeDefinitions.Remove(entity);
    }

    public Task<bool> ExistsAsync(Guid tenantId, Guid applicationId, string code, CancellationToken ct) =>
        db.NotificationTypeDefinitions.AnyAsync(t => t.TenantId == tenantId && t.ApplicationId == applicationId && t.Code == code.ToUpperInvariant(), ct);
}

public class TemplateRepository(NotificationGatewayDbContext db) : ITemplateRepository
{
    public Task<NotificationTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.NotificationTemplates.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

    public Task<NotificationTemplate?> GetActiveAsync(Guid tenantId, Guid applicationId, Guid notificationTypeId, Domain.Enums.NotificationChannel channel, string language, CancellationToken ct) =>
        db.NotificationTemplates.Where(t =>
            t.TenantId == tenantId && t.ApplicationId == applicationId &&
            t.NotificationTypeDefinitionId == notificationTypeId &&
            t.Channel == channel && t.Language == language &&
            t.Status == Domain.Enums.TemplateStatus.Active)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<NotificationTemplate>> GetAllAsync(Guid tenantId, Guid applicationId, Guid? userId = null, Guid? notificationTypeId = null, CancellationToken ct = default)
    {
        var query = db.NotificationTemplates.Where(t => t.TenantId == tenantId && t.ApplicationId == applicationId);
        if (userId.HasValue)
            query = query.Where(t => t.UserId == userId.Value);
        if (notificationTypeId.HasValue)
            query = query.Where(t => t.NotificationTypeDefinitionId == notificationTypeId.Value);

        return await query.ToListAsync(ct);
    }

    public async Task AddAsync(NotificationTemplate template, CancellationToken ct) =>
        await db.NotificationTemplates.AddAsync(template, ct);

    public Task UpdateAsync(NotificationTemplate template, CancellationToken ct)
    {
        db.NotificationTemplates.Update(template);
        return Task.CompletedTask;
    }
}

public class OutboxRepository(NotificationGatewayDbContext db) : IOutboxRepository
{
    public async Task AddAsync(NotificationOutbox entry, CancellationToken ct) =>
        await db.NotificationOutbox.AddAsync(entry, ct);

    public async Task<IReadOnlyList<NotificationOutbox>> GetPendingAsync(int batchSize, CancellationToken ct) =>
        await db.NotificationOutbox.Where(o => !o.IsProcessed).OrderBy(o => o.CreatedAt).Take(batchSize).ToListAsync(ct);

    public async Task MarkProcessedAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.NotificationOutbox.FindAsync([id], ct);
        entity?.MarkProcessed();
    }
}

public class DeliveryRepository(NotificationGatewayDbContext db) : IDeliveryRepository
{
    public Task<NotificationDelivery?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.NotificationDeliveries.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == id, ct);

    public async Task<IReadOnlyList<NotificationDelivery>> GetByNotificationAsync(Guid tenantId, Guid notificationId, CancellationToken ct) =>
        await db.NotificationDeliveries.Where(d => d.TenantId == tenantId && d.NotificationId == notificationId).Include(d => d.Attempts).ToListAsync(ct);

    public async Task AddAsync(NotificationDelivery delivery, CancellationToken ct) =>
        await db.NotificationDeliveries.AddAsync(delivery, ct);

    public Task UpdateAsync(NotificationDelivery delivery, CancellationToken ct)
    {
        db.NotificationDeliveries.Update(delivery);
        return Task.CompletedTask;
    }

    public async Task AddAttemptAsync(DeliveryAttempt attempt, CancellationToken ct) =>
        await db.DeliveryAttempts.AddAsync(attempt, ct);

    public async Task<IReadOnlyList<NotificationDelivery>> GetPendingForRetryAsync(int batchSize, CancellationToken ct) =>
        await db.NotificationDeliveries
            .Where(d => (d.Status == Domain.Enums.DeliveryStatus.Pending || d.Status == Domain.Enums.DeliveryStatus.Retrying)
                && (d.NextAttemptAt == null || d.NextAttemptAt <= DateTime.UtcNow))
            .OrderBy(d => d.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
}

public class ProviderConfigurationRepository(NotificationGatewayDbContext db) : IProviderConfigurationRepository
{
    public Task<TenantProviderConfiguration?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        db.TenantProviderConfigurations.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public async Task<IReadOnlyList<TenantProviderConfiguration>> GetEnabledByChannelAsync(Guid tenantId, Guid applicationId, Domain.Enums.NotificationChannel channel, CancellationToken ct) =>
        await db.TenantProviderConfigurations
            .Where(c => c.TenantId == tenantId && c.ApplicationId == applicationId && c.Channel == channel && c.IsEnabled)
            .OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Priority)
            .ToListAsync(ct);

    public async Task AddAsync(TenantProviderConfiguration config, CancellationToken ct) =>
        await db.TenantProviderConfigurations.AddAsync(config, ct);

    public Task UpdateAsync(TenantProviderConfiguration config, CancellationToken ct)
    {
        db.TenantProviderConfigurations.Update(config);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var entity = await GetByIdAsync(tenantId, id, ct);
        if (entity is not null) db.TenantProviderConfigurations.Remove(entity);
    }
}

public class RoutingRepository(NotificationGatewayDbContext db) : IRoutingRepository
{
    public async Task<IReadOnlyList<RoutingRule>> GetActiveRulesAsync(Guid tenantId, Guid applicationId, Domain.Enums.NotificationChannel channel, Guid? notificationTypeId, CancellationToken ct) =>
        await db.RoutingRules
            .Where(r => r.TenantId == tenantId && r.ApplicationId == applicationId && r.Channel == channel && r.IsActive
                && (r.NotificationTypeDefinitionId == null || r.NotificationTypeDefinitionId == notificationTypeId))
            .OrderBy(r => r.Order).ToListAsync(ct);

    public async Task AddAsync(RoutingRule rule, CancellationToken ct) =>
        await db.RoutingRules.AddAsync(rule, ct);

    public Task UpdateAsync(RoutingRule rule, CancellationToken ct)
    {
        db.RoutingRules.Update(rule);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var entity = await db.RoutingRules.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);
        if (entity is not null) db.RoutingRules.Remove(entity);
    }
}
