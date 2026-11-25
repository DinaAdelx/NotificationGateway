# API Documentation

- **Base URL**: `http://localhost:5022` (HTTPS: `https://localhost:7126`)
- **Swagger UI**: `http://localhost:5022/swagger`
- **Web Test UI**: `http://localhost:5022/`
- **Common Headers**:
  - `X-Tenant-Id`: `11111111-1111-1111-1111-111111111111` (optional fallback)
  - `X-Application-Id`: `22222222-2222-2222-2222-222222222222` (optional fallback)
  - `Authorization`: `Bearer <JWT_TOKEN>` (required for `[Authorize(Roles = "Admin")]` endpoints)

---

## 1. Authentication (`/api/v1/auth`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/v1/auth/register` | Public | Register a new user with `role`: `"Customer"` (default) or `"Admin"`. Returns `userId`, `email`, `fullName`, `role`, `token`. |
| `POST` | `/api/v1/auth/login` | Public | Authenticate user and receive JWT `token` + `role`. |

---

## 2. Notifications (`/api/v1/notifications`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/v1/notifications` | Public / Auth | Submit a multi-channel notification (`channels`: `["Email","Sms","WhatsApp","SignalR","Webhook"]`). Supports `Idempotency-Key` header. |
| `GET` | `/api/v1/notifications/{id}` | Public / Auth | Get notification details by ID. |
| `POST` | `/api/v1/notifications/{id}/cancel` | Public / Auth | Request cancellation of a scheduled/pending notification. |
| `GET` | `/api/v1/notifications/{id}/deliveries` | Public / Auth | List per-channel deliveries and `attempts` (`providerCode`, `providerMessageId`, `status`, `safeErrorMessage`, `durationMilliseconds`). |

---

## 3. Notification Types (`/api/v1/notification-types`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/notification-types` | Public / Auth | List notification type definitions for the tenant/application. |
| `GET` | `/api/v1/notification-types/{id}` | Public / Auth | Get a notification type definition by ID. |
| `POST` | `/api/v1/notification-types/{id}/validate-payload` | Public / Auth | Validate a JSON payload against the type's NJsonSchema contract. |
| `POST` | `/api/v1/notification-types` | **Admin** | Register a new notification type with `payloadSchema` and `allowedChannels`. |
| `DELETE` | `/api/v1/notification-types/{id}` | **Admin** | Delete a notification type definition. |

---

## 4. Templates (`/api/v1/templates`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/templates` | Public / Auth | List templates (optional filters: `userId`, `notificationTypeId`). |
| `GET` | `/api/v1/templates/{id}` | Public / Auth | Get a template by ID. |
| `POST` | `/api/v1/templates/preview` | Public / Auth | Preview Handlebars rendering without saving. |
| `POST` | `/api/v1/templates` | **Admin** | Create a channel-specific template (`Email`, `Sms`, `WhatsApp`, `SignalR`, `Webhook`). |
| `POST` | `/api/v1/templates/{id}/publish` | **Admin** | Publish a `Draft` template to `Active`. |

---

## 5. Provider Configurations (`/api/v1/provider-configurations`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/provider-configurations?channel={ch}` | **Admin** | List enabled provider configs for a channel. |
| `GET` | `/api/v1/provider-configurations/{id}` | **Admin** | Get a provider configuration by ID. |
| `POST` | `/api/v1/provider-configurations` | **Admin** | Create a provider configuration. |
| `DELETE` | `/api/v1/provider-configurations/{id}` | **Admin** | Delete a provider configuration. |

---

## 6. Admin Dashboard (`/api/v1/admin`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/admin/dashboard` | **Admin** | Returns KPI `summary` (`totalUsers`, `adminCount`, `customerCount`, `totalNotificationTypes`, `totalTemplates`, `totalNotifications`, `totalDeliveries`, `sentDeliveries`, `failedDeliveries`) plus filtered `users`, `notificationTypes`, and `recentDeliveries`. Query params: `search`, `role`, `channel`, `status`, `notificationType`, `fromDate`, `toDate`. |

---

## 7. Webhooks & SignalR

| Protocol / Endpoint | Purpose |
|---|---|
| `POST /api/webhooks/receive-test` | Built-in test consumer verifying `X-Hub-Signature-256` HMAC-SHA256 signatures |
| `GET /api/webhooks/received` | List captured webhook deliveries for the UI inspector |
| `POST /api/webhooks/clear` | Clear captured webhook history |
| `SignalR /hubs/notifications` | WebSocket hub (`JoinTenantGroup`, `AcknowledgeNotification`, `OnNotificationReceived`, `ReceiveNotification`) |

For step-by-step payloads, see [`API_Testing_Walkthrough.md`](API_Testing_Walkthrough.md) and [`NotificationGateway.API.http`](../NotificationGateway.API/NotificationGateway.API.http).
