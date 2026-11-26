# Notification Gateway --- Default Provider Settings

This document describes the default configuration for the notification
channels supported by **NotificationGateway**.

> **Important:** These values contain credentials and should be treated
> as secrets. Do not commit this file to a public repository, share it
> publicly, or include it in client-side code. Prefer environment
> variables or a secrets manager for production deployments.

## Supported notification channels

### 1. SMS

**Provider settings**

  Setting       Default value
  ------------- --------------------
  `ApiKey`      `ded500b6`
  `ApiSecret`   `YDuxkURrQ57fZSop`
  `SenderId`    `NotifGate`

**Recipient limitation**

> **SMS can only be sent to `01008552396` due to provider limitations.**
> Sending SMS messages to any other number is not supported by the
> current provider configuration.

### 2. WhatsApp

**Provider settings**

  Setting        Default value
  -------------- ------------------------------------------------------
  `IdInstance`   `710722813665`
  `ApiToken`     `a593a27806d548489d40d9b027934d6795452172a7d34b4e8c`

### 3. SMTP (Email)

**Server settings**

  Setting    Default value
  ---------- ------------------
  `Host`     `smtp.gmail.com`
  `Port`     `587`
  `UseSsl`   `false`

**Sender settings**

  Setting       Default value
  ------------- ----------------------------
  `FromEmail`   `ddinaadelsobhi@gmail.com`
  `FromName`    `NotificationGateway`

**Authentication settings**

  Setting      Default value
  ------------ ----------------------------
  `Username`   `ddinaadelsobhi@gmail.com`
  `Password`   `welg yxwo xzrz csdd`

## Configuration reference

The following JSON represents the default provider configuration:

``` json
{
  "Sms": {
    "ApiKey": "ded500b6",
    "ApiSecret": "YDuxkURrQ57fZSop",
    "SenderId": "NotifGate"
  },
  "WhatsApp": {
    "IdInstance": "710722813665",
    "ApiToken": "a593a27806d548489d40d9b027934d6795452172a7d34b4e8c"
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UseSsl": false,
    "FromEmail": "ddinaadelsobhi@gmail.com",
    "FromName": "NotificationGateway",
    "Username": "ddinaadelsobhi@gmail.com",
    "Password": "welg yxwo xzrz csdd"
  }
}
```

## Security notes

-   Keep provider API keys, tokens, and passwords out of source control.
-   Store production credentials in environment variables or a secure
    secrets manager.
-   Restrict access to configuration files containing credentials.
-   Since these credentials have been shared in a chat, consider
    rotating the API secrets, WhatsApp token, and SMTP app password
    before using them in production.
