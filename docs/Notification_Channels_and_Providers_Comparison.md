# Notification Channels & Providers Comparison

## Overview
The Multi-Tenant Notification Gateway decouples **Notification Channels** (`NotificationChannel` enum) from **Provider Adapters** (`INotificationProvider`). A single `POST /api/v1/notifications` request can fan out across multiple channels simultaneously, each using its own channel-specific Handlebars template and provider adapter.

---

## Supported Channels (`NotificationChannel` Enum)

| Enum Value | Name | Provider Code | Implemented Adapter | Template Format |
|---|---|---|---|---|
| `0` | `Email` | `SMTP` | `SmtpEmailProvider` | Rich HTML (`content.content`) + `Subject` |
| `1` | `Sms` | `SMS` | `SmsNotificationProvider` | Short plain text (`content.message`) |
| `2` | `WhatsApp` | `WHATSAPP` | `WhatsAppNotificationProvider` | WhatsApp markdown (`*bold*`, `\n`) |
| `3` | `Push` | — | Reserved in enum | — |
| `4` | `InApp` | `INAPP` | `InAppProvider` | Short text / in-app record |
| `5` | `SignalR` | `SIGNALR` | `SignalRNotificationProvider` | Real-time toast + `Subject` |
| `6` | `Voice` | — | Reserved in enum | — |
| `7` | `Webhook` | `WEBHOOK` | `WebhookNotificationProvider` | Structured JSON payload |

---

## Provider Comparison

| Feature | Gmail SMTP (`SmtpEmailProvider`) | Vonage (`SmsNotificationProvider`) | Green API (`WhatsAppNotificationProvider`) | SignalR (`SignalRNotificationProvider`) | Webhook (`WebhookNotificationProvider`) |
|---|---|---|---|---|---|
| **Transport** | MailKit SMTP (`StartTls`, port `587`) | HTTPS REST (`rest.nexmo.com/sms/json`) | HTTPS REST (`api.green-api.com`) | WebSockets (`/hubs/notifications`) | HTTPS POST (`HttpClient`) |
| **Free / Dev Tier** | Free Gmail + 16-char App Password | Free trial credit (no CC; verified numbers) | Free Developer Plan (3 chats, QR-linked) | Built-in (free) | Built-in (free) |
| **Egypt (`+20`) Support** | Global | ✅ Yes (strips `+` → `20...`) | ✅ Yes (formats `20...@c.us`) | Global | Global |
| **HTML Support** | ✅ Yes (`SupportsHtml: true`) | ❌ No (plain text only) | ❌ No (WhatsApp markdown only) | ✅ Optional (`SupportsHtml: true`) | ❌ No (JSON payload) |
| **Delivery Receipt** | Provider SMTP response | ✅ `messages[0].status == "0"` + `message-id` | ✅ `idMessage` | Connection broadcast | HTTP status code |
| **Security / Signing** | TLS + App Password | `api_key` + `api_secret` | `IdInstance` + `ApiToken` | Tenant/User Hub groups | HMAC-SHA256 (`X-Hub-Signature-256`) |
| **Simulation Fallback** | ✅ If password is missing or placeholder | ✅ If `ApiKey` / `ApiSecret` missing | ✅ If `IdInstance` / `ApiToken` missing | Broadcasts to connected clients | Posts to configured `WebhookUrl` |

---

## Per-Channel Template Resolution
`HandlebarsTemplateRenderer` resolves templates by `(TenantId, ApplicationId, NotificationTypeDefinitionId, Channel, Language)`:
1. Exact match for `(Channel, Language)`.
2. Language fallback to `"en"` on the same `Channel`.
3. Cross-channel fallback to any active template for that `NotificationTypeDefinitionId` — if a plain-text channel falls back to an `Email` HTML template, `StripHtml()` automatically removes HTML tags so raw `<h1>`/`<p>` markup never leaks to SMS or WhatsApp.
