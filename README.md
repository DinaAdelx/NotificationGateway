# Multi-Tenant Notification Gateway

A standalone, multi-tenant notification gateway built with **.NET 9** and **Clean Architecture**, supporting unified dispatch across **Email (Gmail SMTP)**, **SMS (Vonage)**, **WhatsApp (Green API)**, **SignalR**, and **Webhooks (HMAC-SHA256)** with **per-channel Handlebars templates**.

---

## 🚀 Quick Start

1. Configure your database connection and provider credentials (`Smtp`, `Sms`, `WhatsApp`) in [`NotificationGateway.API/appsettings.json`](NotificationGateway.API/appsettings.json).
2. Run the API:
   ```powershell
   dotnet run --project NotificationGateway.API
   ```
   *(Database migrations, default tenant/application, provider configs, and `ORDER_SHIPPED` per-channel templates are seeded automatically on startup.)*

---

## 🌐 Interactive UI & API Endpoints

| Interface | URL | Purpose |
|---|---|---|
| **Web Test UI** | **`http://localhost:5022/`** | Interactive dashboard to dispatch notifications across all 5 channels, watch live **SignalR** push notifications, track delivery attempts, and inspect incoming **HMAC-SHA256 Webhooks** |
| **Swagger API Docs** | **`http://localhost:5022/swagger`** | Full OpenAPI / Swagger UI for testing all REST endpoints |
| **HTTP Test File** | [`NotificationGateway.API/NotificationGateway.API.http`](NotificationGateway.API/NotificationGateway.API.http) | Ready-to-run REST requests for Visual Studio / VS Code |

---

## 📚 Documentation (`docs/`)

Read the files in the [`docs/`](docs) folder for detailed guides and architecture references:

| File | Description |
|---|---|
| [`docs/API_Testing_Walkthrough.md`](docs/API_Testing_Walkthrough.md) | **End-to-End Testing Guide**: Covers both the Web UI (`/`) and Swagger (`/swagger`), user registration & JWT auth, registering notification types, creating & publishing **per-channel templates** (Email HTML, SMS raw text, WhatsApp, SignalR, Webhook JSON), submitting multi-channel notifications, tracking delivery attempts, and testing idempotency. |
| [`docs/Multi_Tenant_Notification_Gateway_Architecture.md`](docs/Multi_Tenant_Notification_Gateway_Architecture.md) | **Architecture & Provider Guide**: Explains the Clean Architecture layers (`Domain`, `Application`, `Infrastructure`, `API`, `Tests`), Handlebars per-channel template resolution & HTML-stripping fallback, Hangfire outbox/delivery jobs, and all provider adapters (`SmtpEmailProvider`, `SmsNotificationProvider`, `WhatsAppNotificationProvider`, `SignalRNotificationProvider`, `WebhookNotificationProvider`, `InAppProvider`). |
| [`docs/Database_ERD.md`](docs/Database_ERD.md) | **Database Schema & ERD**: Entity-Relationship Diagram and table breakdown for tenants, application clients, notification types, templates, notifications, deliveries, delivery attempts, outbox, and provider configurations. |
