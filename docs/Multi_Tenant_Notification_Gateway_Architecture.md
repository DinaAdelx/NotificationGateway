# Multi-Tenant Notification Gateway — Architecture Guide

## Overview
A standalone, project-agnostic, multi-tenant Notification Gateway built following **Clean Architecture** principles.

### Key Layers
- **NotificationGateway.Domain**: Contains core domain entities (`Tenant`, `ApplicationClient`, `NotificationTypeDefinition`, `NotificationTemplate`, `Notification`, `NotificationDelivery`, `DeliveryAttempt`, `RoutingRule`, `TenantProviderConfiguration`, `UserPreference`, `AuditLog`, `Outbox`), enums, and repository/context interfaces. Zero external infrastructure dependencies.
- **NotificationGateway.Application**: CQRS use-cases via MediatR (`SubmitNotification`, `GetNotification`, `CancelNotification`, `RegisterNotificationType`, `CreateTemplate`, `PreviewTemplate`, `PublishTemplate`), domain abstractions (`INotificationProvider`, `IProviderResolver`, `ITemplateRenderer`, `IPayloadValidator`, `ICredentialEncryptor`), and payload validation rules.
- **NotificationGateway.Infrastructure**: EF Core 9 SqlServer persistence, repository implementations, Handlebars template rendering, NJsonSchema validation, AES-256 credential encryption, provider adapters (`InApp`, `SmtpEmail`, `SignalR`, and test `MockProvider`), and Hangfire background jobs (`DeliveryDispatcher`, `OutboxProcessor`).
- **NotificationGateway.API**: ASP.NET Core Web API with JWT & custom header tenant resolution middleware (`X-Tenant-Id`, `X-Application-Id`), REST endpoints, SignalR notification hub, and Swagger documentation.
- **NotificationGateway.Tests**: Unit and integration test suite with FluentAssertions and Moq.

## Getting Started
1. Configure connection string in `appsettings.json`.
2. Run database migrations: `dotnet ef database update --project NotificationGateway.Infrastructure --startup-project NotificationGateway.API`.
3. Start the API: `dotnet run --project NotificationGateway.API`.
4. Open Swagger UI at `/swagger` to test dynamic type creation, template previewing, provider configuration, and sending notifications.
