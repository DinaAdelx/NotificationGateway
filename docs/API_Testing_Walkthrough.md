# Multi-Tenant Notification Gateway — Testing Guide & API Walkthrough

The Multi-Tenant Notification Gateway supports multi-channel dispatch across **WhatsApp, SMS, Email, SignalR, and Webhooks** with a unified template and notification model.

## 🚀 Two Ways to Test

### 1. Interactive Web Test UI (`/`)
Open in your browser:
👉 **`http://localhost:5022/`**
- **SignalR Real-Time Push Stream**: Live inbound notification stream with browser audio chime, unread badges, and JSON inspector.
- **Multi-Channel Dispatch Form**: Dispatch a single notification simultaneously across:
  - ☑ **SignalR** (instant WebSocket in browser)
  - ☑ **WhatsApp** (`+15551234567`)
  - ☑ **SMS** (`+15551234567`)
  - ☑ **Email** (`customer@example.com`)
  - ☑ **Webhook** (`http://localhost:5022/api/webhooks/receive-test`)
- **Delivery Tracker**: View per-channel delivery attempts, provider status, and provider message IDs in real-time.
- **Webhook Consumer Inspector**: Inspect incoming webhooks received by `/api/webhooks/receive-test`, with HMAC-SHA256 signature verification and payload display.
- **Swagger Direct Link**: Click the **Swagger API Docs** button in the header bar to open Swagger in a new tab anytime.

---

### 2. Swagger API Documentation (`/swagger`)
Open in your browser:
👉 **`http://localhost:5022/swagger`**

