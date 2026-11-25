# Multi-Tenant Notification Gateway — Architecture Guide

## Overview
A standalone, project-agnostic, multi-tenant Notification Gateway built following **Clean Architecture** principles. It supports dispatching a single notification across **Email, SMS, WhatsApp, SignalR, and Webhooks**, with **per-channel Handlebars templates** per notification type and **Role-Based Access Control (`Admin` vs `Customer`)**.

### Key Layers
- **NotificationGateway.Domain**: Core domain entities (`Tenant`, `ApplicationClient`, `User`, `NotificationTypeDefinition`, `NotificationTemplate`, `Notification`, `NotificationDelivery`, `DeliveryAttempt`, `RoutingRule`, `TenantProviderConfiguration`, `UserPreference`, `AuditLog`, `Outbox`), enums (`NotificationChannel`: `Email=0, Sms=1, WhatsApp=2, Push=3, InApp=4, SignalR=5, Voice=6, Webhook=7`), and repository/context interfaces. Zero external infrastructure dependencies.
- **NotificationGateway.Application**: CQRS use-cases via MediatR (`SubmitNotification`, `GetNotification`, `CancelNotification`, `RegisterNotificationType`, `CreateTemplate`, `PreviewTemplate`, `PublishTemplate`, `RegisterUser`, `LoginUser`), domain abstractions (`INotificationProvider`, `IProviderResolver`, `ITemplateRenderer`, `IPayloadValidator`, `ICredentialEncryptor`), and payload validation rules.
- **NotificationGateway.Infrastructure**:
  - EF Core 9 SQL Server persistence + repository implementations
  - **Handlebars Template Renderer** (`HandlebarsTemplateRenderer`): Resolves `(NotificationTypeDefinitionId, Channel, Language)` so each channel (`Email` HTML, `Sms` raw text, `WhatsApp` markdown, `SignalR` toast, `Webhook` JSON) has its own tailored format, with an automatic HTML-stripping safety fallback if a plain-text channel falls back to an HTML template.
  - NJsonSchema payload validation & AES-256 credential encryption
  - **Provider Adapters** (each falls back to simulation mode when credentials are omitted):
    - `SmtpEmailProvider` — Gmail / SMTP via MailKit (`StartTls` on port 587)
    - `SmsNotificationProvider` — Vonage REST API (`https://rest.nexmo.com/sms/json`)
    - `WhatsAppNotificationProvider` — Green API (`https://api.green-api.com/waInstance{id}/sendMessage/{token}`)
    - `SignalRNotificationProvider` — Real-time SignalR push to `{tenantId}:{userId}`, `{userId}`, and `All`
    - `WebhookNotificationProvider` — HTTP POST with `X-Hub-Signature-256` (HMAC-SHA256), `X-Webhook-Timestamp`, `X-Event-ID`, `X-Delivery-Attempt`
    - `InAppProvider` — In-app notification persistence
  - Hangfire background jobs (`OutboxProcessor`, `DeliveryDispatcher`) with exponential backoff retries.
- **NotificationGateway.API**: ASP.NET Core Web API with JWT & custom header tenant resolution middleware (`X-Tenant-Id`, `X-Application-Id`), **Role-Based Access Control (`Admin` vs `Customer`)**, JSON string enum serialization, interactive Web Test UI at `/` (`wwwroot/` — shows the **Admin Dashboard** tab with user/type/delivery counts & filters only to `Admin` users, while `Customer` users see only the standard notification UI), SignalR hub (`/hubs/notifications`), test webhook consumer (`/api/webhooks/receive-test`), and Swagger UI at `/swagger`.
- **NotificationGateway.Tests**: Unit test suite (xUnit + FluentAssertions).

## Role-Based Access Control (`Admin` vs `Customer`)
- **`Admin`**: Full access to all APIs (`AdminDashboardController`, `ProviderConfigurationsController`, creating/deleting `NotificationTypes`, creating/publishing `Templates`) plus the **Admin Dashboard** tab in the Web UI (`/`).
- **`Customer`**: Access to notification dispatch (`NotificationsController`), delivery tracking, SignalR live feed, and webhook inspector — Admin Dashboard and admin management endpoints are restricted.

## Configuration (`NotificationGateway.API/appsettings.json`)
- **`ConnectionStrings:DefaultConnection`**: SQL Server connection string.
- **`Smtp`**: Gmail SMTP settings (`Host`, `Port`, `UseSsl`, `FromEmail`, `FromName`, `Username`, `Password` — requires a 16-char Gmail App Password).
- **`Sms`**: Vonage credentials (`ApiKey`, `ApiSecret`, `SenderId`).
- **`WhatsApp`**: Green API credentials (`IdInstance`, `ApiToken`).

## Getting Started
1. Configure `ConnectionStrings`, `Smtp`, `Sms`, and `WhatsApp` in `NotificationGateway.API/appsettings.json`.
2. Start the API: `dotnet run --project NotificationGateway.API` (migrations and default `ORDER_SHIPPED` type + 5 channel-specific templates seed automatically).
3. Open the **Web Test UI** at `http://localhost:5022/` to test all 5 channels live (or login/register as `Admin` to access the Admin Dashboard).
4. Open **Swagger UI** at `http://localhost:5022/swagger` for full REST API testing.
