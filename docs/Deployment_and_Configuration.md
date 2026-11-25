# Deployment & Configuration Guide

## 1. Prerequisites
- **.NET 9 SDK** (`net9.0`)
- **SQL Server** (local or remote instance; Hangfire can also run with in-memory storage via `Hangfire:UseMemoryStorage = true`)
- Optional external provider accounts for live delivery:
  - **Email**: Gmail account with 2-Step Verification + 16-character **App Password**
  - **SMS**: **Vonage** account (`ApiKey`, `ApiSecret`)
  - **WhatsApp**: **Green API** instance (`IdInstance`, `ApiToken`) linked via WhatsApp QR code

---

## 2. Configuration (`NotificationGateway.API/appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=YOUR_SERVER;Initial Catalog=NotificationGatewayDb;User ID=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "Security": {
    "EncryptionKey": "<32-BYTE-BASE64-KEY>"
  },
  "Jwt": {
    "SecretKey": "<STRONG-JWT-SIGNING-SECRET>",
    "Issuer": "NotificationGateway",
    "Audience": "NotificationGatewayClients"
  },
  "Hangfire": {
    "UseMemoryStorage": false
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UseSsl": false,
    "FromEmail": "your-email@gmail.com",
    "FromName": "Notification Gateway",
    "Username": "your-email@gmail.com",
    "Password": "<16-CHAR-GMAIL-APP-PASSWORD>"
  },
  "Sms": {
    "ApiKey": "<VONAGE_API_KEY>",
    "ApiSecret": "<VONAGE_API_SECRET>",
    "SenderId": "NotifGate"
  },
  "WhatsApp": {
    "IdInstance": "<GREEN_API_ID_INSTANCE>",
    "ApiToken": "<GREEN_API_TOKEN>"
  }
}
```

> **Note**: Any provider whose credentials are omitted or left as placeholders automatically falls back to **simulation mode** so the gateway can still be tested end-to-end.

---

## 3. Running Locally

```powershell
# 1. Restore & build
dotnet build NotificationGateway.slnx

# 2. Run unit tests
dotnet test NotificationGateway.Tests/NotificationGateway.Tests.csproj

# 3. Start the API (auto-migrates DB and seeds default tenant, providers, and ORDER_SHIPPED templates)
dotnet run --project NotificationGateway.API
```

### Runtime Endpoints
- **Web Test UI**: `http://localhost:5022/`
- **Swagger API Docs**: `http://localhost:5022/swagger`
- **Hangfire Dashboard**: `http://localhost:5022/hangfire`
- **SignalR Hub**: `http://localhost:5022/hubs/notifications`

---

## 4. Production Deployment Checklist
1. Move secrets out of `appsettings.json` into **Environment Variables** or **Azure Key Vault / Secret Manager**.
2. Replace `Jwt:SecretKey` and `Security:EncryptionKey` with unique, high-entropy production keys.
3. Restrict Hangfire Dashboard (`/hangfire`) to authenticated `Admin` users in production.
4. Upgrade **WhatsApp** from Green API Developer Plan (3-chat limit) to **Meta WhatsApp Cloud API** and **SMS** from Vonage Trial to a funded Vonage/Twilio account with registered Sender ID.
