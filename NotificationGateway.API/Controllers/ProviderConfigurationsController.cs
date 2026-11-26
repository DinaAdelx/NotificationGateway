using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationGateway.API.Authentication;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;
using NotificationGateway.Infrastructure.Persistence;

namespace NotificationGateway.API.Controllers;

[ApiController]
[Route("api/v1/provider-configurations")]
[Authorize]
public class ProviderConfigurationsController(
    NotificationContext context,
    IProviderConfigurationRepository repo,
    NotificationGatewayDbContext db,
    IUnitOfWork uow) : ControllerBase
{
    public record CreateProviderConfigRequest(
        string ProviderCode,
        NotificationChannel Channel,
        JsonElement Configuration,
        string? EncryptedCredentials,
        bool IsPrimary = false,
        int Priority = 0);

    public record UpsertProviderConfigRequest(
        string ProviderCode,
        NotificationChannel Channel,
        JsonElement Configuration,
        bool IsPrimary = true,
        int Priority = 1);

    private Guid? CurrentUserId => Guid.TryParse(context.ClientId, out var uid) ? uid : null;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] NotificationChannel? channel, CancellationToken ct)
    {
        if (channel.HasValue)
        {
            var configs = await repo.GetEnabledByChannelAsync(context.TenantId, context.ApplicationId, channel.Value, ct);
            return Ok(configs);
        }

        var userId = CurrentUserId;
        var userConfigs = userId.HasValue
            ? await db.TenantProviderConfigurations
                .AsNoTracking()
                .Where(c => c.TenantId == context.TenantId
                         && c.ApplicationId == context.ApplicationId
                         && c.IsEnabled
                         && c.UserId == userId.Value)
                .ToListAsync(ct)
            : new List<TenantProviderConfiguration>();

        var targetChannels = new[]
        {
            (Channel: NotificationChannel.Email, ProviderCode: "SMTP"),
            (Channel: NotificationChannel.Sms, ProviderCode: "SMS"),
            (Channel: NotificationChannel.WhatsApp, ProviderCode: "WHATSAPP"),
            (Channel: NotificationChannel.Webhook, ProviderCode: "WEBHOOK")
        };

        var result = targetChannels.Select(target =>
        {
            var userRow = userConfigs.FirstOrDefault(c => c.Channel == target.Channel);
            return new
            {
                Id = userRow?.Id ?? Guid.Empty,
                ProviderCode = userRow?.ProviderCode ?? target.ProviderCode,
                Channel = target.Channel.ToString(),
                IsEnabled = userRow?.IsEnabled ?? false,
                IsPrimary = userRow?.IsPrimary ?? false,
                IsConfigured = userRow is not null,
                Configuration = ParseConfigOrNull(userRow?.ConfigurationJson),
                UpdatedAt = userRow?.UpdatedAt
            };
        });

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var config = await repo.GetByIdAsync(context.TenantId, id, ct);
        if (config is null) return NotFound();
        return Ok(config);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
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

    /// <summary>
    /// Creates or updates the authenticated user's own provider configuration for a channel.
    /// Persists in SQL Server with no default fallback.
    /// </summary>
    [HttpPut("upsert")]
    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert([FromBody] UpsertProviderConfigRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        var existing = await db.TenantProviderConfigurations
            .FirstOrDefaultAsync(c =>
                c.TenantId == context.TenantId
                && c.ApplicationId == context.ApplicationId
                && c.UserId == userId
                && c.Channel == request.Channel
                && c.ProviderCode == request.ProviderCode, ct);

        var rawJson = request.Configuration.GetRawText();

        if (existing is not null)
        {
            existing.UpdateConfiguration(rawJson, request.IsPrimary, request.Priority);
            await uow.SaveChangesAsync(ct);
            return Ok(new
            {
                existing.Id,
                existing.ProviderCode,
                Channel = existing.Channel.ToString(),
                existing.IsEnabled,
                existing.IsPrimary,
                IsConfigured = true,
                Message = $"Your {existing.Channel} ({existing.ProviderCode}) settings were updated and saved."
            });
        }

        var created = TenantProviderConfiguration.Create(
            context.TenantId,
            context.ApplicationId,
            request.ProviderCode,
            request.Channel,
            rawJson,
            null,
            request.IsPrimary,
            request.Priority,
            userId);

        await repo.AddAsync(created, ct);
        await uow.SaveChangesAsync(ct);

        return Ok(new
        {
            created.Id,
            created.ProviderCode,
            Channel = created.Channel.ToString(),
            created.IsEnabled,
            created.IsPrimary,
            IsConfigured = true,
            Message = $"Your {created.Channel} ({created.ProviderCode}) settings were saved."
        });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await repo.DeleteAsync(context.TenantId, id, ct);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    private static JsonElement? ParseConfigOrNull(string? dbJson)
    {
        if (string.IsNullOrWhiteSpace(dbJson) || dbJson == "{}")
            return null;

        try
        {
            using var doc = JsonDocument.Parse(dbJson);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }
}
