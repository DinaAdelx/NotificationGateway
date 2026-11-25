# Security & Secrets Management

## 1. Authentication & Role-Based Access Control (RBAC)
- **JWT Bearer Authentication**: Configured in `Program.cs` (`ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`) using HMAC-SHA256 (`JwtTokenService`). Tokens expire after 7 days and carry `NameIdentifier`, `Email`, `Name`, `Role`, `tenant_id`, and `app_id` claims.
- **Password Hashing**: `Pbkdf2PasswordHasher` hashes user passwords before persistence (`User.PasswordHash`).
- **Strict Registration Roles**: `User.Create` and `AuthController` only allow **`Admin`** or **`Customer`** roles (defaulting to `Customer`; invalid roles return `400 Bad Request`).
- **Endpoint Authorization**:
  - `Admin`-only (`[Authorize(Roles = "Admin")]`):
    - `GET /api/v1/admin/dashboard` (`AdminDashboardController`)
    - `GET/POST/DELETE /api/v1/provider-configurations` (`ProviderConfigurationsController`)
    - `POST/DELETE /api/v1/notification-types` (`NotificationTypesController`)
    - `POST /api/v1/templates` and `POST /api/v1/templates/{id}/publish` (`TemplatesController`)
  - `Customer` & `Admin`:
    - `POST/GET /api/v1/notifications`, `GET /api/v1/notifications/{id}/deliveries`, `POST /api/v1/notifications/{id}/cancel`
    - `/api/webhooks/*`, `/api/v1/auth/*`

---

## 2. Credential Encryption at Rest
- [`AesCredentialEncryptor`](../NotificationGateway.Infrastructure/Security/AesCredentialEncryptor.cs) uses **AES-256-CBC** with a random 16-byte IV prepended to the ciphertext, Base64-encoded.
- Configured via `Security:EncryptionKey` (32-byte Base64 string) in `appsettings.json`.

---

## 3. Webhook Payload Signing (HMAC-SHA256)
- `WebhookNotificationProvider` signs outbound webhook payloads using `HMAC-SHA256` with the tenant's configured `SecretKey` (default seeded secret: `whsec_default_gateway_secret_2026!`).
- Outbound headers:
  - `X-Hub-Signature-256: sha256=<hex>`
  - `X-Webhook-Timestamp`
  - `X-Event-ID`
  - `X-Delivery-Attempt`
- `WebhooksController` (`/api/webhooks/receive-test`) verifies incoming signatures in constant time and displays verification status in the Web UI.

---

## 4. Secrets Management Best Practices
- **Never commit production secrets** (`Smtp:Password`, `Sms:ApiSecret`, `WhatsApp:ApiToken`, `Jwt:SecretKey`, `Security:EncryptionKey`, SQL passwords) to source control.
- Use **User Secrets** (`dotnet user-secrets`), **`appsettings.Development.json`** (gitignored), or **environment variables** (`Smtp__Password`, `Sms__ApiSecret`, `WhatsApp__ApiToken`, `Jwt__SecretKey`) in staging/production.
- **Gmail SMTP**: Always use a 16-character **Google App Password** (with 2-Step Verification enabled), never your primary Gmail password.
- **Safe Error Logging**: `DeliveryAttempt.SafeErrorMessage` stores sanitized failure details without exposing raw credentials.
