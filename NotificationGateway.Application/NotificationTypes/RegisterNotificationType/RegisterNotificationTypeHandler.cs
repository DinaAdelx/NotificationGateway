using MediatR;
using NotificationGateway.Domain.Interfaces;
using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Application.NotificationTypes.RegisterNotificationType;

public record RegisterNotificationTypeCommand(
    Guid TenantId, Guid ApplicationId,
    string Code, string DisplayName, string? Description,
    string PayloadSchemaJson,
    IList<NotificationChannel> AllowedChannels,
    string DefaultLanguage) : IRequest<RegisterNotificationTypeResult>;

public record RegisterNotificationTypeResult(Guid Id, string Code);

public class RegisterNotificationTypeHandler(
    INotificationTypeRepository repo,
    IUnitOfWork uow)
    : IRequestHandler<RegisterNotificationTypeCommand, RegisterNotificationTypeResult>
{
    public async Task<RegisterNotificationTypeResult> Handle(RegisterNotificationTypeCommand request, CancellationToken ct)
    {
        if (await repo.ExistsAsync(request.TenantId, request.ApplicationId, request.Code, ct))
            throw new InvalidOperationException($"Notification type '{request.Code}' already exists.");

        var type = Domain.Entities.NotificationTypeDefinition.Create(
            request.TenantId, request.ApplicationId,
            request.Code, request.DisplayName,
            request.PayloadSchemaJson, request.AllowedChannels,
            request.DefaultLanguage, request.Description);

        await repo.AddAsync(type, ct);
        await uow.SaveChangesAsync(ct);

        return new RegisterNotificationTypeResult(type.Id, type.Code);
    }
}
