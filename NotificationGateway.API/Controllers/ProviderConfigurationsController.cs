using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationGateway.API.Authentication;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.API.Controllers;

[ApiController]
[Route("api/v1/provider-configurations")]
[Authorize(Roles = "Admin")]
public class ProviderConfigurationsController(
    NotificationContext context,
    IProviderConfigurationRepository repo,
    IUnitOfWork uow) : ControllerBase
{
    public record CreateProviderConfigRequest(
        string ProviderCode,
        NotificationChannel Channel,
        JsonElement Configuration,
        string? EncryptedCredentials,
        bool IsPrimary = false,
        int Priority = 0);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] NotificationChannel channel, CancellationToken ct)
    {
        var configs = await repo.GetEnabledByChannelAsync(context.TenantId, context.ApplicationId, channel, ct);
        return Ok(configs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var config = await repo.GetByIdAsync(context.TenantId, id, ct);
        if (config is null) return NotFound();
        return Ok(config);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProviderConfigRequest request, CancellationToken ct)
    {
        var config = TenantProviderConfiguration.Create(
            context.TenantId,
            context.ApplicationId,
            request.ProviderCode,
            request.Channel,
            request.Configuration.GetRawText(),
            request.EncryptedCredentials,
            request.IsPrimary,
            request.Priority);

        await repo.AddAsync(config, ct);
        await uow.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = config.Id }, config);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await repo.DeleteAsync(context.TenantId, id, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
