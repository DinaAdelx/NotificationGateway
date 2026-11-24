using System.Security.Claims;
using NotificationGateway.API.Authentication;

namespace NotificationGateway.API.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FallbackAppId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public async Task InvokeAsync(HttpContext context, NotificationContext notifContext)
    {
        // 1. Check custom headers (for API key / service-to-service auth)
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantVal) &&
            Guid.TryParse(tenantVal, out var tenantId))
        {
            notifContext.TenantId = tenantId;
        }

        if (context.Request.Headers.TryGetValue("X-Application-Id", out var appVal) &&
            Guid.TryParse(appVal, out var appId))
        {
            notifContext.ApplicationId = appId;
        }

        if (context.Request.Headers.TryGetValue("X-Client-Id", out var clientVal))
        {
            notifContext.ClientId = clientVal.ToString();
        }

        // 2. Also check claims if JWT present
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            if (Guid.TryParse(tenantClaim, out var claimTenantId))
            {
                notifContext.TenantId = claimTenantId;
            }

            var appClaim = context.User.FindFirst("app_id")?.Value;
            if (Guid.TryParse(appClaim, out var claimAppId))
            {
                notifContext.ApplicationId = claimAppId;
            }

            notifContext.ClientId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? notifContext.ClientId;
            notifContext.IsSystemAdministrator = context.User.IsInRole("Admin");
        }

        // 3. Fallback to default tenant/app if neither header nor JWT claim was set
        if (notifContext.TenantId == Guid.Empty)
        {
            notifContext.TenantId = FallbackTenantId;
        }

        if (notifContext.ApplicationId == Guid.Empty)
        {
            notifContext.ApplicationId = FallbackAppId;
        }

        await next(context);
    }
}
