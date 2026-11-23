using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NotificationGateway.API.Authentication;
using NotificationGateway.Application.Templates.CreateTemplate;
using NotificationGateway.Application.Templates.PreviewTemplate;
using NotificationGateway.Application.Templates.PublishTemplate;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.API.Controllers;

[ApiController]
[Route("api/v1/templates")]
public class TemplatesController(
    IMediator mediator,
    NotificationContext context,
    ITemplateRepository repo) : ControllerBase
{
    public record CreateTemplateRequest(
        Guid NotificationTypeDefinitionId,
        NotificationChannel Channel,
        string Language,
        JsonElement Content,
        string? Subject,
        IList<string>? Variables,
        Guid? UserId = null);

    public record PreviewTemplateRequest(
        JsonElement Content,
        string? Subject,
        JsonElement Payload);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? notificationTypeId,
        CancellationToken ct)
    {
        var list = await repo.GetAllAsync(context.TenantId, context.ApplicationId, userId, notificationTypeId, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var t = await repo.GetByIdAsync(context.TenantId, id, ct);
        if (t is null) return NotFound();
        return Ok(t);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request, CancellationToken ct)
    {
        // Associate with specified user, or with the currently authenticated client if it's a User GUID
        Guid? templateUserId = request.UserId;
        if (!templateUserId.HasValue && Guid.TryParse(context.ClientId, out var parsedClientId))
        {
            templateUserId = parsedClientId;
        }

        var cmd = new CreateTemplateCommand(
            context.TenantId,
            context.ApplicationId,
            request.NotificationTypeDefinitionId,
            request.Channel,
            request.Language,
            request.Content.GetRawText(),
            request.Subject,
            request.Variables,
            templateUserId);

        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var success = await mediator.Send(new PublishTemplateCommand(context.TenantId, context.ApplicationId, id), ct);
        if (!success) return NotFound();
        return Ok(new { message = "Template published successfully." });
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewTemplateRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new PreviewTemplateCommand(
            request.Content.GetRawText(),
            request.Subject,
            request.Payload.GetRawText()), ct);

        return Ok(result);
    }
}
