using MediatR;
using NotificationGateway.Domain.Interfaces;
using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Application.Templates.CreateTemplate;

public record CreateTemplateCommand(
    Guid TenantId,
    Guid ApplicationId,
    Guid NotificationTypeDefinitionId,
    NotificationChannel Channel,
    string Language,
    string ContentJson,
    string? Subject,
    IList<string>? Variables,
    Guid? UserId = null) : IRequest<CreateTemplateResult>;

public record CreateTemplateResult(Guid Id, Guid NotificationTypeDefinitionId, Guid? UserId);

public class CreateTemplateHandler(
    ITemplateRepository repo,
    INotificationTypeRepository notifTypeRepo,
    IUnitOfWork uow)
    : IRequestHandler<CreateTemplateCommand, CreateTemplateResult>
{
    public async Task<CreateTemplateResult> Handle(CreateTemplateCommand request, CancellationToken ct)
    {
        // Verify notification type exists in tenant
        var notifType = await notifTypeRepo.GetByIdAsync(request.TenantId, request.NotificationTypeDefinitionId, ct)
            ?? throw new InvalidOperationException($"Notification type definition '{request.NotificationTypeDefinitionId}' not found.");

        var template = Domain.Entities.NotificationTemplate.Create(
            request.TenantId,
            request.ApplicationId,
            request.NotificationTypeDefinitionId,
            request.Channel,
            request.Language,
            request.ContentJson,
            request.Subject,
            request.Variables,
            request.UserId);

        await repo.AddAsync(template, ct);
        await uow.SaveChangesAsync(ct);

        return new CreateTemplateResult(template.Id, template.NotificationTypeDefinitionId, template.UserId);
    }
}
