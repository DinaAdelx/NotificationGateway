# Improvements Log

This file tracks all improvements and changes made to the **Multi-Tenant Notification Gateway** from **2026-11-26** onward.

---

## 2026-11-26 — PR Review Remediation (All 7 Critical Findings Resolved)

### 1. Tenant Isolation & Authentication Enforcement
- **[`TenantResolutionMiddleware.cs`](NotificationGateway.API/Middleware/TenantResolutionMiddleware.cs)**:
  - Enforced JWT claim-based tenant/application binding (`tenant_id`, `app_id`).
  - Added cross-tenant/cross-application header spoofing protection (`403 Forbidden` when `X-Tenant-Id` or `X-Application-Id` mismatches JWT claims).
  - Added `X-Api-Key` service-to-service validation against `ApplicationClients.HashedApiKey` (`PBKDF2`) with `401 Unauthorized` on invalid/revoked keys.
  - Removed silent fallback to default tenant/application on protected `/api/v1/*` routes.
- **Controllers**: Added `[Authorize]` to [`NotificationsController.cs`](NotificationGateway.API/Controllers/NotificationsController.cs), [`NotificationTypesController.cs`](NotificationGateway.API/Controllers/NotificationTypesController.cs), and [`TemplatesController.cs`](NotificationGateway.API/Controllers/TemplatesController.cs).

### 2. Secondary-Provider Failover
- **[`IProviderResolver.cs`](NotificationGateway.Application/Abstractions/IProviderResolver.cs)** & **[`DeliveryDispatcher.cs`](NotificationGateway.Infrastructure/BackgroundJobs/DeliveryDispatcher.cs)**:
  - Exposed `GetProvider(string providerCode)` on `IProviderResolver`.
  - Updated `DeliveryDispatcher.DispatchDeliveryAsync` to call `ResolveFallbacksAsync(...)` and execute secondary provider attempts when the primary provider fails.

### 3. Real Hangfire Retry Scheduling & Scheduled Delivery
- **[`DeliveryDispatcher.cs`](NotificationGateway.Infrastructure/BackgroundJobs/DeliveryDispatcher.cs)**:
  - Wired `IBackgroundJobClient.Schedule<DeliveryDispatcher>(..., delay)` so transient failures enqueue real Hangfire delayed retry jobs with exponential backoff.
- **[`OutboxProcessor.cs`](NotificationGateway.Infrastructure/BackgroundJobs/OutboxProcessor.cs)**:
  - Added support for delayed dispatch when `Notification.ScheduledAt` is in the future.

### 4. AES-256 Credential Encryption & Secret Hygiene
- **[`ProviderConfigurationsController.cs`](NotificationGateway.API/Controllers/ProviderConfigurationsController.cs)**:
  - Encrypted provider configurations at rest using `ICredentialEncryptor` (`AesCredentialEncryptor`) into `EncryptedCredentials`, storing `"{}"` in `ConfigurationJson`.
  - Decrypted `EncryptedCredentials` on authenticated reads (`GET /api/v1/provider-configurations`).
- **[`DeliveryDispatcher.cs`](NotificationGateway.Infrastructure/BackgroundJobs/DeliveryDispatcher.cs)**:
  - Added `DecryptConfig(...)` so primary and fallback providers receive decrypted credentials at dispatch time.
- **[`docs/notification-provider-defaults.md`](docs/notification-provider-defaults.md)**:
  - Removed plaintext secrets and replaced them with placeholders.

### 5. Notification Localization
- **[`Notification.cs`](NotificationGateway.Domain/Entities/Notification.cs)** & **[`SubmitNotificationHandler.cs`](NotificationGateway.Application/Notifications/SubmitNotification/SubmitNotificationHandler.cs)**:
  - Added `Language` property to `Notification` and persisted `request.Language` from `SubmitNotificationCommand`.
- **[`Program.cs`](NotificationGateway.API/Program.cs)**:
  - Added idempotent startup SQL guard for `Notifications.Language`.
- **[`DeliveryDispatcher.cs`](NotificationGateway.Infrastructure/BackgroundJobs/DeliveryDispatcher.cs)**:
  - Replaced hardcoded `"en"` with `notification.Language` when calling `ITemplateRenderer.RenderAsync`.

### 6. Push & Voice Channels + `UserPreference` Enforcement
- **New Providers**:
  - Added [`PushNotificationProvider.cs`](NotificationGateway.Infrastructure/Providers/Push/PushNotificationProvider.cs) (`PUSH`) and [`VoiceNotificationProvider.cs`](NotificationGateway.Infrastructure/Providers/Voice/VoiceNotificationProvider.cs) (`VOICE`), registered in [`Program.cs`](NotificationGateway.API/Program.cs) and [`ProviderResolver.cs`](NotificationGateway.Infrastructure/Services/ProviderResolver.cs).
- **User Preferences**:
  - Added [`IUserPreferenceRepository.cs`](NotificationGateway.Domain/Interfaces/IUserPreferenceRepository.cs), [`UserPreferenceRepository.cs`](NotificationGateway.Infrastructure/Persistence/Repositories/UserPreferenceRepository.cs), and [`UserPreferencesController.cs`](NotificationGateway.API/Controllers/UserPreferencesController.cs) (`GET/PUT /api/v1/preferences`).
  - Enforced `DisabledNotificationTypes`, `PreferredChannels`, and `QuietHours` in [`OutboxProcessor.cs`](NotificationGateway.Infrastructure/BackgroundJobs/OutboxProcessor.cs).

### 7. Automated Integration & Lifecycle Testing + Docs
- **[`GatewayLifecycleTests.cs`](NotificationGateway.Tests/Integration/GatewayLifecycleTests.cs)**:
  - Added 7 integration/lifecycle tests covering cross-tenant header spoofing (`403`), `X-Api-Key` validation (`401`), AES-256 encryption/decryption round-trip, secondary-provider failover, Hangfire retry scheduling + Arabic (`ar`) localization + recovery, `UserPreference` opt-out enforcement, and `Push`/`Voice` providers (**22/22 tests passing**).
- **[`docs/Testing_Report.md`](docs/Testing_Report.md)** & **[`docs/API_Documentation.md`](docs/API_Documentation.md)**:
  - Updated with new test results and `/api/v1/preferences` endpoints.
