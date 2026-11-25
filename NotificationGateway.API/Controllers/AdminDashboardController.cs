using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationGateway.API.Authentication;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Infrastructure.Persistence;

namespace NotificationGateway.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController(
    NotificationContext context,
    NotificationGatewayDbContext db) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] NotificationChannel? channel,
        [FromQuery] DeliveryStatus? status,
        [FromQuery] string? notificationType,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var tenantId = context.TenantId;
        var appId = context.ApplicationId;

        // 1. Summary KPIs (tenant + application scoped)
        var usersQuery = db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.ApplicationId == appId);

        var totalUsers = await usersQuery.CountAsync(ct);
        var adminCount = await usersQuery.CountAsync(u => u.Role == "Admin", ct);
        var customerCount = await usersQuery.CountAsync(u => u.Role == "Customer", ct);

        var typesQuery = db.NotificationTypeDefinitions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ApplicationId == appId);

        var totalNotificationTypes = await typesQuery.CountAsync(ct);
        var totalTemplates = await db.NotificationTemplates
            .AsNoTracking()
            .CountAsync(t => t.TenantId == tenantId && t.ApplicationId == appId, ct);

        var totalNotifications = await db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.TenantId == tenantId && n.ApplicationId == appId, ct);

        var deliveriesBase = db.NotificationDeliveries
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId);

        var totalDeliveries = await deliveriesBase.CountAsync(ct);
        var sentDeliveries = await deliveriesBase.CountAsync(
            d => d.Status == DeliveryStatus.Sent || d.Status == DeliveryStatus.Delivered, ct);
        var failedDeliveries = await deliveriesBase.CountAsync(
            d => d.Status == DeliveryStatus.Failed || d.Status == DeliveryStatus.DeadLettered, ct);

        // 2. Filtered Users
        var filteredUsersQuery = usersQuery;
        if (!string.IsNullOrWhiteSpace(role))
            filteredUsersQuery = filteredUsersQuery.Where(u => u.Role == role);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            filteredUsersQuery = filteredUsersQuery.Where(u =>
                u.Email.ToLower().Contains(term) || u.FullName.ToLower().Contains(term));
        }

        var users = await filteredUsersQuery
            .OrderByDescending(u => u.CreatedAt)
            .Take(100)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.Role,
                u.IsActive,
                u.CreatedAt
            })
            .ToListAsync(ct);

        // 3. Filtered Notification Types
        var allTypes = await typesQuery
            .OrderBy(t => t.Code)
            .Select(t => new
            {
                t.Id,
                t.Code,
                t.DisplayName,
                t.AllowedChannels,
                t.DefaultLanguage,
                t.CreatedAt,
                TemplateCount = db.NotificationTemplates.Count(tpl =>
                    tpl.TenantId == tenantId
                    && tpl.NotificationTypeDefinitionId == t.Id
                    && tpl.Status == TemplateStatus.Active)
            })
            .ToListAsync(ct);

        var filteredTypes = allTypes.Where(t =>
            (string.IsNullOrWhiteSpace(search)
                || t.Code.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                || t.DisplayName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            && (!channel.HasValue || t.AllowedChannels.Contains(channel.Value))
            && (string.IsNullOrWhiteSpace(notificationType)
                || t.Code.Equals(notificationType.Trim(), StringComparison.OrdinalIgnoreCase))
        ).ToList();

        // 4. Filtered Recent Deliveries
        var deliveriesQuery = from d in db.NotificationDeliveries.AsNoTracking()
                              join n in db.Notifications.AsNoTracking() on d.NotificationId equals n.Id
                              join nt in db.NotificationTypeDefinitions.AsNoTracking() on n.NotificationTypeDefinitionId equals nt.Id
                              where d.TenantId == tenantId && n.ApplicationId == appId
                              select new { Delivery = d, NotificationTypeCode = nt.Code };

        if (channel.HasValue)
            deliveriesQuery = deliveriesQuery.Where(x => x.Delivery.Channel == channel.Value);

        if (status.HasValue)
            deliveriesQuery = deliveriesQuery.Where(x => x.Delivery.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(notificationType))
        {
            var ntCode = notificationType.Trim().ToLower();
            deliveriesQuery = deliveriesQuery.Where(x => x.NotificationTypeCode.ToLower() == ntCode);
        }

        if (fromDate.HasValue)
            deliveriesQuery = deliveriesQuery.Where(x => x.Delivery.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            deliveriesQuery = deliveriesQuery.Where(x => x.Delivery.CreatedAt <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            deliveriesQuery = deliveriesQuery.Where(x =>
                x.Delivery.RecipientAddress.ToLower().Contains(term)
                || x.NotificationTypeCode.ToLower().Contains(term));
        }

        var recentDeliveries = await deliveriesQuery
            .OrderByDescending(x => x.Delivery.CreatedAt)
            .Take(50)
            .Select(x => new
            {
                x.Delivery.Id,
                NotificationType = x.NotificationTypeCode,
                Channel = x.Delivery.Channel.ToString(),
                x.Delivery.RecipientAddress,
                Status = x.Delivery.Status.ToString(),
                x.Delivery.AttemptCount,
                x.Delivery.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new
        {
            summary = new
            {
                totalUsers,
                adminCount,
                customerCount,
                totalNotificationTypes,
                totalTemplates,
                totalNotifications,
                totalDeliveries,
                sentDeliveries,
                failedDeliveries
            },
            users,
            notificationTypes = filteredTypes,
            recentDeliveries
        });
    }
}
