# Notification Gateway --- Provider Settings Reference

This document describes the configuration schema for the notification
channels supported by **NotificationGateway**.

> **Important:** Never commit real API keys, tokens, or passwords to source
> control. Store secrets in **User Secrets**, `appsettings.Development.json`
> (gitignored), environment variables, or per-user encrypted settings via
> `PUT /api/v1/provider-configurations/upsert` (encrypted at rest with AES-256-CBC).

## Supported notification channels

### 1. SMS (Vonage)

  Setting       Description
  ------------- ---------------------------------------
  `ApiKey`      Vonage API Key (`<VONAGE_API_KEY>`)
  `ApiSecret`   Vonage API Secret (`<VONAGE_API_SECRET>`)
  `SenderId`    Alphanumeric sender ID (e.g. `NotifGate`)

### 2. WhatsApp (Green API)

  Setting        Description
  -------------- ------------------------------------------
  `IdInstance`   Green API Instance ID (`<GREEN_ID>`)
  `ApiToken`     Green API Token (`<GREEN_API_TOKEN>`)

### 3. SMTP (Email)

  Setting       Description
  ------------- ------------------------------------------
  `Host`        SMTP host (e.g. `smtp.gmail.com`)
  `Port`        SMTP port (`587` for STARTTLS)
  `UseSsl`      `false` for STARTTLS, `true` for port `465`
  `FromEmail`   Sender email address
  `FromName`    Sender display name
  `Username`    SMTP username
  `Password`    16-character Gmail App Password (`<APP_PASSWORD>`)

## Configuration JSON reference

```json
{
  "Sms": {
    "ApiKey": "<VONAGE_API_KEY>",
    "ApiSecret": "<VONAGE_API_SECRET>",
    "SenderId": "NotifGate"
  },
  "WhatsApp": {
    "IdInstance": "<GREEN_ID_INSTANCE>",
    "ApiToken": "<GREEN_API_TOKEN>"
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UseSsl": false,
    "FromEmail": "sender@example.com",
    "FromName": "NotificationGateway",
    "Username": "sender@example.com",
    "Password": "<16_CHAR_APP_PASSWORD>"
  }
}
```
