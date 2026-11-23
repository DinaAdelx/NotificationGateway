using MediatR;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.Application.Notifications.GetNotification;

public record GetNotificationQuery(Guid TenantId, Guid ApplicationId, Guid NotificationId) : IRequest<NotificationDto?>;

public record NotificationDto(
    Guid Id, string TypeCode, string Status, string Priority,
    DateTime CreatedAt, DateTime? CompletedAt, DateTime? ScheduledAt,
    string? CorrelationId, int SchemaVersion);

public class GetNotificationHandler(INotificationRepository repo)
    : IRequestHandler<GetNotificationQuery, NotificationDto?>
{
    public async Task<NotificationDto?> Handle(GetNotificationQuery request, CancellationToken ct)
    {
        var n = await repo.GetByIdAsync(request.TenantId, request.NotificationId, ct);
        if (n is null || n.ApplicationId != request.ApplicationId) return null;

        return new NotificationDto(n.Id, n.NotificationTypeCode, n.Status.ToString(),
            n.Priority.ToString(), n.CreatedAt, n.CompletedAt, n.ScheduledAt, n.CorrelationId, n.SchemaVersion);
    }
}