> **💡 Multi-Tenant Headers & Automatic Fallbacks**:  
> Swagger automatically pre-populates these default header values on every request:
> - `X-Tenant-Id`: `11111111-1111-1111-1111-111111111111`
> - `X-Application-Id`: `22222222-2222-2222-2222-222222222222`  
> *(If omitted, the gateway automatically falls back to these default IDs so requests won't fail with 401 Unauthorized).*

---

## 📋 Quick Reference: Values to Save Along the Way

| Variable Name | Obtained From | Example Value | Where Used |
|---|---|---|---|
| `USER_ID` | Step 1 (`POST /api/v1/auth/register`) | `ee259c97-fcb9-4b1d-8175-13aeaffbffe3` | Step 5 (`userId`) |
| `JWT_TOKEN` | Step 1 (`POST /api/v1/auth/register`) | `eyJhbGciOiJIUzI1Ni...` | Step 2 (Swagger Authorize) |
| `TYPE_ID` | Step 3 (`POST /api/v1/notification-types`) | `3fa85f64-5717-4562-b3fc-2c963f66afa6` | Steps 4 & 5 |
| `TEMPLATE_ID` | Step 5 (`POST /api/v1/templates`) | `99887766-5544-3322-1100-aabbccddeeff` | Step 6 (`publish`) |
| `NOTIFICATION_ID` | Step 8 (`POST /api/v1/notifications`) | `33445566-7788-9900-aabb-ccddeeff0011` | Step 9 (`deliveries`) |

---

## Step 1: Register a New User (`Admin` or `Customer`)

Creates an authenticated user under the current tenant and application client. Allowed roles are **`Admin`** and **`Customer`** (defaults to `Customer` if omitted):
- **`Admin`**: Allowed to view and use **all APIs** (including provider configurations, notification type registration, template creation/publishing) plus the **Admin Dashboard** in the Web UI (`http://localhost:5022/`) and `GET /api/v1/admin/dashboard`.
- **`Customer`**: Sees only the standard notification dispatch, SignalR live feed, delivery tracker, and webhook inspector UI.

- **Method**: `POST`
- **URL**: `/api/v1/auth/register`
- **Request Body**:
```json
{
  "email": "sarah.dev@example.com",
  "password": "SecurePassword123!",
  "fullName": "Sarah Developer",
  "role": "Admin"
}
```

- **Expected Response (`200 OK`)**:
```json
{
  "userId": "ee259c97-fcb9-4b1d-8175-13aeaffbffe3",
  "email": "sarah.dev@example.com",
  "fullName": "Sarah Developer",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```
> 📌 **Copy and save:**
> 1. `userId` ➔ This will be your `USER_ID`.
> 2. `token` ➔ This will be your `JWT_TOKEN`.

---

## Step 2: (Optional) Authorize with JWT in Swagger

1. Click the green **Authorize** button at the top right of the Swagger UI page.
2. In the `Value:` input box, type:
   ```text
   Bearer <YOUR_JWT_TOKEN>
   ```
3. Click **Authorize** and then **Close**.

---

## Step 3: Register a Notification Type (with JSON Schema)

Define a new dynamic notification type (`ORDER_SHIPPED`) with a required payload contract.

- **Method**: `POST`
- **URL**: `/api/v1/notification-types`
- **Request Body**:
```json
{
  "code": "ORDER_SHIPPED",
  "displayName": "Order Shipped Notification",
  "description": "Triggered when an order package is dispatched to the carrier",
  "allowedChannels": [
    "Email",
    "Sms",
    "WhatsApp",
    "SignalR",
    "Webhook"
  ],
  "defaultLanguage": "en",
  "payloadSchema": {
    "type": "object",
    "required": [
      "orderId",
      "customerName",
      "carrier",
      "trackingUrl"
    ],
    "properties": {
      "orderId": { "type": "string" },
      "customerName": { "type": "string" },
      "carrier": { "type": "string" },
      "trackingUrl": { "type": "string" }
    },
    "additionalProperties": false
  }
}
```
*(Channel Enum accepts string names or integers: `Email`=0, `Sms`=1, `WhatsApp`=2, `Push`=3, `InApp`=4, `SignalR`=5, `Voice`=6, `Webhook`=7)*

- **Expected Response (`201 Created`)**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "code": "ORDER_SHIPPED"
}
```
> 📌 **Copy the `id` from the response ➔ This will be your `TYPE_ID`.**

---

## Step 4: Validate a Payload Against the Registered Schema

Test payload validation before submitting notifications to make sure the schema works as expected.

- **Method**: `POST`
- **URL**: `/api/v1/notification-types/{id}/validate-payload`
  - *Replace `{id}` parameter with your `TYPE_ID` from Step 3.*
- **Request Body**:
```json
{
  "payload": {
    "orderId": "ORD-98765",
    "customerName": "Sarah",
    "carrier": "FedEx",
    "trackingUrl": "https://fedex.com/track/ORD-98765"
  }
}
```

- **Expected Response (`200 OK`)**:
```json
{
  "isValid": true,
  "errors": []
}
```

---

## Step 5: Create Channel-Specific Templates (One per Channel)

A single `NotificationTypeDefinition` (such as `ORDER_SHIPPED`) can have **multiple templates — one for each channel**:
- **Email (`"channel": "Email"`)**: Rich HTML (`content.content`) + `subject`
- **SMS (`"channel": "Sms"`)**: Short plain text (`content.message`), no HTML
- **WhatsApp (`"channel": "WhatsApp"`)**: WhatsApp-formatted text (`*bold*`, `\n`)
- **SignalR (`"channel": "SignalR"`)**: Concise toast message + `subject`
- **Webhook (`"channel": "Webhook"`)**: Structured JSON payload

- **Method**: `POST`
- **URL**: `/api/v1/templates`
- **Request Body (Email example — repeat with `"channel": "Sms"`, `"WhatsApp"`, `"SignalR"`, `"Webhook"` for each channel)**:
> ⚠️ **Important**: Replace `"3fa85f64-5717-4562-b3fc-2c963f66afa6"` with your actual `TYPE_ID` from Step 3, and replace `"ee259c97-fcb9-4b1d-8175-13aeaffbffe3"` with your actual `USER_ID` from Step 1.
```json
{
  "notificationTypeDefinitionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "ee259c97-fcb9-4b1d-8175-13aeaffbffe3",
  "channel": "Email",
  "language": "en",
  "subject": "Great news, {{customerName}}! Your order {{orderId}} is on the way",
  "content": {
    "content": "<h1>Order Shipped</h1><p>Hi {{customerName}},</p><p>Your order <strong>{{orderId}}</strong> has been dispatched via {{carrier}}.</p><p><a href=\"{{trackingUrl}}\">Track Your Package</a></p>"
  },
  "variables": [
    "customerName",
    "orderId",
    "carrier",
    "trackingUrl"
  ]
}
```

Example **SMS** body (`"channel": "Sms"`):
```json
{
  "notificationTypeDefinitionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "channel": "Sms",
  "language": "en",
  "subject": null,
  "content": {
    "message": "Hi {{customerName}}, your order {{orderId}} shipped via {{carrier}}. Track: {{trackingUrl}}"
  },
  "variables": ["customerName", "orderId", "carrier", "trackingUrl"]
}
```

- **Expected Response (`201 Created`)**:
```json
{
  "id": "99887766-5544-3322-1100-aabbccddeeff",
  "notificationTypeDefinitionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "ee259c97-fcb9-4b1d-8175-13aeaffbffe3"
}
```
> 📌 **Copy the created template `id` ➔ This will be your `TEMPLATE_ID`.**

---

## Step 6: Publish the Template

New templates start in `Draft` status. Publishing activates them so the delivery system can use them to render notifications.

- **Method**: `POST`
- **URL**: `/api/v1/templates/{id}/publish`
  - *Replace `{id}` parameter with your `TEMPLATE_ID` from Step 5.*
- **Expected Response (`200 OK`)**:
```json
{
  "message": "Template published successfully."
}
```

---

## Step 7: Configure a Delivery Provider

Configure an active delivery provider for the tenant (e.g. SMTP for Email).

- **Method**: `POST`
- **URL**: `/api/v1/provider-configurations`
- **Request Body**:
```json
{
  "providerCode": "SMTP",
  "channel": 0,
  "isPrimary": true,
  "priority": 1,
  "configuration": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UseSsl": false,
    "FromEmail": "notifications@mygateway.com",
    "FromName": "Order Gateway"
  },
  "encryptedCredentials": null
}
```

- **Expected Response (`201 Created`)**:
```json
{
  "id": "55667788-99aa-bbcc-ddee-ff0011223344",
  "providerCode": "SMTP",
  "channel": "Email",
  "isEnabled": true,
  "isPrimary": true
}
```

---

## Step 8: Submit a Notification

Submit an actual notification request using the type code, payload, recipient, and idempotency key.

- **Method**: `POST`
- **URL**: `/api/v1/notifications`
- **Header**: `Idempotency-Key: test-order-98765-dispatch`
- **Request Body**:
```json
{
  "type": "ORDER_SHIPPED",
  "recipient": {
    "externalUserId": "cust-500",
    "email": "dina.adel.sobhi@gmail.com",
    "phone": "+201000000000",
    "webhookUrl": "http://localhost:5022/api/webhooks/receive-test"
  },
  "channels": [
    "Email",
    "Sms",
    "WhatsApp",
    "SignalR",
    "Webhook"
  ],
  "language": "en",
  "priority": "Normal",
  "payload": {
    "orderId": "ORD-98765",
    "customerName": "Dina",
    "carrier": "Aramex",
    "trackingUrl": "https://aramex.com/track/ORD-98765"
  },
  "correlationId": "corr-checkout-abc-123"
}
```
*(Priority Enum: `0` = Low, `1` = Normal, `2` = High, `3` = Critical)*

- **Expected Response (`202 Accepted`)**:
```json
{
  "notificationId": "33445566-7788-9900-aabb-ccddeeff0011",
  "status": "Accepted",
  "acceptedAt": "2026-11-24T09:45:00Z",
  "statusUrl": "/api/v1/notifications/33445566-7788-9900-aabb-ccddeeff0011",
  "isDuplicate": false
}
```
> 📌 **Copy the `notificationId` ➔ This will be your `NOTIFICATION_ID`.**

---

## Step 9: Track Delivery Status & Attempts

Inspect background worker delivery execution and attempt logs.

- **Method**: `GET`
- **URL**: `/api/v1/notifications/{id}/deliveries`
  - *Replace `{id}` parameter with your `NOTIFICATION_ID` from Step 8.*
- **Expected Response (`200 OK`)**:
```json
[
  {
    "id": "fafb7c80-fc71-4e88-a8e3-4e86cc2d7104",
    "notificationId": "33445566-7788-9900-aabb-ccddeeff0011",
    "channel": "Email",
    "recipientAddress": "customer@example.com",
    "status": "Retrying",
    "attemptCount": 1,
    "nextAttemptAt": "2026-11-24T09:14:33Z",
    "createdAt": "2026-11-24T09:13:57Z",
    "attempts": [
      {
        "id": "363376e2-5ce5-4ef3-990c-cd1094b0b83d",
        "attemptNumber": 1,
        "providerCode": "SMTP",
        "status": "Failed",
        "failureCategory": "ProviderUnavailable",
        "errorCode": "SMTP_ERROR",
        "safeErrorMessage": "5.7.0 Authentication Required...",
        "isRetryable": true,
        "startedAt": "2026-11-24T09:13:57Z",
        "completedAt": "2026-11-24T09:14:03Z",
        "durationMilliseconds": 5383
      }
    ]
  }
]
```
> 💡 **Notice**: The response cleanly isolates each delivery attempt, status, failure category, safe error message, duration, and the scheduled `nextAttemptAt` for automatic retry with exponential backoff!

---

## Step 10: Test Duplicate Idempotency Protection

Re-send the request from **Step 8** with the exact same `Idempotency-Key: test-order-98765-dispatch`.

- **Expected Response (`200 OK`)**:
```json
{
  "notificationId": "33445566-7788-9900-aabb-ccddeeff0011",
  "status": "Accepted",
  "acceptedAt": "2026-11-24T09:45:00Z",
  "statusUrl": "/api/v1/notifications/33445566-7788-9900-aabb-ccddeeff0011",
  "isDuplicate": true
}
```
Notice that:
- It returns `isDuplicate: true`.
- It returns the original tracking ID without generating duplicate outbox entries or duplicate deliveries.

---

## Step 11: Query the Admin Dashboard (`Admin` Role Only)

Requires a Bearer JWT token for a user with `"role": "Admin"`.

- **Method**: `GET`
- **URL**: `/api/v1/admin/dashboard?role=Customer&channel=Email&status=Sent&search=ORD`
- **Returns**:
  - `summary`: `totalUsers`, `adminCount`, `customerCount`, `totalNotificationTypes`, `totalTemplates`, `totalNotifications`, `totalDeliveries`, `sentDeliveries`, `failedDeliveries`
  - `users`: Filtered list of registered users
  - `notificationTypes`: Filtered list of notification types with active template counts
  - `recentDeliveries`: Latest 50 deliveries matching the selected filters
