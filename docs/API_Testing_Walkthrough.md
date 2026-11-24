# Multi-Tenant Notification Gateway — API Testing Walkthrough

This guide provides a complete, end-to-end testing cycle with ready-to-use inputs for testing the gateway through Swagger UI (`http://localhost:5022/swagger`).

> **Note on Multi-Tenant Headers**:  
> Swagger automatically pre-populates these default header values on every request:
> - `X-Tenant-Id`: `11111111-1111-1111-1111-111111111111`
> - `X-Application-Id`: `22222222-2222-2222-2222-222222222222`

---

## Step 1: Register a New User

Creates an authenticated user under the current tenant and application client.

- **Method**: `POST`
- **URL**: `/api/v1/auth/register`
- **Request Body**:
```json
{
  "email": "sarah.dev@example.com",
  "password": "SecurePassword123!",
  "fullName": "Sarah Developer",
  "role": "Manager"
}
```

- **Expected Response (`200 OK`)**:
```json
{
  "userId": "d7488f57-e6ea-45a8-9bf1-d3ef6f4b66df",
  "email": "sarah.dev@example.com",
  "fullName": "Sarah Developer",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```
> 📌 **Save the `userId` and `token` for the next steps.**

---

## Step 2: (Optional) Authorize with JWT in Swagger

1. Click the green **Authorize** button at the top right of the Swagger page.
2. In the input box, enter:
   ```text
   Bearer <YOUR_TOKEN_FROM_STEP_1>
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
    0,
    4
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
*(Channel Enum: `0` = Email, `1` = Sms, `2` = WhatsApp, `3` = Push, `4` = InApp, `5` = SignalR)*

- **Expected Response (`201 Created`)**:
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "code": "ORDER_SHIPPED"
}
```
> 📌 **Save the `id` of this notification type (referred to below as `TYPE_ID`).**

---

## Step 4: Validate a Payload Against the Registered Schema

Test payload validation before submitting notifications.

- **Method**: `POST`
- **URL**: `/api/v1/notification-types/{id}/validate-payload` *(replace `{id}` with `TYPE_ID`)*
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

## Step 5: Create a Template Associated with the User

Create a template associated with the author's `userId` and the notification type.

- **Method**: `POST`
- **URL**: `/api/v1/templates`
- **Request Body**:
```json
{
  "notificationTypeDefinitionId": "PASTE_TYPE_ID_HERE",
  "userId": "PASTE_USER_ID_FROM_STEP_1_HERE",
  "channel": 0,
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

- **Expected Response (`201 Created`)**:
```json
{
  "id": "99887766-5544-3322-1100-aabbccddeeff",
  "notificationTypeDefinitionId": "PASTE_TYPE_ID_HERE",
  "userId": "PASTE_USER_ID_FROM_STEP_1_HERE"
}
```
> 📌 **Save the created template `id` (referred to below as `TEMPLATE_ID`).**

---

## Step 6: Publish the Template

New templates start in `Draft` status. Publishing activates them for dispatching.

- **Method**: `POST`
- **URL**: `/api/v1/templates/{id}/publish` *(replace `{id}` with `TEMPLATE_ID`)*
- **Expected Response (`200 OK`)**:
```json
{
  "message": "Template published successfully."
}
```

---

## Step 7: Configure a Delivery Provider

Configure an active email provider for the tenant.

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
  "id": "...",
  "providerCode": "SMTP",
  "channel": "Email",
  "isEnabled": true,
  "isPrimary": true
}
```

---

## Step 8: Submit a Notification

Submit an actual notification request using the type code and payload.

- **Method**: `POST`
- **URL**: `/api/v1/notifications`
- **Header**: `Idempotency-Key: test-order-98765-dispatch`
- **Request Body**:
```json
{
  "type": "ORDER_SHIPPED",
  "recipient": {
    "externalUserId": "cust-500",
    "email": "customer@example.com",
    "phone": "+1234567890"
  },
  "channels": [
    0
  ],
  "language": "en",
  "priority": 1,
  "payload": {
    "orderId": "ORD-98765",
    "customerName": "Sarah",
    "carrier": "FedEx",
    "trackingUrl": "https://fedex.com/track/ORD-98765"
  },
  "correlationId": "corr-checkout-abc-123"
}
```

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
> 📌 **Save the `notificationId`.**

---

## Step 9: Track Delivery Status & Attempts

Inspect background worker delivery execution and attempt logs.

- **Method**: `GET`
- **URL**: `/api/v1/notifications/{id}/deliveries` *(replace `{id}` with `notificationId`)*
- **Expected Response (`200 OK`)**:
```json
[
  {
    "id": "...",
    "notificationId": "33445566-7788-9900-aabb-ccddeeff0011",
    "channel": "Email",
    "recipientAddress": "customer@example.com",
    "status": "Processing",
    "attemptCount": 1,
    "attempts": [
      {
        "attemptNumber": 1,
        "providerCode": "SMTP",
        "status": "Processing"
      }
    ]
  }
]
```

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
