# Testing Report

## 1. Automated Unit Test Suite (`NotificationGateway.Tests`)
- **Framework**: xUnit + FluentAssertions
- **Command**:
  ```powershell
  dotnet test NotificationGateway.Tests/NotificationGateway.Tests.csproj --no-restore
  ```
- **Latest Observed Result**:
  - **Total**: 22
  - **Passed**: 22
  - **Failed**: 0
  - **Skipped**: 0

### Covered Integration & Lifecycle Tests (`NotificationGateway.Tests/Integration/GatewayLifecycleTests.cs`)
| Test | Area Verified |
|---|---|
| `TenantMiddleware_RejectsCrossTenantHeaderSpoofing_With403` | Rejects mismatched `X-Tenant-Id` header vs JWT claim (`403 Forbidden`) |
| `TenantMiddleware_ValidatesXApiKey_AndBlocksInvalidKey` | Validates `X-Api-Key` against `ApplicationClients` and rejects invalid keys (`401`) |
| `ProviderConfig_UpsertEncryptsAtRest_AndDecryptsOnRead` | Encrypts provider secrets at rest via `ICredentialEncryptor` (AES-256-CBC) and decrypts on read |
| `DeliveryDispatcher_FailsOverToSecondaryProvider_WhenPrimaryFails` | Executes secondary-provider failover via `ResolveFallbacksAsync` |
| `DeliveryDispatcher_SchedulesHangfireRetryOnTransientFailure_AndRecovers` | Schedules real Hangfire delayed retry (`ScheduledState`), verifies non-English (`ar`) localization, and recovers on retry |
| `OutboxProcessor_EnforcesUserPreferenceOptOut` | Enforces `UserPreference.PreferredChannels` opt-out before creating deliveries |
| `PushAndVoiceProviders_SimulatedSend_Succeed` | Verifies `PushNotificationProvider` and `VoiceNotificationProvider` |

### Covered Unit Test Cases (`NotificationGateway.Tests/Unit/CoreTests.cs`)
| Test | Area Verified |
|---|---|
| `AesCredentialEncryptor_Roundtrip_Success` | AES-256-CBC encrypt/decrypt round-trip |
| `Pbkdf2PasswordHasher_VerifyPassword_Success` | Password hashing & verification |
| `JsonSchemaPayloadValidator_ValidAndInvalid` | NJsonSchema payload contract validation |
| `HandlebarsRenderer_Preview_Success` | Handlebars template variable substitution |
| `HandlebarsRenderer_WebhookJsonTemplate_RendersCleanJson` | Clean JSON rendering for Webhook channel templates |
| `WebhookSignature_GeneratesValidHmacSha256` | HMAC-SHA256 webhook signature generation |
| `SmsProvider_SimulatedSend_ReturnsSuccessAndId` | Vonage SMS provider simulation fallback (`SM...`) |
| `WhatsAppProvider_SimulatedSend_ReturnsSuccessAndWamid` | Green API WhatsApp provider simulation fallback (`wamid....`) |
| `User_Create_AcceptsAdminAndCustomerRoles` (4 cases) | Normalizes `Admin` / `admin` / `Customer` / `customer` |
| `User_Create_RejectsInvalidRole` | Rejects unsupported roles with `ArgumentException` |

---

## 2. Manual & Interactive Verification Tools
1. **Interactive Web Test UI (`http://localhost:5022/`)**:
   - Multi-channel dispatch across `SignalR`, `WhatsApp` (Green API), `Sms` (Vonage), `Email` (Gmail SMTP), and `Webhook`.
   - Live SignalR stream with Web Audio API chime and unread badge.
   - Real-time Delivery Tracker polling (`/api/v1/notifications/{id}/deliveries`).
   - Webhook Consumer Inspector (`/api/webhooks/received`) with HMAC-SHA256 signature verification.
   - Role-based UI: `Admin` login/register unlocks the **Admin Dashboard** tab with KPIs and filters; `Customer` sees only the standard UI.
2. **Swagger UI (`http://localhost:5022/swagger`)**:
   - Full OpenAPI explorer with pre-populated `X-Tenant-Id` / `X-Application-Id` headers and Bearer JWT authorization.
3. **HTTP Test Suite ([`NotificationGateway.API.http`](../NotificationGateway.API/NotificationGateway.API.http))**:
   - 14 ready-to-run requests covering types, per-channel templates, multi-channel send, WhatsApp/SMS single-channel sends, deliveries, webhooks, `Admin`/`Customer` registration, login, and `/api/v1/admin/dashboard`.
