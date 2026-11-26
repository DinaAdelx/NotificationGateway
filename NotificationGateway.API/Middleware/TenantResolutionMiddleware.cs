using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NotificationGateway.API.Authentication;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Infrastructure.Persistence;

namespace NotificationGateway.API.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FallbackAppId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public async Task InvokeAsync(
        HttpContext context,
        NotificationContext notifContext,
        NotificationGatewayDbContext db,
        IPasswordHasher hasher)
    {
        Guid headerTenantId = Guid.Empty;
        Guid headerAppId = Guid.Empty;
        var hasTenantHeader = context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantVal)
                              && Guid.TryParse(tenantVal, out headerTenantId);
        var hasAppHeader = context.Request.Headers.TryGetValue("X-Application-Id", out var appVal)
                           && Guid.TryParse(appVal, out headerAppId);

        // 1. JWT Bearer Authentication (strictly enforces claim-based tenant isolation)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            var appClaim = context.User.FindFirst("app_id")?.Value;

            if (Guid.TryParse(tenantClaim, out var claimTenantId))
                notifContext.TenantId = claimTenantId;
            if (Guid.TryParse(appClaim, out var claimAppId))
                notifContext.ApplicationId = claimAppId;

            // Prevent cross-tenant header spoofing
            if (hasTenantHeader && notifContext.TenantId != Guid.Empty && headerTenantId != notifContext.TenantId)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Cross-tenant header mismatch is forbidden." });
                return;
            }

            if (hasAppHeader && notifContext.ApplicationId != Guid.Empty && headerAppId != notifContext.ApplicationId)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Cross-application header mismatch is forbidden." });
                return;
            }

            notifContext.ClientId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            notifContext.IsSystemAdministrator = context.User.IsInRole("Admin");

            await next(context);
            return;
        }

        // 2. Service-to-service X-Api-Key Authentication (validated against ApplicationClients)
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyVal) && !string.IsNullOrWhiteSpace(apiKeyVal))
        {
            var rawKey = apiKeyVal.ToString();
            var prefix = rawKey.Length >= 8 ? rawKey[..8] : rawKey;

            var candidates = await db.ApplicationClients
                .AsNoTracking()
                .Where(c => c.IsActive && c.RevokedAt == null && (!hasTenantHeader || c.TenantId == headerTenantId))
                .ToListAsync(context.RequestAborted);

            var matched = candidates.FirstOrDefault(c =>
                (string.IsNullOrEmpty(c.ApiKeyPrefix) || rawKey.StartsWith(c.ApiKeyPrefix, StringComparison.Ordinal))
                && hasher.VerifyPassword(rawKey, c.HashedApiKey));

            if (matched is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Invalid or revoked X-Api-Key." });
                return;
            }

            notifContext.TenantId = matched.TenantId;
            notifContext.ApplicationId = matched.Id;
            notifContext.ClientId = matched.ClientId;

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, matched.Id.ToString()),
                new Claim(ClaimTypes.Name, matched.Name),
                new Claim(ClaimTypes.Role, "Customer"),
                new Claim("tenant_id", matched.TenantId.ToString()),
                new Claim("app_id", matched.Id.ToString())
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));

            await next(context);
            return;
        }

        // 3. Public Auth routes (/api/v1/auth/login & /register) allow explicit headers or seeded default tenant
        if (context.Request.Path.StartsWithSegments("/api/v1/auth"))
        {
            notifContext.TenantId = hasTenantHeader ? headerTenantId : FallbackTenantId;
            notifContext.ApplicationId = hasAppHeader ? headerAppId : FallbackAppId;
        }

        await next(context);
    }
}
