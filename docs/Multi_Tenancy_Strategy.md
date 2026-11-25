# Multi-Tenancy Strategy

## Overview
The gateway uses a **Shared Database, Shared Schema with Tenant & Application Isolation** model. Every tenant-scoped entity carries `TenantId` (and where applicable `ApplicationId`), ensuring strict logical isolation across tenants and their client applications.

---

## Tenant & Application Resolution Pipeline
Implemented in [`TenantResolutionMiddleware`](../NotificationGateway.API/Middleware/TenantResolutionMiddleware.cs) and scoped per request via [`NotificationContext`](../NotificationGateway.API/Authentication/NotificationContext.cs):

1. **Custom HTTP Headers** (service-to-service / Swagger / UI):
   - `X-Tenant-Id` (`Guid`)
   - `X-Application-Id` (`Guid`)
   - `X-Client-Id` (`string`)
2. **JWT Bearer Claims** (overrides headers when authenticated):
   - `tenant_id` → `NotificationContext.TenantId`
   - `app_id` → `NotificationContext.ApplicationId`
   - `ClaimTypes.NameIdentifier` → `NotificationContext.ClientId`
   - `ClaimTypes.Role` (`Admin`) → `NotificationContext.IsSystemAdministrator`
3. **Development / Test Fallback**:
   - If neither header nor JWT claim is present, falls back to the seeded default tenant (`11111111-1111-1111-1111-111111111111`) and application (`22222222-2222-2222-2222-222222222222`) so local testing works out of the box.

---

## Data Isolation Guarantees
- **Repositories**: All repository queries (`ITemplateRepository`, `INotificationRepository`, `INotificationTypeRepository`, `IProviderConfigurationRepository`, `IDeliveryRepository`, `IUserRepository`) require `tenantId` (and `applicationId` where applicable) as explicit parameters.
- **Idempotency Scoping**: Notification `IdempotencyKey` is scoped per tenant/application so two tenants can safely use identical idempotency keys without collision.
- **Provider Credentials**: `TenantProviderConfiguration` stores per-tenant/per-application provider settings (`ConfigurationJson` and `EncryptedCredentials`), while `appsettings.json` can provide environment-level defaults for `Smtp`, `Sms`, and `WhatsApp`.
- **SignalR Isolation**: `NotificationHub` supports joining `{tenantId}:{userId}` groups so real-time pushes target only the intended tenant's user.
- **Role Scoping (`Admin` vs `Customer`)**: Users belong to a specific `(TenantId, ApplicationId)` pair and hold either `Admin` or `Customer` role.
