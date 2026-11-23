using MediatR;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.Application.Notifications.CancelNotification;

public record CancelNotificationCommand(Guid TenantId, Guid ApplicationId, Guid NotificationId) : IRequest<bool>;

public class CancelNotificationHandler(INotificationRepository repo, IUnitOfWork uow)
    : IRequestHandler<CancelNotificationCommand, bool>
{
    public async Task<bool> Handle(CancelNotificationCommand request, CancellationToken ct)
    {
        var notification = await repo.GetByIdAsync(request.TenantId, request.NotificationId, ct);
        if (notification is null || notification.ApplicationId != request.ApplicationId) return false;

        notification.Cancel();
        await repo.UpdateAsync(notification, ct);
        await uow.SaveChangesAsync(ct);
        return true;
    }
}
