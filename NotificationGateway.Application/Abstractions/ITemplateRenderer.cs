using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;

namespace NotificationGateway.Application.Abstractions;

public record TemplateRenderResult(string Body, string? Subject);

/// <summary>Resolves the correct template and renders content with Handlebars.</summary>
public interface ITemplateRenderer
{
    Task<TemplateRenderResult?> RenderAsync(
        Guid tenantId, Guid applicationId,
        Guid notificationTypeId,
        NotificationChannel channel,
        string language,
        string payloadJson,
        CancellationToken ct = default);

    /// <summary>Preview rendering with an arbitrary payload — does not persist anything.</summary>
    TemplateRenderResult Preview(string contentJson, string? subject, string payloadJson);
}
