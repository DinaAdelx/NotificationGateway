using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationGateway.API.Authentication;
using NotificationGateway.Domain.Entities;
using NotificationGateway.Domain.Enums;
using NotificationGateway.Domain.Interfaces;

namespace NotificationGateway.API.Controllers;

[ApiController]
[Route("api/v1/preferences")]
[Authorize]
public class UserPreferencesController(
    NotificationContext context,
    IUserPreferenceRepository repo,
    IUnitOfWork uow) : ControllerBase
{
    public record UpsertPreferenceRequest(
        string ExternalUserId,
        string? PreferredLanguage,
        IList<NotificationChannel>? PreferredChannels,
        IList<string>? DisabledNotificationTypes,
        bool QuietHoursEnabled = false,
        TimeOnly? QuietHoursStart = null,
        TimeOnly? QuietHoursEnd = null,
        string? QuietHoursTimezone = "UTC");

    [HttpGet("{externalUserId}")]
    public async Task<IActionResult> Get(string externalUserId, CancellationToken ct)
    {
        var pref = await repo.GetByExternalUserIdAsync(context.TenantId, context.ApplicationId, externalUserId, ct);
        if (pref is null) return NotFound();
        return Ok(pref);
    }

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertPreferenceRequest request, CancellationToken ct)
    {
        var pref = await repo.GetByExternalUserIdAsync(context.TenantId, context.ApplicationId, request.ExternalUserId, ct);
        var isNew = pref is null;
        pref ??= UserPreference.Create(context.TenantId, context.ApplicationId, request.ExternalUserId);

        pref.PreferredChannels.Clear();
        if (request.PreferredChannels is not null)
        {
            foreach (var ch in request.PreferredChannels)
                pref.PreferredChannels.Add(ch);
        }

        pref.DisabledNotificationTypes.Clear();
        if (request.DisabledNotificationTypes is not null)
        {
            foreach (var t in request.DisabledNotificationTypes)
                pref.DisabledNotificationTypes.Add(t);
        }

        if (request.QuietHoursEnabled && request.QuietHoursStart.HasValue && request.QuietHoursEnd.HasValue)
            pref.SetQuietHours(request.QuietHoursStart.Value, request.QuietHoursEnd.Value, request.QuietHoursTimezone ?? "UTC");
        else
            pref.DisableQuietHours();

        if (isNew) await repo.AddAsync(pref, ct);
        else await repo.UpdateAsync(pref, ct);

        await uow.SaveChangesAsync(ct);
        return Ok(pref);
    }
}
