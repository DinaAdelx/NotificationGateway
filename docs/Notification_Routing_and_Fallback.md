# Notification Routing & Fallback Strategy

## Overview
Notification delivery is orchestrated asynchronously via an **Outbox + Hangfire Background Job** pipeline (`OutboxProcessor` → `DeliveryDispatcher`) combined with `ProviderResolver` for dynamic provider selection, retries, and fallbacks.

---

## 1. Provider Resolution Order (`ProviderResolver`)
When `DeliveryDispatcher` processes a `NotificationDelivery` for a given `(TenantId, ApplicationId, Channel, NotificationTypeId, Priority)`:

1. **Routing Rules (`IRoutingRepository.GetActiveRulesAsync`)**:
   - Looks up active `RoutingRule` records for the tenant, application, channel, and notification type.
   - Matches a rule for the notification's `Priority` (or a wildcard rule where `Priority == null`).
   - Uses `rule.PrimaryProviderCode` if an enabled `TenantProviderConfiguration` exists for it.
2. **Primary Tenant Provider Config**:
   - Falls back to the enabled `TenantProviderConfiguration` marked `IsPrimary = true` (or the first enabled config for that channel).
3. **Auto-Fallback Default Configuration**:
   - If no DB row exists for the channel, `ProviderResolver` synthesizes a default configuration (`SignalR` → `SIGNALR`, `Webhook` → `WEBHOOK`, `Sms` → `SMS`, `WhatsApp` → `WHATSAPP`, `Email` → `SMTP`, `InApp` → `INAPP`), allowing `appsettings.json` or simulation mode to handle delivery without failing.

---

## 2. Retry & Dead-Letter Handling (`DeliveryDispatcher`)
- Each dispatch records a `DeliveryAttempt` with `AttemptNumber`, `ProviderCode`, `Status`, `FailureCategory`, `ErrorCode`, `SafeErrorMessage`, and `DurationMilliseconds`.
- If `result.IsSuccess == false` and `result.IsRetryable == true`:
  - Up to **5 attempts** (`MaxAttempts = 5`) are retried with exponential backoff (`nextAttemptAt` scheduled).
  - Once attempts are exhausted (or `IsRetryable == false`), the delivery is marked `DeadLettered` / `Failed`.

---

## 3. Channel-Level & Template-Level Fallbacks
- **Template Fallback (`HandlebarsTemplateRenderer`)**:
  - Exact `(Channel, Language)` → `(Channel, "en")` → any active template for the `NotificationTypeDefinitionId`.
  - If a non-Email channel (`Sms`, `WhatsApp`, `SignalR`, `InApp`) falls back to an `Email` HTML template, `StripHtml()` removes HTML tags automatically.
- **Provider Simulation Fallback**:
  - `SmtpEmailProvider`, `SmsNotificationProvider`, and `WhatsAppNotificationProvider` automatically enter **simulation mode** (logging the payload and returning a simulated ID) when real credentials are absent or contain placeholder strings.
- **Secondary Provider Fallbacks**:
  - `ProviderResolver.ResolveFallbacksAsync` reads `rule.FallbackProviderCodes` from `RoutingRule` to locate secondary enabled provider configurations.
