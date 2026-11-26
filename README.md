# Multi-Tenant Notification Gateway

A standalone, multi-tenant notification gateway built with **.NET 9** and **Clean Architecture**, supporting unified dispatch across **Email (Gmail SMTP)**, **SMS (Vonage)**, **WhatsApp (Green API)**, **SignalR**, and **Webhooks (HMAC-SHA256)** with **per-channel Handlebars templates** and **Admin / Customer** role-based access.

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
| **Web Test UI** | **`http://localhost:5022/`** | Interactive dashboard to dispatch notifications across all 5 channels, watch live **SignalR** push notifications, track delivery attempts, and inspect incoming **HMAC-SHA256 Webhooks**. Includes **Login / Register (`Admin` or `Customer`)** — `Admin` unlocks the **Admin Dashboard** (users count, notification types, and filters), while `Customer` sees only the standard UI. |
| **Swagger API Docs** | **`http://localhost:5022/swagger`** | Full OpenAPI / Swagger UI for testing all REST endpoints |
| **HTTP Test File** | [`NotificationGateway.API/NotificationGateway.API.http`](NotificationGateway.API/NotificationGateway.API.http) | Ready-to-run REST requests for Visual Studio / VS Code |

---

## 📚 Documentation (`docs/`)

Read the files in the [`docs/`](docs) folder for complete architecture, design, security, testing, and deployment details:

| File | Description |
|---|---|
| [`docs/Multi_Tenant_Notification_Gateway_Architecture.md`](docs/Multi_Tenant_Notification_Gateway_Architecture.md) | **Architecture Guide**: Clean Architecture layers (`Domain`, `Application`, `Infrastructure`, `API`, `Tests`), Handlebars per-channel template resolution, Hangfire outbox/delivery jobs, RBAC (`Admin` vs `Customer`), and provider adapters. |
| [`docs/Notification_Channels_and_Providers_Comparison.md`](docs/Notification_Channels_and_Providers_Comparison.md) | **Channels & Providers Comparison**: Compares `Email` (Gmail SMTP), `Sms` (Vonage), `WhatsApp` (Green API), `SignalR`, and `Webhook` (HMAC-SHA256), including Egypt (`+20`) formatting and template formats. |
| [`docs/Multi_Tenancy_Strategy.md`](docs/Multi_Tenancy_Strategy.md) | **Multi-Tenancy Strategy**: Shared-schema isolation using `TenantId` & `ApplicationId`, `TenantResolutionMiddleware` (headers, JWT claims, fallback), and SignalR group isolation. |
| [`docs/Database_ERD.md`](docs/Database_ERD.md) | **Database Schema & ERD**: Entity-Relationship Diagram and SQL Server table breakdown for tenants, users, notification types, templates, notifications, deliveries, attempts, outbox, and provider configs. |
| [`docs/Notification_Routing_and_Fallback.md`](docs/Notification_Routing_and_Fallback.md) | **Routing & Fallback**: `ProviderResolver` priority order, Hangfire retry & dead-letter handling (5 attempts), HTML-stripping template fallback, and provider simulation fallbacks. |
| [`docs/Security_and_Secrets_Management.md`](docs/Security_and_Secrets_Management.md) | **Security & Secrets**: JWT Bearer auth, PBKDF2 password hashing, `Admin`/`Customer` RBAC, AES-256-CBC credential encryption, HMAC-SHA256 webhook signing, and secrets best practices. |
| [`docs/API_Documentation.md`](docs/API_Documentation.md) | **API Reference**: Endpoint catalog across Auth, Notifications, Notification Types, Templates, Provider Configurations, Admin Dashboard, Webhooks, and SignalR. |
| [`docs/API_Testing_Walkthrough.md`](docs/API_Testing_Walkthrough.md) | **End-to-End Testing Guide**: Step-by-step walkthrough for Web UI (`/`), Swagger (`/swagger`), per-channel templates, multi-channel dispatch, delivery tracking, idempotency, and Admin Dashboard. |
| [`docs/Testing_Report.md`](docs/Testing_Report.md) | **Testing Report**: Automated unit test results (15/15 passing in `NotificationGateway.Tests`) and manual verification coverage. |
| [`docs/Deployment_and_Configuration.md`](docs/Deployment_and_Configuration.md) | **Deployment & Configuration**: Prerequisites, `appsettings.json` reference, local run commands, and production hardening checklist. |
| [`docs/notification-provider-defaults.md`](docs/notification-provider-defaults.md) | **Provider Credentials Reference**: Reference values for SMS (Vonage, restricted to `01008552396` on trial), WhatsApp (Green API), and SMTP (Gmail) that can be entered per user in the Provider Settings UI, along with security & rotation notes. |
