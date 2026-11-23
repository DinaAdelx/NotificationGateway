using MediatR;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.Application.Templates.PublishTemplate;

public record PublishTemplateCommand(Guid TenantId, Guid ApplicationId, Guid TemplateId) : IRequest<bool>;

public class PublishTemplateHandler(ITemplateRepository repo, IUnitOfWork uow)
    : IRequestHandler<PublishTemplateCommand, bool>
{
    public async Task<bool> Handle(PublishTemplateCommand request, CancellationToken ct)
    {
        var template = await repo.GetByIdAsync(request.TenantId, request.TemplateId, ct);
        if (template is null || template.ApplicationId != request.ApplicationId) return false;

        template.Publish();
        await repo.UpdateAsync(template, ct);
        await uow.SaveChangesAsync(ct);
        return true;
    }
}
