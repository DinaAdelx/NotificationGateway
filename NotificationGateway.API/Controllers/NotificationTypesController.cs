using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationGateway.API.Authentication;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Application.NotificationTypes.RegisterNotificationType;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.API.Controllers;

[ApiController]
[Route("api/v1/notification-types")]
[Authorize]
public class NotificationTypesController(
    IMediator mediator,
    NotificationContext context,
    INotificationTypeRepository repo,
    IPayloadValidator validator) : ControllerBase
{
    public record RegisterTypeRequest(
        string Code,
        string DisplayName,
        string? Description,
        JsonElement PayloadSchema,
        IList<NotificationChannel> AllowedChannels,
        string DefaultLanguage = "en");

    public record ValidatePayloadRequest(JsonElement Payload);

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await repo.GetAllAsync(context.TenantId, context.ApplicationId, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var type = await repo.GetByIdAsync(context.TenantId, id, ct);
        if (type is null) return NotFound();
        return Ok(type);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterTypeRequest request, CancellationToken ct)
    {
        var cmd = new RegisterNotificationTypeCommand(
            context.TenantId,
            context.ApplicationId,
            request.Code,
            request.DisplayName,
            request.Description,
            request.PayloadSchema.GetRawText(),
            request.AllowedChannels,
            request.DefaultLanguage);

        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/validate-payload")]
    public async Task<IActionResult> ValidatePayload(Guid id, [FromBody] ValidatePayloadRequest request, CancellationToken ct)
    {
        var type = await repo.GetByIdAsync(context.TenantId, id, ct);
        if (type is null) return NotFound();

        var result = await validator.ValidateAsync(type.PayloadSchemaJson, request.Payload.GetRawText(), ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await repo.DeleteAsync(context.TenantId, id, ct);
        return NoContent();
    }
}
