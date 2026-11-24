using HandlebarsDotNet;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;
using System.Text.Json;

namespace NotificationGateway.Infrastructure.Services;

public class HandlebarsTemplateRenderer(ITemplateRepository templateRepo) : ITemplateRenderer
{
    public async Task<TemplateRenderResult?> RenderAsync(
        Guid tenantId, Guid applicationId, Guid notificationTypeId,
        NotificationChannel channel, string language, string payloadJson, CancellationToken ct)
    {
        var template = await templateRepo.GetActiveAsync(tenantId, applicationId, notificationTypeId, channel, language, ct);

        // language fallback
        if (template is null && language != "en")
            template = await templateRepo.GetActiveAsync(tenantId, applicationId, notificationTypeId, channel, "en", ct);

        // Multi-channel fallback: if no channel-specific template exists, fall back to ANY active template for this notification type!
        if (template is null)
        {
            var allTemplates = await templateRepo.GetAllAsync(tenantId, applicationId, null, notificationTypeId, ct);
            template = allTemplates.FirstOrDefault(t => t.Status == TemplateStatus.Active);
        }

        if (template is null) return null;

        return Preview(template.ContentJson, template.Subject, payloadJson);
    }

    public TemplateRenderResult Preview(string contentJson, string? subject, string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson) ?? new();

        string RenderString(string template)
        {
            var compiled = Handlebars.Compile(template);
            return compiled(payload);
        }

        // Try to render body from contentJson
        string body;
        try
        {
            var content = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(contentJson) ?? new();
            if (content.TryGetValue("content", out var contentEl))
                body = RenderString(contentEl.GetString() ?? string.Empty);
            else if (content.TryGetValue("message", out var msgEl))
                body = RenderString(msgEl.GetString() ?? string.Empty);
            else
                body = RenderString(contentJson);
        }
        catch
        {
            body = contentJson;
        }

        string? renderedSubject = subject is not null ? RenderString(subject) : null;

        return new TemplateRenderResult(body, renderedSubject);
    }
}
